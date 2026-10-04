"""Clean driving must be measured, full-route and ranked independently of returns."""
import copy
import unittest
from types import SimpleNamespace
import train_sac
import unity_env


def ending():
    return dict(seed=20000,spawnTurnIndex=-1,reason='Finish',interrupted=False,physicsTicks=100,
                simulatedSeconds=2.,quality=dict(version='physics-quality-v1',ticks=100,offAsphaltTicks=5,
                offAsphaltFraction=.05,maximumReverseSeconds=.5,maximumStallSeconds=2.,
                firstCornerPassed=True,cleanFinish=True))


class QualityChecks(unittest.TestCase):
    def test_measured_thresholds_missing_and_contradictory_quality(self):
        e=ending();r=train_sac.score_quality([e],[(20000,-1)])
        self.assertEqual(r['clean_finish_fraction'],1.)
        for field,value in [('maximumReverseSeconds',.52),('maximumStallSeconds',2.02),('offAsphaltFraction',.06)]:
            bad=copy.deepcopy(e);bad['quality'].update({field:value,'cleanFinish':False})
            if field=='offAsphaltFraction':bad['quality']['offAsphaltTicks']=6
            self.assertEqual(train_sac.score_quality([bad],[(20000,-1)])['clean_finish_fraction'],0.)
        for mutate in [lambda e:e.pop('quality'),lambda e:e['quality'].update(ticks=99),
                       lambda e:e['quality'].update(cleanFinish=False),lambda e:e['quality'].update(maximumReverseSeconds=float('nan'))]:
            bad=copy.deepcopy(e);mutate(bad)
            with self.assertRaises(RuntimeError):train_sac.score_quality([bad],[(20000,-1)])
        with self.assertRaises(ValueError):train_sac.score_quality([e],[(20000,0)])

    def test_completion_then_cleanliness_then_identical_completed_task_time(self):
        r=dict(quality_selection=True,measured_finish_fraction=1.,clean_finish_fraction=.5,quality_mean_finished_seconds=8.)
        self.assertGreater(train_sac.rank(dict(r,clean_finish_fraction=1.,quality_mean_finished_seconds=80.)),train_sac.rank(r))
        self.assertGreater(train_sac.rank(r),train_sac.rank(dict(r,measured_finish_fraction=.9,clean_finish_fraction=.9)))
        partial=dict(r,measured_finish_fraction=.5)
        self.assertEqual(train_sac.rank(partial),train_sac.rank(dict(partial,quality_mean_finished_seconds=80.)))
        self.assertGreater(train_sac.rank(r),train_sac.rank(dict(r,quality_mean_finished_seconds=9.)))

    def test_quality_is_explicitly_opted_in_and_cleared_on_next_configuration(self):
        class Channel:
            def __init__(self):self.values={}
            def set_float_parameter(self,name,value):self.values[name]=value
        env=SimpleNamespace(racing_parameters=Channel())
        unity_env.configure_track(env,dict(track_mode='fixed',quality_telemetry=True))
        self.assertEqual(env.racing_parameters.values['racing_quality_telemetry'],1)
        unity_env.configure_track(env,dict(track_mode='fixed'))
        self.assertEqual(env.racing_parameters.values['racing_quality_telemetry'],0)

    def test_default_view_requires_accepted_hash_and_honors_explicit_options(self):
        import hashlib,json,tempfile
        from pathlib import Path
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'configs/quality').mkdir(parents=True)
            self.assertEqual(train_sac.accepted_view_defaults(['view'],root),{})
            policy=root/'policy.pt';policy.write_bytes(b'test-policy')
            path=root/'configs/quality/accepted-view.json'
            data=dict(gate_passed=True,checkpoint='policy.pt',checkpoint_sha256=hashlib.sha256(policy.read_bytes()).hexdigest(),track_mode='procedural',track_layout='compact',track_seed=1009,track_segments=5,track_straight_min=25,track_straight_max=55,track_radius_min=22,track_radius_max=42,track_angle_min=25,track_angle_max=100,track_spacing=2)
            path.write_text(json.dumps(data))
            self.assertEqual(train_sac.accepted_view_defaults(['view','--camera','overview'],root)['checkpoint'],policy.resolve())
            self.assertFalse(train_sac.accepted_view_defaults(['view'],root)['quality_telemetry'])
            data['quality_telemetry']=True;path.write_text(json.dumps(data))
            defaults=train_sac.accepted_view_defaults(['view'],root)
            self.assertTrue(defaults['quality_telemetry'])
            self.assertEqual(defaults['track_layout'],'compact')
            # The same argparse action used by main must preserve explicit CLI precedence.
            import argparse
            parser=argparse.ArgumentParser()
            parser.add_argument('--quality-telemetry',action=argparse.BooleanOptionalAction,default=False)
            parser.set_defaults(**defaults)
            self.assertTrue(parser.parse_args([]).quality_telemetry)
            self.assertFalse(parser.parse_args(['--no-quality-telemetry']).quality_telemetry)
            self.assertTrue(parser.parse_args(['--quality-telemetry']).quality_telemetry)
            for seed_args in (['--track-seed','77'],['--track-seed=77']):
                defaults=train_sac.accepted_view_defaults(['view']+seed_args,root)
                self.assertEqual(defaults['checkpoint'],policy.resolve())
                self.assertEqual(defaults['track_mode'],'procedural')
                seed_parser=argparse.ArgumentParser()
                seed_parser.add_argument('--track-seed',type=int,default=101)
                seed_parser.add_argument('--track-eval-seeds',type=int,nargs='+')
                seed_parser.set_defaults(**defaults)
                config=vars(seed_parser.parse_args(seed_args))
                self.assertEqual(unity_env.procedural_tasks(config,evaluation=True),[(77,-1)])
            for flag in ['--checkpoint=x','--track-mode=fixed','--track-layout=open','--config=x']:
                self.assertEqual(train_sac.accepted_view_defaults(['view',flag],root),{})
            self.assertEqual(train_sac.accepted_view_defaults(['view','--track-layout','open'],root),{})
            path.write_text(json.dumps(dict(data,gate_passed=False)))
            self.assertEqual(train_sac.accepted_view_defaults(['view'],root),{})
            path.write_text(json.dumps(data));policy.write_bytes(b'changed')
            with self.assertRaises(ValueError):train_sac.accepted_view_defaults(['view'],root)

    def test_initial_clean_development_stops_before_any_training_action(self):
        import tempfile,json
        from pathlib import Path
        from unittest.mock import patch,MagicMock
        good=dict(quality_selection=True,measured_finish_fraction=1.,clean_finish_fraction=1.,quality_mean_finished_seconds=10.,transitions=80)
        self.assertFalse(train_sac.clean_development_stop({},good))
        self.assertFalse(train_sac.clean_development_stop(dict(stop_on_clean_development=True),dict(good,clean_finish_fraction=.875)))
        self.assertFalse(train_sac.clean_development_stop(dict(stop_on_clean_development=True),dict(good,quality_selection=False)))
        config=dict(initial_alpha=.02,learning_rate=.0003,gamma=.995,reward_scale=10.,tau=.005,target_entropy=-2,
                    batch_size=32,update_every=4,updates_per_cycle=4,env='unused',seed=7,replay_capacity=256,
                    learning_starts=32,max_transitions=100,max_seconds=10,evaluate_initial=True,eval_episodes=1,
                    eval_frequency=50,stop_on_clean_development=True)
        with tempfile.TemporaryDirectory() as directory:
            config['output']=str(Path(directory)/'training')
            env=MagicMock();stream=MagicMock()
            with patch.object(train_sac,'build_manifest',return_value={'sha256':'build'}), \
                 patch.object(train_sac,'save'),patch.object(train_sac,'SummaryWriter'), \
                 patch.object(unity_env,'connect',return_value=env),patch.object(train_sac,'UnityStream',return_value=stream), \
                 patch.object(train_sac,'evaluate',return_value={'0':good}):
                train_sac.train(config)
            stream.act.assert_not_called();stream.reset.assert_not_called();stream.read.assert_not_called()
            env.close.assert_called_once()
            state=json.loads((Path(config['output'])/'final-state.json').read_text())
            self.assertEqual(state['transitions'],0)
            self.assertEqual(state['outcome'],'development_gate_met')

    def test_published_protocol_separates_training_selection_and_untouched_test(self):
        import json
        from pathlib import Path
        root=Path(__file__).resolve().parents[1]
        development=json.loads((root/'configs/quality/development.json').read_text())
        test=json.loads((root/'configs/quality/held-out.json').read_text())
        self.assertEqual(development['track_train_seeds'],list(range(10000,10032)))
        self.assertEqual(development['track_eval_seeds'],list(range(20000,20008)))
        self.assertEqual(test['track_eval_seeds'],list(range(30000,30040)))
        self.assertFalse(set(development['track_train_seeds'])&set(test['track_eval_seeds']))
        self.assertFalse(set(development['track_eval_seeds'])&set(test['track_eval_seeds']))
        self.assertEqual(development['track_eval_spawn_indices'],[-1])
        self.assertEqual(test['track_eval_spawn_indices'],[-1])
        self.assertEqual(development['original_fraction'],.25)
        self.assertEqual(development['eval_frequency'],10000)
