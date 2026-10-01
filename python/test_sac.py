"""Meaningful SAC probability, gradients, transition and restoration checks."""
import copy
from pathlib import Path
import tempfile
import unittest
import numpy as np
import torch
from train_sac import Actor, Replay, SAC, UnityStream, bellman_target, save, load, rank, evaluate, build_manifest
from test_fixtures import FakeUnity, steps
import unity_env as unity


def config():
    return dict(initial_alpha=.02,learning_rate=.0003,gamma=.995,reward_scale=10.,tau=.005,
                target_entropy=-2,batch_size=32,update_every=4,updates_per_cycle=4)


def replay():
    result=Replay(80,7)
    rng=np.random.default_rng(9)
    for i in range(100):
        result.add(rng.random(15,dtype=np.float32),rng.uniform(-1,1,2),.01,
                   rng.random(15,dtype=np.float32),float(i%7==0))
    return result


class SACChecks(unittest.TestCase):
    def setUp(self): torch.manual_seed(7); torch.set_num_threads(1)

    def test_density_reparameterization_and_finite_extremes(self):
        actor=Actor(); obs=torch.zeros((100,15)); actions,logp=actor.sample(obs)
        normal=actor.distribution(obs); u=torch.atanh(actions)
        expected=(normal.log_prob(u)-torch.log1p(-actions.square())).sum(-1)
        torch.testing.assert_close(logp,expected,atol=1e-5,rtol=1e-5)
        self.assertTrue(actions.requires_grad); self.assertTrue((actions.abs()<=1).all())
        actions.sum().backward()
        self.assertGreater(float(actor.mean.bias.grad.abs().sum()),0)
        actor.mean.bias.data.fill_(100)
        _,extreme_logp=actor.sample(obs); self.assertTrue(torch.isfinite(extreme_logp).all())

    def test_terminal_mask_and_time_limit_bootstrap(self):
        target=bellman_target(torch.tensor([1.,1.]),torch.tensor([1.,0.]),torch.tensor([4.,4.]),
                             torch.tensor([-2.,-2.]),.5,.1,10.)
        torch.testing.assert_close(target,torch.tensor([10.,12.1]))

    def test_stream_alignment_empty_delivery_ids_and_interruption(self):
        env=FakeUnity(); stream=UnityStream(env); actor=Actor(); records=[]; ends=[]
        for _ in range(5):
            transitions,episodes=stream.read(); records.extend(transitions); ends.extend(episodes)
            if len(ends)==2: break
            stream.act(actor)
        self.assertEqual([t[2] for t in records],[2.,3.,5.])
        self.assertEqual([t[4] for t in records],[0.,1.,0.])
        self.assertEqual([e['length'] for e in ends],[2,1])
        self.assertEqual([e['return_'] for e in ends],[5.,5.])
        self.assertEqual(env.sent,[[4],[4],[9]])

    def test_stream_terminal_and_reset_can_reuse_id(self):
        env=FakeUnity()
        env.deliveries=[(steps([4],[0]),steps([],[],True)),
                        (steps([4],[0]),steps([4],[.1],True)),
                        (steps([],[]),steps([4],[.2],True))]
        stream=UnityStream(env); actor=Actor(); records=[]
        for i in range(3):
            transitions,_=stream.read(); records.extend(transitions)
            if i<2: stream.act(actor)
        np.testing.assert_allclose([t[2] for t in records],[.1,.2])
        self.assertEqual([t[4] for t in records],[1.,1.])

    def test_sac_update_changes_networks_and_target_is_soft_copy(self):
        learner=SAC(config()); before_actor=copy.deepcopy(learner.actor.state_dict())
        before_q=copy.deepcopy(learner.critic.state_dict()); before_target=copy.deepcopy(learner.target.state_dict())
        metrics=learner.update(replay().sample(32))
        self.assertTrue(all(np.isfinite(v) for v in metrics.values()))
        self.assertTrue(any(not torch.equal(v,before_actor[k]) for k,v in learner.actor.state_dict().items()))
        self.assertTrue(any(not torch.equal(v,before_q[k]) for k,v in learner.critic.state_dict().items()))
        self.assertTrue(all(p.grad is None for p in learner.critic.parameters()))
        self.assertTrue(all(p.grad is None for p in learner.target.parameters()))
        for key,value in learner.target.state_dict().items():
            torch.testing.assert_close(value,.995*before_target[key]+.005*learner.critic.state_dict()[key])

    def test_checkpoint_restores_replay_sampling_action_rng_and_next_update(self):
        learner=SAC(config()); buffer=replay(); learner.update(buffer.sample(32))
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'latest.pt'; save(path,learner,buffer,dict(transitions=100),config())
            expected_batch=buffer.sample(32); expected_action=learner.actor.sample(torch.zeros((2,15)))[0]
            metrics=learner.update(buffer.sample(32))
            other=SAC(config()); other_buffer=Replay(80,999)
            data=load(path,other,other_buffer); self.assertEqual(data['state']['transitions'],100)
            for key,value in other_buffer.sample(32).items(): torch.testing.assert_close(value,expected_batch[key])
            torch.testing.assert_close(other.actor.sample(torch.zeros((2,15)))[0],expected_action)
            other_metrics=other.update(other_buffer.sample(32))
            self.assertEqual(metrics,other_metrics)
            for key,value in other.actor.state_dict().items(): torch.testing.assert_close(value,learner.actor.state_dict()[key])
            self.assertFalse(path.with_suffix('.tmp').exists())

    def test_mean_actor_transfer_preserves_deterministic_commands(self):
        source=Actor(); actor=Actor()
        source.log_std.bias.data.fill_(-1.)
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'source.pt'
            torch.save(dict(format='racer-mean-v1',trunk=source.trunk.state_dict(),mean=source.mean.state_dict(),source_version=75),path)
            self.assertEqual(actor.warm_start(path),75)
        obs=torch.randn((10,15))
        torch.testing.assert_close(torch.tanh(actor.distribution(obs).mean),torch.tanh(source.distribution(obs).mean))
        torch.testing.assert_close(actor.log_std.bias,torch.full((2,),np.log(.3),dtype=torch.float32))

    def test_evaluation_preserves_actor_and_action_rng(self):
        actor=Actor(); before=copy.deepcopy(actor.state_dict()); rng=torch.get_rng_state().clone()
        class Parameters:
            def set_float_parameter(self,key,value): pass
        env=FakeUnity(); env.racing_parameters=Parameters()
        with tempfile.TemporaryDirectory() as folder:
            results=evaluate(env,actor,{},Path(folder),0,2)
        self.assertEqual(results['0']['transitions'],3)
        torch.testing.assert_close(torch.get_rng_state(),rng)
        for key,value in actor.state_dict().items(): torch.testing.assert_close(value,before[key])
        self.assertTrue(all(p.grad is None for p in actor.parameters()))

    def test_selection_prioritizes_laps_then_reliability_then_speed(self):
        partial=dict(reward_inferred_finish_fraction=0,mean_finished_seconds=None,mean_return=3.)
        lap=dict(reward_inferred_finish_fraction=1,mean_finished_seconds=40.,mean_return=2.)
        fast=dict(lap,mean_finished_seconds=30.)
        unreliable=dict(lap,reward_inferred_finish_fraction=.5,mean_finished_seconds=20.)
        self.assertGreater(rank(lap),rank(partial)); self.assertGreater(rank(fast),rank(lap))
        self.assertGreater(rank(lap),rank(unreliable))

    def test_build_identity_excludes_only_runtime_timer_artifacts(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder); data=root/'Contents/Resources/gameplay.dll'
            timer=root/'Contents/ML-Agents/Timers/runtime.json'
            data.parent.mkdir(parents=True); timer.parent.mkdir(parents=True)
            data.write_bytes(b'original'); timer.write_text('first run')
            a=build_manifest(root); timer.write_text('second run')
            b=build_manifest(root); self.assertEqual(a['sha256'],b['sha256'])
            self.assertNotEqual(a['runtime_artifacts'],b['runtime_artifacts'])
            data.write_bytes(b'changed'); self.assertNotEqual(a['sha256'],build_manifest(root)['sha256'])


if __name__=='__main__': unittest.main(verbosity=2)
