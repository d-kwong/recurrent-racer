"""Soft Actor-Critic for Recurrent Racer: replay -> twin Q targets -> actor -> alpha.

The unchanged Unity player supplies 15 observations, two continuous actions,
rewards and episode boundaries. Run --help for train/evaluate/view commands.
"""
import argparse
from collections import deque
import copy
import hashlib
import importlib.metadata as metadata
import json
import math
import os
from pathlib import Path
import random
import shutil
import signal
import sys
import time

import numpy as np
import torch
from torch import nn
from torch.distributions import Normal
from torch.nn import functional as F
from torch.utils.tensorboard import SummaryWriter
from mlagents_envs.base_env import ActionTuple
import unity_env as unity

ROOT=unity.ROOT
DEFAULT_SOURCE=ROOT/'policies/initial-mean.pt'


class Actor(nn.Module):
    """State-dependent Gaussian; tanh bounds both steering and signed throttle."""
    def __init__(self):
        super().__init__()
        self.trunk=nn.Sequential(nn.Linear(15,64),nn.Tanh(),nn.Linear(64,64),nn.Tanh())
        self.mean=nn.Linear(64,2)
        self.log_std=nn.Linear(64,2)
        nn.init.uniform_(self.mean.weight,-.01,.01); nn.init.zeros_(self.mean.bias)
        self.mean.bias.data[1]=math.atanh(.2)
        nn.init.zeros_(self.log_std.weight); nn.init.constant_(self.log_std.bias,math.log(.3))

    def distribution(self, observations):
        features=self.trunk(observations)
        return Normal(self.mean(features),self.log_std(features).clamp(-5,1).exp())

    def sample(self, observations):
        # Unlike REINFORCE, SAC differentiates actions THROUGH the learned Q.
        distribution=self.distribution(observations)
        u=distribution.rsample(); actions=torch.tanh(u)
        correction=2*(math.log(2)-u-F.softplus(-2*u))
        logp=(distribution.log_prob(u)-correction).sum(-1)
        return actions,logp

    def warm_start(self,path):
        data=torch.load(path,map_location='cpu',weights_only=True)
        if data['format']!='racer-mean-v1': raise ValueError('Expected mean-controller initialization export')
        self.trunk.load_state_dict(data['trunk'])
        self.mean.load_state_dict(data['mean'])
        return data['source_version']


class TwinQ(nn.Module):
    def __init__(self):
        super().__init__()
        def network():
            return nn.Sequential(nn.Linear(17,128),nn.ReLU(),nn.Linear(128,128),nn.ReLU(),nn.Linear(128,1))
        self.q1=network(); self.q2=network()

    def forward(self, observations, actions):
        inputs=torch.cat((observations,actions),-1)
        return self.q1(inputs).squeeze(-1),self.q2(inputs).squeeze(-1)


class Replay:
    """Uniform replay; retains bad outcomes as well as successful ones."""
    def __init__(self,capacity,seed):
        self.capacity=capacity; self.size=0; self.position=0
        self.rng=np.random.default_rng(seed)
        self.arrays={key:np.empty((capacity,*shape),np.float32) for key,shape in
            [('obs',(15,)),('actions',(2,)),('rewards',()),('next_obs',(15,)),('terminated',())]}

    def add(self,obs,action,reward,next_obs,terminated):
        for key,value in zip(self.arrays,(obs,action,reward,next_obs,terminated)):
            self.arrays[key][self.position]=value
        self.position=(self.position+1)%self.capacity; self.size=min(self.size+1,self.capacity)

    def sample(self,count):
        indices=self.rng.integers(self.size,size=count)
        return {key:torch.from_numpy(value[indices]) for key,value in self.arrays.items()}

    def state_dict(self):
        return dict(capacity=self.capacity,size=self.size,position=self.position,
            rng=json.dumps(self.rng.bit_generator.state),
            arrays={key:torch.from_numpy(value[:self.size].copy()) for key,value in self.arrays.items()})

    def load_state_dict(self,state):
        if state['capacity']!=self.capacity: raise ValueError('Resume must preserve replay capacity')
        self.size=state['size']; self.position=state['position']
        self.rng.bit_generator.state=json.loads(state['rng'])
        for key,value in state['arrays'].items(): self.arrays[key][:self.size]=value.numpy()


def bellman_target(rewards,terminated,next_q,next_logp,gamma,alpha,reward_scale):
    # True task endings have zero tail. Time-limit interruption bootstraps from
    # the actual terminal observation, never the new episode's reset observation.
    return reward_scale*rewards+gamma*(1-terminated)*(next_q-alpha*next_logp)


class SAC:
    def __init__(self,config):
        self.config=config; self.actor=Actor(); self.critic=TwinQ()
        self.target=copy.deepcopy(self.critic).requires_grad_(False)
        self.log_alpha=nn.Parameter(torch.tensor(math.log(config['initial_alpha'])))
        self.actor_optimizer=torch.optim.Adam(self.actor.parameters(),lr=config['learning_rate'])
        self.critic_optimizer=torch.optim.Adam(self.critic.parameters(),lr=config['learning_rate'])
        self.alpha_optimizer=torch.optim.Adam([self.log_alpha],lr=config['learning_rate'])

    @property
    def alpha(self): return self.log_alpha.exp()

    def update(self,batch):
        c=self.config
        with torch.no_grad():
            next_action,next_logp=self.actor.sample(batch['next_obs'])
            next_q=torch.minimum(*self.target(batch['next_obs'],next_action))
            target=bellman_target(batch['rewards'],batch['terminated'],next_q,next_logp,
                                   c['gamma'],self.alpha,c['reward_scale'])
        q1,q2=self.critic(batch['obs'],batch['actions'])
        critic_loss=F.mse_loss(q1,target)+F.mse_loss(q2,target)
        unity.finite(critic_loss,'SAC critic loss')
        self.critic_optimizer.zero_grad(set_to_none=True); critic_loss.backward()
        critic_norm=nn.utils.clip_grad_norm_(self.critic.parameters(),10.,error_if_nonfinite=True)
        self.critic_optimizer.step(); self.critic_optimizer.zero_grad(set_to_none=True)
        # Freeze Q parameters, while preserving dQ/da for actor learning.
        self.critic.requires_grad_(False)
        try:
            actions,logp=self.actor.sample(batch['obs'])
            policy_q=torch.minimum(*self.critic(batch['obs'],actions))
            actor_loss=(self.alpha.detach()*logp-policy_q).mean()
            unity.finite(actor_loss,'SAC actor loss')
            self.actor_optimizer.zero_grad(set_to_none=True); actor_loss.backward()
            actor_norm=nn.utils.clip_grad_norm_(self.actor.parameters(),10.,error_if_nonfinite=True)
            self.actor_optimizer.step()
        finally: self.critic.requires_grad_(True)
        # Increase exploration pressure when entropy is below its target.
        alpha_loss=-(self.log_alpha*(logp.detach()+c['target_entropy'])).mean()
        unity.finite(alpha_loss,'SAC alpha loss')
        self.alpha_optimizer.zero_grad(set_to_none=True); alpha_loss.backward(); self.alpha_optimizer.step()
        with torch.no_grad():
            for source,destination in zip(self.critic.parameters(),self.target.parameters()):
                destination.lerp_(source,c['tau'])
            std=self.actor.distribution(batch['obs']).stddev.mean(0).tolist()
        for module in (self.actor,self.critic,self.target):
            for p in module.parameters(): unity.finite(p,'SAC parameters')
        unity.finite(self.alpha,'SAC alpha')
        return dict(critic_loss=float(critic_loss.detach()),actor_loss=float(actor_loss.detach()),
            alpha=float(self.alpha.detach()),entropy=float(-logp.detach().mean()),
            q_mean=float(torch.minimum(q1,q2).detach().mean()),target_mean=float(target.mean()),
            critic_gradient_norm=float(critic_norm),actor_gradient_norm=float(actor_norm),
            std_steer=std[0],std_throttle=std[1])


class UnityStream:
    """Align every delivered reward/next state with the previously sent action."""
    def __init__(self,env):
        self.env=env; self.active={}; self.reset()

    def reset(self):
        self.active.clear(); self.env.reset(); self.key=unity.runtime_behavior(self.env)

    def read(self):
        decisions,terminals=self.env.get_steps(self.key); transitions=[]; episodes=[]
        for steps in (decisions,terminals):
            assert len(steps)<=1,'One car expected'
            assert steps.obs[0].dtype==np.float32 and steps.obs[0].shape==(len(steps),15)
            unity.finite(steps.obs[0],'observations'); unity.finite(steps.reward,'rewards')
            assert np.all(np.abs(steps.obs[0])<=1.00001)
        for steps,is_terminal in ((terminals,True),(decisions,False)):
            for i,agent in enumerate(steps.agent_id):
                agent=int(agent); next_obs=steps.obs[0][i].copy(); reward=float(steps.reward[i])
                if agent not in self.active:
                    assert not is_terminal and abs(reward)<1e-6,'Reward without prior action'
                    self.active[agent]=dict(return_=0.,length=0)
                    continue
                episode=self.active[agent]
                assert 'obs' in episode,'Missing pending action'
                interrupted=bool(steps.interrupted[i]) if is_terminal else False
                transitions.append((episode.pop('obs'),episode.pop('action'),reward,next_obs,
                                    float(is_terminal and not interrupted)))
                episode['return_']+=reward; episode['length']+=1
                assert episode['length']<=1300,'Unexpected episode cap'
                if is_terminal:
                    # Current reward contract: +1 finish; other per-delivery rewards
                    # are bounded well below .5. This is an explicitly labelled proxy.
                    episode.update(interrupted=interrupted,reward_inferred_finish=not interrupted and reward>.5,
                                   estimated_seconds=episode['length']*.1)
                    episodes.append(episode); del self.active[agent]
        self.decisions=decisions
        return transitions,episodes

    @torch.no_grad()
    def act(self,actor):
        decisions=self.decisions
        if len(decisions):
            _,actions=unity.sample_actions(actor,torch.from_numpy(decisions.obs[0].copy()))
            for i,agent in enumerate(decisions.agent_id):
                self.active[int(agent)].update(obs=decisions.obs[0][i].copy(),action=actions[i].numpy().copy())
            self.env.set_actions(self.key,ActionTuple(continuous=actions.numpy()))
        self.env.step()


def rng_restore(state):
    random.setstate(state['python']); torch.set_rng_state(state['torch'])
    n=state['numpy']; np.random.set_state((n[0],np.asarray(n[1],dtype=np.uint32),*n[2:]))


def save(path,learner,replay,state,config,include_replay=True):
    data=dict(format='racer-sac-v1',config=config,state=dict(state),actor=learner.actor.state_dict(),
        critic=learner.critic.state_dict(),target=learner.target.state_dict(),log_alpha=learner.log_alpha.detach(),
        actor_optimizer=learner.actor_optimizer.state_dict(),critic_optimizer=learner.critic_optimizer.state_dict(),
        alpha_optimizer=learner.alpha_optimizer.state_dict(),rng=unity.rng_state())
    if include_replay: data['replay']=replay.state_dict()
    temp=path.with_suffix('.tmp')
    with temp.open('wb') as f: torch.save(data,f); f.flush(); os.fsync(f.fileno())
    os.replace(temp,path)


def load(path,learner,replay=None):
    data=torch.load(path,map_location='cpu',weights_only=True)
    assert data['format'] in ('racer-sac-v1','racer-sac-actor-v1')
    learner.actor.load_state_dict(data['actor'])
    for p in learner.actor.parameters(): unity.finite(p,'loaded SAC actor')
    if replay is not None:
        if data['format']!='racer-sac-v1': raise ValueError('Inference exports cannot resume training')
        for key in ('gamma','reward_scale','tau','learning_rate','batch_size','target_entropy','update_every','updates_per_cycle'):
            if learner.config[key]!=data['config'][key]: raise ValueError(f'Resume must preserve {key}')
        if 'replay' not in data: raise ValueError('Resume from latest.pt; this checkpoint contains weights only')
        learner.critic.load_state_dict(data['critic']); learner.target.load_state_dict(data['target'])
        learner.log_alpha.data.copy_(data['log_alpha'])
        for key in ('actor_optimizer','critic_optimizer','alpha_optimizer'):
            getattr(learner,key).load_state_dict(data[key])
        replay.load_state_dict(data['replay']); rng_restore(data['rng'])
    return data


def score_evaluation(episodes):
    finishes=[not e.interrupted and float(e.rewards[-1])>.5 for e in episodes]
    times=[len(e.rewards)*.1 for e,finish in zip(episodes,finishes) if finish]
    return dict(mean_return=float(np.mean([e.return_ for e in episodes])),
        reward_inferred_finish_fraction=float(np.mean(finishes)),
        mean_finished_seconds=float(np.mean(times)) if times else None,
        best_finished_seconds=min(times) if times else None,
        mean_length=float(np.mean([len(e.rewards) for e in episodes])))


def score_quality(endings, tasks):
    """Require measured original-start task evidence; never derive clean driving from return."""
    if not endings or not tasks or len(endings)%len(tasks):
        raise RuntimeError('Incomplete quality evaluation task evidence')
    if any(turn != -1 for seed, turn in tasks):
        raise ValueError('Quality selection requires original full-route starts; suffix probes are separate')
    required={'version','ticks','offAsphaltTicks','offAsphaltFraction','maximumReverseSeconds',
              'maximumStallSeconds','firstCornerPassed','cleanFinish'}
    finishes=[]; clean=[]; seconds=[]
    for ending in endings:
        q=ending.get('quality')
        if ending.get('reason') not in ('Crash','OffTrack','InvalidProgress','Finish','NoProgress','Timeout'):
            raise RuntimeError('Missing or invalid terminal reason')
        if not isinstance(q,dict) or not required <= q.keys() or q['version']!='physics-quality-v1':
            raise RuntimeError('Missing measured quality telemetry')
        if q['ticks']!=ending['physicsTicks'] or q['ticks']<=0 or not np.isfinite(ending.get('simulatedSeconds',float('nan'))) or abs(ending['simulatedSeconds']-q['ticks']*.02)>.0001:
            raise RuntimeError('Quality physics tick mismatch')
        numeric=[q[k] for k in ('offAsphaltFraction','maximumReverseSeconds','maximumStallSeconds')]
        if not np.isfinite(numeric).all() or not 0<=q['offAsphaltTicks']<=q['ticks'] or abs(q['offAsphaltFraction']-q['offAsphaltTicks']/q['ticks'])>1e-6 or min(numeric[1:])<0:
            raise RuntimeError('Invalid quality measurements')
        finish=ending['reason']=='Finish' and not ending['interrupted']
        measured_clean=finish and q['maximumReverseSeconds']<=.5001 and q['maximumStallSeconds']<=2.0001 and q['offAsphaltFraction']<=.05
        if bool(q['cleanFinish']) != measured_clean:
            raise RuntimeError('Inconsistent clean-finish telemetry')
        finishes.append(finish); clean.append(measured_clean)
        if finish: seconds.append(ending['simulatedSeconds'])
    return dict(quality_selection=True,measured_finish_fraction=float(np.mean(finishes)),
                clean_finish_fraction=float(np.mean(clean)),
                quality_mean_finished_seconds=float(np.mean(seconds)) if seconds else None)


def rank(result):
    # Time is comparable only when the complete identical original task set finished.
    if result.get('quality_selection'):
        return (result['measured_finish_fraction'], result['clean_finish_fraction'],
                -result['quality_mean_finished_seconds'] if result['measured_finish_fraction']==1 else 0.)
    # Full laps outrank partial progress. Reliability precedes speed.
    if result.get('track_mode')=='procedural':
        return (result['reward_inferred_finish_fraction'], result['normalized_progress'],
                -result['mean_finished_seconds'] if result['reward_inferred_finish_fraction']==1 else 0.)
    return (result['reward_inferred_finish_fraction'],
            -result['mean_finished_seconds'] if result['mean_finished_seconds'] is not None else -float('inf'),
            result['mean_return'])


def evaluate(env,actor,config,out,step,episodes,approaches=False,writer=None):
    rng=unity.rng_state(); results={}
    procedural=config.get('track_mode', 'fixed')=='procedural'
    if procedural and approaches:
        raise ValueError('Fixed-track approach curriculum is unavailable for procedural routes')
    if procedural:
        episodes *= len(unity.procedural_tasks(config, evaluation=True))
    try:
        for start in ([0,10,15,20] if approaches else [0]):
            spawn=dict(config) if procedural else dict(config,spawn_curriculum=bool(start),spawn_min=start,spawn_max=start,original_fraction=0)
            unity.configure_spawn(env,spawn,track_evaluation=True)
            rollouts,seconds,n=unity.evaluate(env,actor,episodes)
            result=score_evaluation(rollouts); result.update(approach_m=start,transitions=n,wall_seconds=seconds)
            if procedural:
                telemetry=unity.track_records(env.racing_log_path)
                endings=telemetry['episodes'][-len(rollouts):]
                if len(endings)!=len(rollouts):
                    raise RuntimeError('Missing realized procedural episode telemetry; selection cannot infer geometry')
                expected=unity.procedural_tasks(config,evaluation=True)
                if [(e['seed'],e['spawnTurnIndex']) for e in endings]!=[expected[i % len(expected)] for i in range(len(rollouts))]:
                    raise RuntimeError('Realized evaluation seed sequence disagrees with requested seeds')
                result['normalized_progress']=float(np.mean([1. if e['reason']=='Finish' else np.clip(e['travelMetres']/e['finishDistance'],0,1) for e in endings]))
                if config.get('quality_selection',False):
                    measured=score_quality(endings,expected)
                    if measured['measured_finish_fraction']!=result['reward_inferred_finish_fraction']:
                        raise RuntimeError('Finish telemetry disagrees with reward finish proxy')
                    result.update(measured)
                result['track_mode']='procedural'
                result['evaluation_tasks']=','.join(f'{seed}:{turn}' for seed,turn in expected)
                (out/f'track-eval-{step:09d}.json').write_text(json.dumps(telemetry,indent=2))
            unity.save_trajectories(out/f'eval-{step:09d}-start{start}.npz',rollouts)
            for i,e in enumerate(rollouts):
                unity.append_csv(out/'evaluation-episodes.csv',dict(step=step,start_m=start,episode=i,
                    return_=e.return_,length=len(e.rewards),estimated_seconds=len(e.rewards)*.1,
                    interrupted=e.interrupted,reward_inferred_finish=not e.interrupted and float(e.rewards[-1])>.5,
                    mean_forward_mps=float(e.observations[:,11].mean()*48),
                    peak_steering=float(np.abs(e.actions[:,0]).max()),
                    peak_applied_degrees=float(np.abs(e.observations[:,14]).max()*22)))
            unity.append_csv(out/'evaluations.csv',dict(step=step,**result)); results[str(start)]=result
            if writer is not None:
                for key,value in result.items():
                    if isinstance(value,(float,int)): writer.add_scalar(f'evaluation/start{start}/{key}',value,step)
            print(f'EVAL step={step:,} start={start}m return={result["mean_return"]:.4f} '
                  f'finish_proxy={result["reward_inferred_finish_fraction"]:.0%} '
                  f'lap_seconds_est={result["mean_finished_seconds"]}',flush=True)
    finally: rng_restore(rng)
    return results


def file_sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''): digest.update(chunk)
    return digest.hexdigest()


def build_manifest(path):
    """Fingerprint player data and managed assemblies as well as the executable."""
    root=Path(path).resolve()
    files=[dict(path=str(p.relative_to(root)),bytes=p.stat().st_size,sha256=file_sha(p))
           for p in sorted(root.rglob('*')) if p.is_file()]
    if not files: raise ValueError(f'Missing Unity player: {root}')
    # Unity writes diagnostic timer JSON inside the app after every launch.
    # Preserve those file hashes, but keep them out of immutable build identity.
    runtime=[r for r in files if r['path'].startswith('Contents/ML-Agents/Timers/')]
    immutable=[r for r in files if r not in runtime]
    return dict(path=str(root),files=files,runtime_artifacts=runtime,
                sha256=hashlib.sha256(json.dumps(immutable,sort_keys=True).encode()).hexdigest())


def clean_development_stop(config, result):
    return (config.get('stop_on_clean_development',False) and result.get('quality_selection') is True
            and result.get('measured_finish_fraction')==1 and result.get('clean_finish_fraction')==1)


def train(config,resume=None,warm_start=None):
    manifest=build_manifest(config['env']); config['build_sha256']=manifest['sha256']
    random.seed(config['seed']); np.random.seed(config['seed']); torch.manual_seed(config['seed'])
    torch.set_num_threads(1); learner=SAC(config); replay=Replay(config['replay_capacity'],config['seed']+100)
    state=dict(transitions=0,updates=0,episodes=0,eval_transitions=0,best_evaluation=None,last_evaluation_step=None)
    if warm_start and not resume:
        config['warm_start']=dict(path=str(warm_start.resolve()),sha256=file_sha(warm_start),
                                  source_version=learner.actor.warm_start(warm_start))
    inherited_best=None
    if resume:
        restored=load(resume,learner,replay); state=restored['state']
        if config['build_sha256']!=restored['config'].get('build_sha256'):
            raise ValueError('Unity build changed; use a new experiment rather than resume')
        for key in ('spawn_curriculum','spawn_min','spawn_max','original_fraction','env','learning_starts'):
            if config[key]!=restored['config'][key]: raise ValueError(f'Resume must preserve {key}')
        for key in ('track_mode','track_layout','track_seed','track_train_seeds','track_eval_seeds','track_segments',
                    'track_straight_min','track_straight_max','track_radius_min','track_radius_max',
                    'track_angle_min','track_angle_max','track_spacing','track_train_spawn_mode','track_spawn_seed',
                    'track_eval_spawn_indices','track_eval_spawn_approach','quality_selection','quality_telemetry'):
            # Older fixed checkpoints have no procedural fields; defaults retain fixed compatibility.
            old=restored['config'].get(key, 'fixed' if key=='track_mode' else 'open' if key=='track_layout' else config.get(key))
            current=config.get(key, 'fixed' if key=='track_mode' else 'open' if key=='track_layout' else None)
            if current!=old: raise ValueError(f'Resume must preserve {key}')
        config['resume']=dict(path=str(resume.resolve()),sha256=file_sha(resume))
        candidate=resume.parent/'best.pt'
        if candidate.exists():
            best=torch.load(candidate,map_location='cpu',weights_only=True)
            if (best['state']['transitions']<=state['transitions'] and
                    best['state']['best_evaluation']==state['best_evaluation'] and
                    best['config'].get('build_sha256')==config['build_sha256']):
                inherited_best=candidate
        if inherited_best is None: state['best_evaluation']=None
    out=Path(config['output']).resolve(); out.mkdir(parents=True,exist_ok=False)
    config['trainer_sha256']=file_sha(Path(__file__))
    config['dependencies']={key:metadata.version(key) for key in ('torch','numpy','mlagents-envs','tensorboard')}
    (out/'config.json').write_text(json.dumps(config,indent=2)); (out/'train_sac.py').write_bytes(Path(__file__).read_bytes())
    (out/'build-manifest.json').write_text(json.dumps(manifest,indent=2))
    if inherited_best is not None: shutil.copy2(inherited_best,out/'best.pt')
    writer=SummaryWriter(str(out/'tensorboard')); recent=deque(maxlen=50)
    metrics={key:0. for key in ('critic_loss','actor_loss','entropy','q_mean','target_mean',
                               'critic_gradient_norm','actor_gradient_norm')}
    metrics.update(alpha=config['initial_alpha'],std_steer=.3,std_throttle=.3)
    env=None; started=time.monotonic(); last_log=state['transitions']; last_save=state['transitions']
    session_start_transitions=state['transitions']
    next_eval=state['transitions']+config['eval_frequency']; outcome='completed'
    save(out/'latest.pt',learner,replay,state,config)
    print(f'SAC budget: {config["max_transitions"]:,} total training transitions; '
          f'CPU, one environment. Output: {out}\nCtrl+C saves replay/optimizers/RNG to latest.pt.',flush=True)
    try:
        env=unity.connect(config,out/'unity.log'); stream=UnityStream(env)
        if config['evaluate_initial']:
            result=evaluate(env,learner.actor,config,out,state['transitions'],config['eval_episodes'],writer=writer)
            state['eval_transitions']+=result['0']['transitions']
            state['last_evaluation_step']=state['transitions']
            if state['best_evaluation'] is None or rank(result['0'])>rank(state['best_evaluation']):
                state['best_evaluation']=result['0']; save(out/'best.pt',learner,replay,state,config,False)
            if clean_development_stop(config,result['0']):
                outcome='development_gate_met'; state['outcome']=outcome
                save(out/'development-gate.pt',learner,replay,state,config,False)
            else: unity.configure_spawn(env,config); stream.reset()
        else: unity.configure_spawn(env,config); stream.reset()
        while outcome!='development_gate_met' and state['transitions']<config['max_transitions']:
            if unity.STOP: outcome='interrupted'; break
            if config['max_seconds'] and time.monotonic()-started>=config['max_seconds']:
                outcome='wall_time_limit'; break
            transitions,episodes=stream.read()
            for transition in transitions:
                replay.add(*transition); state['transitions']+=1
                if replay.size>=config['learning_starts'] and state['transitions']%config['update_every']==0:
                    for _ in range(config['updates_per_cycle']):
                        metrics=learner.update(replay.sample(config['batch_size'])); state['updates']+=1
            for episode in episodes:
                state['episodes']+=1; recent.append(episode)
                row=dict(step=state['transitions'],episode=state['episodes'],**episode)
                unity.append_csv(out/'episodes.csv',row)
                writer.add_scalar('training/episode_return',episode['return_'],state['transitions'])
            if state['transitions']-last_log>=config['log_frequency']:
                elapsed=time.monotonic()-started
                row=dict(step=state['transitions'],updates=state['updates'],episodes=state['episodes'],
                    rolling_return=float(np.mean([e['return_'] for e in recent])) if recent else 0.,
                    rolling_length=float(np.mean([e['length'] for e in recent])) if recent else 0.,
                    rolling_finish_proxy=float(np.mean([e['reward_inferred_finish'] for e in recent])) if recent else 0.,
                    wall_seconds=elapsed,transitions_per_wall_second=(state['transitions']-session_start_transitions)/max(elapsed,.001),**metrics)
                unity.append_csv(out/'updates.csv',row)
                for key,value in row.items(): writer.add_scalar(f'training/{key}',value,state['transitions'])
                writer.flush(); last_log=state['transitions']
                print(f'SAC steps={state["transitions"]:,}/{config["max_transitions"]:,} updates={state["updates"]:,} '
                    f'episodes={state["episodes"]} return50={row["rolling_return"]:.3f} '
                    f'length50={row["rolling_length"]:.0f} finish50={row["rolling_finish_proxy"]:.0%} '
                    f'alpha={metrics.get("alpha",config["initial_alpha"]):.4f} '
                    f'std={metrics.get("std_steer",.3):.3f}/{metrics.get("std_throttle",.3):.3f} '
                    f'wall={elapsed/60:.1f}m',flush=True)
            if state['transitions']>=next_eval and episodes and state['transitions']<config['max_transitions']:
                # Evaluate between episodes: no artificial replay terminals.
                result=evaluate(env,learner.actor,config,out,state['transitions'],config['eval_episodes'],writer=writer)
                state['eval_transitions']+=result['0']['transitions']; state['last_evaluation_step']=state['transitions']
                if state['best_evaluation'] is None or rank(result['0'])>rank(state['best_evaluation']):
                    state['best_evaluation']=result['0']; save(out/'best.pt',learner,replay,state,config,False)
                save(out/f'policy-{state["transitions"]:09d}.pt',learner,replay,state,config,False)
                if clean_development_stop(config,result['0']):
                    outcome='development_gate_met'; state['outcome']=outcome
                    save(out/'development-gate.pt',learner,replay,state,config,False)
                    break
                next_eval=state['transitions']+config['eval_frequency']
                unity.configure_spawn(env,config); stream.reset(); stream.read()
            if state['transitions']-last_save>=config['checkpoint_frequency']:
                save(out/'latest.pt',learner,replay,state,config); last_save=state['transitions']
            if state['transitions']<config['max_transitions']: stream.act(learner.actor)
        if not unity.STOP and state['last_evaluation_step']!=state['transitions']:
            result=evaluate(env,learner.actor,config,out,state['transitions'],config['eval_episodes'],writer=writer)
            state['eval_transitions']+=result['0']['transitions']; state['last_evaluation_step']=state['transitions']
            if state['best_evaluation'] is None or rank(result['0'])>rank(state['best_evaluation']):
                state['best_evaluation']=result['0']; save(out/'best.pt',learner,replay,state,config,False)
            if clean_development_stop(config,result['0']):
                outcome='development_gate_met'; state['outcome']=outcome
                save(out/'development-gate.pt',learner,replay,state,config,False)
    except (unity.StopRequested,unity.UnityCommunicationException,unity.UnityTimeOutException):
        if not unity.STOP: outcome='communication_failure'; raise
        outcome='interrupted'
    except Exception:
        outcome='technical_failure'; raise
    finally:
        state['outcome']=outcome; state['session_wall_seconds']=time.monotonic()-started
        save(out/'latest.pt',learner,replay,state,config)
        (out/'final-state.json').write_text(json.dumps(state,indent=2))
        if env is not None: env.close()
        writer.close()
        print(f'Saved {out/"latest.pt"}. Outcome={outcome}; steps={state["transitions"]:,}.',flush=True)


def accepted_view_defaults(arguments, root=ROOT):
    """Bare view resolves only a committed, accepted and hash-verified policy manifest."""
    if not arguments or arguments[0]!='view' or any(
            flag in ('--checkpoint','--track-mode','--track-layout','--config') or flag.startswith(('--checkpoint=','--track-mode=','--track-layout=','--config='))
            for flag in arguments[1:]):
        return {}
    manifest=root/'configs/quality/accepted-view.json'
    if not manifest.exists():
        return {}
    accepted=json.loads(manifest.read_text())
    if accepted.get('gate_passed') is not True:
        return {}
    checkpoint=(root/accepted['checkpoint']).resolve()
    if not checkpoint.is_relative_to(root.resolve()) or file_sha(checkpoint)!=accepted['checkpoint_sha256']:
        raise ValueError('Accepted view checkpoint provenance mismatch')
    if accepted.get('track_mode')!='procedural' or accepted.get('track_layout')!='compact' or accepted.get('track_seed')!=1009:
        raise ValueError('Invalid accepted procedural view defaults')
    geometry_keys=('track_segments','track_straight_min','track_straight_max','track_radius_min',
                   'track_radius_max','track_angle_min','track_angle_max','track_spacing')
    defaults=dict(checkpoint=checkpoint,track_mode='procedural',track_layout='compact',track_seed=1009,
                  track_eval_spawn_indices=[-1],
                  quality_telemetry=accepted.get('quality_telemetry') is True)
    defaults.update({key: accepted[key] for key in geometry_keys})
    if not any(flag=='--track-seed' or flag.startswith('--track-seed=') for flag in arguments[1:]):
        defaults['track_eval_seeds']=[1009]
    return defaults


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode',choices=['train','evaluate','view'])
    parser.add_argument('--env',default=str(ROOT/'RacingEnvironment/Builds/macOS/RacingCurriculum.app'))
    parser.add_argument('--output',type=Path)
    parser.add_argument('--seed',type=int,default=7)
    parser.add_argument('--port',type=int,default=5505)
    parser.add_argument('--time-scale',type=float,default=20)
    parser.add_argument('--max-transitions',type=int,default=1000000,help='Total transitions including resumed data')
    parser.add_argument('--max-seconds',type=float,default=0,help='Optional session wall cap; 0 means no wall cap')
    parser.add_argument('--learning-rate',type=float,default=.0003)
    parser.add_argument('--gamma',type=float,default=.995)
    parser.add_argument('--reward-scale',type=float,default=10,help='Learner units only; Unity reward stays unchanged')
    parser.add_argument('--initial-alpha',type=float,default=.02)
    parser.add_argument('--target-entropy',type=float,default=-2)
    parser.add_argument('--tau',type=float,default=.005)
    parser.add_argument('--batch-size',type=int,default=256)
    parser.add_argument('--replay-capacity',type=int,default=250000)
    parser.add_argument('--learning-starts',type=int,default=2000)
    parser.add_argument('--update-every',type=int,default=4)
    parser.add_argument('--updates-per-cycle',type=int,default=4)
    parser.add_argument('--eval-frequency',type=int,default=10000)
    parser.add_argument('--eval-episodes',type=int,default=3)
    parser.add_argument('--checkpoint-frequency',type=int,default=10000)
    parser.add_argument('--log-frequency',type=int,default=1000)
    parser.add_argument('--evaluate-initial',action=argparse.BooleanOptionalAction,default=True)
    parser.add_argument('--spawn-curriculum',action=argparse.BooleanOptionalAction,default=True)
    parser.add_argument('--track-mode',choices=['fixed','procedural'],default='fixed')
    parser.add_argument('--track-layout',choices=['open','compact'],default='open')
    parser.add_argument('--track-seed',type=int,default=101)
    parser.add_argument('--track-train-seeds',type=int,nargs='+')
    parser.add_argument('--track-eval-seeds',type=int,nargs='+')
    parser.add_argument('--track-train-spawn-mode',choices=['original','curriculum'],default='curriculum')
    parser.add_argument('--track-spawn-seed',type=int,default=7)
    parser.add_argument('--track-eval-spawn-indices',type=int,nargs='+',help='-1 original, 0..segments-1 turn entry; default all')
    parser.add_argument('--track-eval-spawn-approach',type=float,default=15)
    parser.add_argument('--track-segments',type=int,default=5)
    for name, default in [('straight-min',25),('straight-max',55),('radius-min',22),
                          ('radius-max',42),('angle-min',25),('angle-max',100),('spacing',2)]:
        parser.add_argument('--track-'+name,type=float,default=default)
    parser.add_argument('--spawn-min',type=float,default=10)
    parser.add_argument('--spawn-max',type=float,default=20)
    parser.add_argument('--original-fraction',type=float,default=.25)
    parser.add_argument('--warm-start',type=Path,default=DEFAULT_SOURCE,help='Mean-controller initialization; new SAC critics/replay/optimizers')
    parser.add_argument('--from-scratch',action='store_true')
    parser.add_argument('--resume',type=Path)
    parser.add_argument('--checkpoint',type=Path)
    parser.add_argument('--approaches',action='store_true',help='Evaluate original plus fixed10/15/20m approaches')
    parser.add_argument('--config',type=Path,help='JSON training defaults; explicit CLI flags take precedence')
    parser.add_argument('--capture-dir',type=Path,help='Optional rendered PNG sequence; view mode only')
    parser.add_argument('--camera',choices=['chase','overview'],default='chase')
    parser.add_argument('--stop-on-clean-development',action='store_true',help='Orderly training stop after a complete clean original-route quality evaluation')
    parser.add_argument('--quality-selection',action='store_true',help='Select full original routes by measured completion and clean driving')
    parser.add_argument('--quality-telemetry',action=argparse.BooleanOptionalAction,default=False,help='Record passive physics-tick driving metrics')
    parser.add_argument('--sensor-overlay',action='store_true',help='Display actual decision ray samples; view mode only')
    try: parser.set_defaults(**accepted_view_defaults(sys.argv[1:]))
    except (ValueError,KeyError,OSError) as error: parser.error(str(error))
    pre,_=parser.parse_known_args()
    if pre.config:
        values=json.loads(pre.config.read_text())
        allowed={a.dest for a in parser._actions}-{'mode','config','help'}
        if set(values)-allowed: parser.error('Unknown config keys: '+str(set(values)-allowed))
        parser.set_defaults(**values)
    args=parser.parse_args()
    if (args.capture_dir or args.sensor_overlay) and args.mode!='view': parser.error('Capture options require view mode')
    for key in ('max_transitions','learning_rate','reward_scale','initial_alpha','tau','batch_size','replay_capacity',
                'learning_starts','update_every','updates_per_cycle','eval_frequency','eval_episodes',
                'checkpoint_frequency','log_frequency','time_scale'):
        if getattr(args,key)<=0: parser.error(f'{key} must be positive')
    if not 0<=args.gamma<=1 or not 0<args.tau<=1 or args.max_seconds<0: parser.error('Invalid discount/tau/wall cap')
    if args.learning_starts>args.replay_capacity: parser.error('learning-starts exceeds replay capacity')
    if not 5<=args.spawn_min<=args.spawn_max<=50 or not 0<=args.original_fraction<=1: parser.error('Invalid curriculum')
    if args.track_layout=='compact' and (args.track_mode!='procedural' or args.track_segments!=5 or args.track_radius_min<22 or args.track_angle_min>35 or args.track_angle_max<100):
        parser.error('Compact layout requires procedural mode, five turns, radius-min >=22 and angle range covering 35..100')
    if args.track_mode=='procedural':
        train_seeds=args.track_train_seeds or [args.track_seed]
        eval_seeds=args.track_eval_seeds or [args.track_seed]
        if any(not isinstance(s,int) or not 0<=s<=16777215 for s in train_seeds+eval_seeds) or max(len(train_seeds),len(eval_seeds))>64:
            parser.error('Invalid exact float-safe track seeds')
        if args.mode=='train' and set(train_seeds)&set(eval_seeds):
            parser.error('Procedural training requires disjoint --track-train-seeds and --track-eval-seeds')
        if args.approaches: parser.error('Approach evaluation applies only to the fixed track')
        if not 0<=args.track_spawn_seed<=16777215 or not 10<=args.spawn_min<=args.spawn_max<=20 or not 10<=args.track_eval_spawn_approach<=20:
            parser.error('Invalid procedural spawn seed/approach range')
        if args.track_straight_min < max(args.spawn_max,args.track_eval_spawn_approach)+4:
            parser.error('Procedural straight-min must leave 4m cap clearance before every near-turn start')
        try: unity.procedural_tasks(vars(args),evaluation=True)
        except ValueError as error: parser.error(str(error))
        args.spawn_curriculum=False
    if args.stop_on_clean_development and not args.quality_selection:
        parser.error('stop-on-clean-development requires quality-selection')
    if args.quality_selection:
        if args.track_mode!='procedural' or args.track_eval_spawn_indices!=[-1]:
            parser.error('Quality selection requires explicit procedural original-start evaluation indices [-1]')
    if args.mode!='train' and args.checkpoint is None: parser.error('evaluate/view requires --checkpoint')
    config=vars(args).copy()
    for key in ('resume','checkpoint','warm_start','from_scratch','approaches','config','capture_dir','sensor_overlay'): config.pop(key)
    config.update(output=str(args.output or ROOT/'runs/sac'/time.strftime(args.mode+'-%Y%m%d-%H%M%S')),
                  editor=False,worker_id=0,timeout=60,render=args.mode=='view')
    config['player_args']=[]
    if args.capture_dir:
        args.capture_dir.mkdir(parents=True,exist_ok=False)
        config['player_args']+=['--portfolio-capture',str(args.capture_dir.resolve())]
    if args.sensor_overlay: config['player_args']+=['--portfolio-rays']
    if args.mode=='view': config['player_args']+=['--portfolio-camera',args.camera,'--portfolio-label','SAC / '+str(args.checkpoint.stem if args.checkpoint else 'policy'),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0']
    def stop(signum,frame): unity.STOP=True
    signal.signal(signal.SIGINT,stop); signal.signal(signal.SIGTERM,stop)
    if args.mode=='train': train(config,args.resume,None if args.from_scratch else args.warm_start)
    else:
        torch.set_num_threads(1); learner=SAC(config); data=load(args.checkpoint,learner)
        out=Path(config['output']); out.mkdir(parents=True,exist_ok=False); env=None
        (out/'config.json').write_text(json.dumps(dict(config,checkpoint=str(args.checkpoint.resolve()),
                                                      checkpoint_sha256=file_sha(args.checkpoint)),indent=2))
        try:
            env=unity.connect(config,out/'unity.log',viewer=args.mode=='view')
            result=evaluate(env,learner.actor,config,out,data['state']['transitions'],args.eval_episodes,args.approaches)
            (out/'result.json').write_text(json.dumps(result,indent=2))
        except unity.StopRequested: print('Evaluation stopped.',flush=True)
        finally:
            if env is not None: env.close()


if __name__=='__main__': main()
