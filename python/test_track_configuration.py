"""Check explicit fixed approach poses survive the new procedural seed configuration."""
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import numpy as np
import train_sac
import unity_env as unity


class ParameterChannel:
    def __init__(self): self.values = {}
    def set_float_parameter(self, name, value): self.values[name] = value


class TrackConfiguration(unittest.TestCase):
    def test_fixed_approach_evaluations_preserve_explicit_pose(self):
        env = SimpleNamespace(racing_parameters=ParameterChannel())
        observed = []
        episode = unity.Episode(np.zeros((1, 15), np.float32), np.zeros((1, 2), np.float32),
                                np.zeros((1, 2), np.float32), np.array([1.], np.float32),
                                np.zeros(15, np.float32), 1, False)
        def rollout(*args):
            observed.append(dict(env.racing_parameters.values))
            return [episode], .01, 1
        with tempfile.TemporaryDirectory() as folder, patch.object(unity, 'evaluate', rollout):
            train_sac.evaluate(env, None, dict(track_mode='fixed'), Path(folder), 0, 1, approaches=True)
        self.assertEqual([row['racing_spawn_min'] for row in observed], [0, 10, 15, 20])
        self.assertEqual([row['racing_spawn_max'] for row in observed], [0, 10, 15, 20])
        self.assertTrue(all(row['racing_original_fraction']==0 for row in observed))
        self.assertTrue(all(row['racing_track_mode']==0 for row in observed))

    def test_procedural_schedule_switch_restarts_sequence_without_curriculum(self):
        env = SimpleNamespace(racing_parameters=ParameterChannel())
        config = dict(track_mode='procedural', track_train_seeds=[101, 211, 307],
                      track_eval_seeds=[1009, 2003, 3001], spawn_curriculum=True)
        unity.configure_spawn(env, config)
        train = dict(env.racing_parameters.values)
        unity.configure_spawn(env, config, evaluation=True)
        evaluation = dict(env.racing_parameters.values)
        self.assertNotEqual(train['racing_track_sequence_id'], evaluation['racing_track_sequence_id'])
        self.assertEqual([train[f'racing_track_seed_{i}'] for i in range(3)], [101,211,307])
        expected = [(seed, turn) for seed in [1009,2003,3001] for turn in [-1,0,1,2,3,4]]
        self.assertEqual(evaluation['racing_track_seed_count'], 18)
        self.assertEqual([(evaluation[f'racing_track_seed_{i}'],evaluation[f'racing_track_spawn_turn_{i}']) for i in range(18)], expected)
        self.assertEqual(train['racing_track_spawn_mode'], 1)
        self.assertEqual(evaluation['racing_track_spawn_mode'], 2)
        self.assertEqual(train['racing_spawn_max'], 0)
        self.assertEqual(evaluation['racing_spawn_max'], 0)

    def test_reject_unrepresentable_track_seeds(self):
        env = SimpleNamespace(racing_parameters=ParameterChannel())
        for seeds in ([16777216], [-1], [1.5], list(range(65))):
            with self.assertRaises(ValueError):
                unity.configure_track(env, dict(track_mode='procedural', track_train_seeds=seeds))

    def test_partial_route_selection_cannot_reward_easier_finished_subset(self):
        base = dict(track_mode='procedural', reward_inferred_finish_fraction=1/3,
                    normalized_progress=.5, mean_finished_seconds=8., mean_return=100.)
        slower_more_progress = dict(base, normalized_progress=.8, mean_finished_seconds=80., mean_return=1.)
        self.assertGreater(train_sac.rank(slower_more_progress), train_sac.rank(base))
        self.assertEqual(train_sac.rank(base), train_sac.rank(dict(base, mean_finished_seconds=80.)))
        finished = dict(base, reward_inferred_finish_fraction=1., normalized_progress=1.)
        self.assertGreater(train_sac.rank(finished), train_sac.rank(slower_more_progress))
        self.assertGreater(train_sac.rank(finished), train_sac.rank(dict(finished, mean_finished_seconds=9.)))

    def test_resume_rejects_environment_config_changes_before_starting_player(self):
        config = dict(initial_alpha=.02, learning_rate=.0003, gamma=.995, reward_scale=10., tau=.005,
                      target_entropy=-2, batch_size=32, update_every=4, updates_per_cycle=4,
                      env='unused', seed=7, replay_capacity=256, learning_starts=32,
                      spawn_curriculum=False, spawn_min=10, spawn_max=20, original_fraction=.25,
                      track_mode='procedural', track_seed=101, track_train_seeds=[101,211,307],
                      track_eval_seeds=[1009,2003,3001], track_radius_min=22,
                      track_spawn_seed=7, track_eval_spawn_indices=[-1,0,1,2,3,4])
        for key, value in [('track_mode', 'fixed'), ('track_train_seeds', [999]), ('track_radius_min', 23), ('track_spawn_seed', 8), ('track_eval_spawn_indices', [-1,0])]:
            old = dict(config, build_sha256='build')
            changed = dict(config, **{key: value})
            with patch.object(train_sac, 'build_manifest', return_value={'sha256':'build'}), \
                 patch.object(train_sac, 'load', return_value={'state':{}, 'config':old}), \
                 patch.object(unity, 'connect') as connect:
                with self.assertRaisesRegex(ValueError, 'Resume must preserve '+key):
                    train_sac.train(changed, resume=Path('unused.pt'))
                connect.assert_not_called()

    def test_procedural_evaluate_preserves_valid_approach_bounds_and_explicit_tasks(self):
        env = SimpleNamespace(racing_parameters=ParameterChannel(), racing_log_path=Path('unused.log'))
        config = dict(track_mode='procedural', track_segments=2, track_eval_seeds=[1009],
                      track_eval_spawn_indices=[-1,0], spawn_min=10, spawn_max=20, original_fraction=.25)
        episode = unity.Episode(np.zeros((1, 15), np.float32), np.zeros((1, 2), np.float32),
                                np.zeros((1, 2), np.float32), np.array([1.], np.float32),
                                np.zeros(15, np.float32), 1, False)
        observed = []
        def rollout(unused_env, unused_actor, count):
            observed.append(dict(env.racing_parameters.values))
            self.assertEqual(count, 2)
            return [episode,episode], .01, 2
        telemetry = dict(tracks=[], episodes=[dict(seed=1009,spawnTurnIndex=i,reason='Finish',
                                                   travelMetres=98,finishDistance=100) for i in [-1,0]])
        with tempfile.TemporaryDirectory() as folder, patch.object(unity,'evaluate',rollout), \
             patch.object(unity,'track_records',return_value=telemetry):
            result = train_sac.evaluate(env,None,config,Path(folder),0,1)
        self.assertEqual(observed[0]['racing_track_spawn_min'], 10)
        self.assertEqual(observed[0]['racing_track_spawn_max'], 20)
        self.assertEqual(observed[0]['racing_track_spawn_turn_0'], -1)
        self.assertEqual(observed[0]['racing_track_spawn_turn_1'], 0)
        self.assertEqual(result['0']['normalized_progress'], 1.)
