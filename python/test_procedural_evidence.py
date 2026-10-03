"""Reject contradictory per-episode procedural provenance and termination evidence."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

_spec = importlib.util.spec_from_file_location('audit_procedural_log', Path(__file__).resolve().parents[1]/'tools/audit_procedural_log.py')
audit = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(audit)


class ProceduralEvidence(unittest.TestCase):
    def test_geometry_identity_and_timeout_semantics(self):
        track = dict(generatorVersion='test', seed=101, parameters={}, geometrySha256='abc',
                     acceptedAttempt=0, length=100, gateDistances=[94, 30, 60])
        episode = dict(geometrySha256='abc', reason='Timeout', interrupted=True,
                       finishDistance=90, travelMetres=45)
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'unity.log'
            def write(tracks, end):
                path.write_text('\n'.join(['RACING_TRACK: '+json.dumps(t) for t in tracks]+
                                           ['RACING_EPISODE: '+json.dumps(end)]))
            write([track, track], episode)
            report = audit.audit(path)
            self.assertEqual(len(report['unique_tracks']), 1)
            self.assertEqual(report['episodes'][0]['normalized_progress'], .5)
            write([track], dict(episode, reason='Finish', interrupted=False, travelMetres=88.))
            self.assertEqual(audit.audit(path)['episodes'][0]['normalized_progress'], 1.)
            first = dict(track, taskSha256='start-a', spawnDistance=4., taskDistance=90.)
            second = dict(track, taskSha256='start-b', spawnDistance=40., taskDistance=54.)
            write([first, second], dict(episode, taskSha256='start-a'))
            self.assertEqual(len(audit.audit(path)['unique_tasks']), 2)
            write([first, second], dict(episode, taskSha256='missing-start'))
            with self.assertRaisesRegex(ValueError, 'unrecorded spawn task'):
                audit.audit(path)
            write([track, dict(track, geometrySha256='changed')], episode)
            with self.assertRaisesRegex(ValueError, 'different geometry'):
                audit.audit(path)
            write([track], dict(episode, interrupted=False))
            with self.assertRaisesRegex(ValueError, 'interruption mismatch'):
                audit.audit(path)
            write([track], dict(episode, geometrySha256='missing'))
            with self.assertRaisesRegex(ValueError, 'unrecorded geometry'):
                audit.audit(path)
