"""Shared Unity transport, numerical checks and deterministic rollout storage."""
import csv
from dataclasses import dataclass
from pathlib import Path
import random
import time
import numpy as np
import torch
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicationException, UnityTimeOutException
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
ROOT = Path(__file__).resolve().parents[1]
STOP = False

def finite(value, name):
    valid = torch.isfinite(value).all().item() if isinstance(value, torch.Tensor) else np.isfinite(value).all()
    if not valid:
        raise ValueError(f'Nonfinite {name}')

@torch.no_grad()
def sample_actions(policy, observations, deterministic=False):
    """Sampling explores; tanh(mean) gives a repeatable evaluation controller."""
    distribution = policy.distribution(observations)
    u = distribution.mean if deterministic else distribution.sample()
    actions = torch.tanh(u)
    finite(actions, 'actions')
    assert actions.shape == (observations.shape[0], 2)
    return u.detach(), actions.detach()

@dataclass
class Episode:
    observations: np.ndarray  # [T,15], only states where an action was sent
    pre_tanh: np.ndarray      # [T,2], fixed Gaussian samples
    actions: np.ndarray       # [T,2], bounded commands actually sent
    rewards: np.ndarray       # [T], reward delivered AFTER each corresponding action
    terminal_observation: np.ndarray  # [15], no invented terminal action
    agent_id: int
    interrupted: bool

    @property
    def return_(self):
        return float(self.rewards.sum(dtype=np.float64))

class StopRequested(Exception):
    pass

def connect(config, log_path, viewer=False):
    engine = EngineConfigurationChannel()
    engine.set_configuration_parameters(time_scale=1 if viewer else config['time_scale'],
                                         target_frame_rate=60 if viewer else -1)
    rendered = viewer or config['render']
    worker = config['worker_id']
    print(f"Connecting {'Editor' if config['editor'] else config['env']} "
          f"port={config['port']} worker={worker}, rendered={rendered}", flush=True)
    parameters = EnvironmentParametersChannel()
    env = UnityEnvironment(file_name=None if config['editor'] else config['env'],
        seed=config['seed'], base_port=config['port'], worker_id=worker,
        timeout_wait=config['timeout'], no_graphics=not rendered, side_channels=[engine, parameters],
        additional_args=None if config['editor'] else
            ([] if rendered else ['--racing-no-render']) + config.get('player_args',[]) + ['-logFile', str(log_path.resolve())])
    env.racing_parameters = parameters
    return env

def configure_spawn(env, config, evaluation=False):
    """Send before reset; never change episode pose during an active rollout.

    Training mixes uniform approach distances with original starts. Evaluation
    defaults to the original start, so best scores share the original task.
    """
    near = config.get('spawn_curriculum', False) and not evaluation
    channel = env.racing_parameters
    channel.set_float_parameter('racing_spawn_min', config.get('spawn_min', 10) if near else 0)
    channel.set_float_parameter('racing_spawn_max', config.get('spawn_max', 20) if near else 0)
    channel.set_float_parameter('racing_original_fraction', config.get('original_fraction', .25) if near else 0)

def runtime_behavior(env):
    specs = env.behavior_specs
    assert len(specs) == 1, specs
    key = next(iter(specs))
    spec = specs[key]
    assert key.split('?')[0] == 'RecurrentRacer'
    assert len(spec.observation_specs) == 1 and spec.observation_specs[0].shape == (15,)
    assert spec.action_spec.continuous_size == 2 and spec.action_spec.discrete_size == 0
    return key

def collect_episodes(env, policy, count, counters, deterministic=False, on_episode=None):
    """Complete episodes under ONE fixed policy; handle terminals before resets.

    A decision's reward belongs to its PREVIOUS action, not its next action.
    Initial reset reward is zero and has no preceding action. IDs are looked up
    from every API delivery; a terminal removes that episode even if ID is reused.
    """
    env.reset()
    key = runtime_behavior(env)
    active, completed = {}, []
    while len(completed) < count:
        if STOP:
            raise StopRequested('Incomplete batch abandoned before update')
        decisions, terminals = env.get_steps(key)
        for steps in (decisions, terminals):
            assert steps.obs[0].dtype == np.float32
            assert steps.obs[0].shape == (len(steps), 15)
            finite(steps.obs[0], 'observations'); finite(steps.reward, 'rewards')
            assert np.all(np.abs(steps.obs[0]) <= 1.00001), 'Observation outside normalized range'
        assert len(decisions) <= 1 and len(terminals) <= 1, 'Expected one car'
        for row, agent in enumerate(terminals.agent_id):
            agent = int(agent)
            assert agent in active, 'Terminal without a preceding action'
            data = active.pop(agent)
            data['rewards'].append(float(terminals.reward[row]))
            counters['transitions'] += 1
            episode = Episode(*(np.asarray(data[k], dtype=np.float32) for k in
                ('observations', 'pre_tanh', 'actions', 'rewards')),
                terminals.obs[0][row].copy(), agent, bool(terminals.interrupted[row]))
            assert len(episode.rewards) == len(episode.actions)
            completed.append(episode)
            if on_episode:
                on_episode(episode, len(completed))
        # Do not start an extra action/episode once the requested batch is complete.
        if len(completed) >= count:
            break
        for row, agent in enumerate(decisions.agent_id):
            agent = int(agent)
            if agent in active:
                active[agent]['rewards'].append(float(decisions.reward[row]))
                counters['transitions'] += 1
            else:
                assert abs(float(decisions.reward[row])) < 1e-6, 'Unexpected reward at spawn'
                active[agent] = {k: [] for k in ('observations', 'pre_tanh', 'actions', 'rewards')}
            assert len(active[agent]['actions']) < 1300, 'Episode exceeded documented timeout'
        if len(decisions):
            observations = torch.from_numpy(decisions.obs[0].copy())
            u, actions = sample_actions(policy, observations, deterministic)
            for row, agent in enumerate(decisions.agent_id):
                data = active[int(agent)]
                for name, value in [('observations', observations[row]),
                                    ('pre_tanh', u[row]), ('actions', actions[row])]:
                    data[name].append(value.numpy().copy())
            # Rows are exactly the order in returned DecisionSteps, never sorted IDs.
            env.set_actions(key, ActionTuple(continuous=actions.numpy()))
        env.step()  # Empty decision sets require no action submission.
    return completed

@torch.no_grad()
def evaluate(env, policy, count, on_episode=None):
    """Separate deterministic rollouts, never placed in a training loss/buffer."""
    start = time.monotonic()
    counters = {'transitions': 0}
    episodes = collect_episodes(env, policy, count, counters, True, on_episode)
    return episodes, time.monotonic()-start, counters['transitions']

def rng_state():
    n = np.random.get_state()
    return {'python': random.getstate(), 'torch': torch.get_rng_state(),
            'numpy': [n[0], n[1].tolist(), n[2], n[3], n[4]]}

def save_trajectories(path, episodes):
    arrays = {}
    for i,e in enumerate(episodes):
        for name in ('observations','pre_tanh','actions','rewards','terminal_observation'):
            arrays[f'episode_{i}_{name}'] = getattr(e,name)
        arrays[f'episode_{i}_boundary'] = np.array([e.agent_id, int(e.interrupted)])
    np.savez_compressed(path, **arrays)

def append_csv(path, row):
    fresh = not path.exists()
    with path.open('a', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(row))
        if fresh: writer.writeheader()
        writer.writerow(row)
