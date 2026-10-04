"""Guard experiment separation and immutable geometry-only preselection."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('preselection', ROOT/'tools/preselect_quality_captures.py')
preselection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preselection)


class CompactProtocolTests(unittest.TestCase):
    def test_seed_sets_and_budget(self):
        development = json.loads((ROOT/'configs/compact/development.json').read_text())
        held_out = json.loads((ROOT/'configs/compact/held-out.json').read_text())
        train = set(development['track_train_seeds'])
        dev = set(development['track_eval_seeds'])
        test = set(held_out['track_eval_seeds'])
        self.assertEqual(train, set(range(40000,40032)))
        self.assertEqual(dev, set(range(50000,50008)))
        self.assertEqual(test, set(range(60000,60040)))
        self.assertFalse(train&dev or train&test or dev&test)
        self.assertEqual(development['track_layout'], 'compact')
        self.assertEqual(development['track_eval_spawn_indices'], [-1])
        self.assertEqual(development['max_seconds'], 3600)
        self.assertEqual(development['eval_frequency'], 10000)
        self.assertTrue(development['quality_selection'])

    def test_selection_ignores_driving_outcomes(self):
        tracks = [dict(seed=50000+i,length=300+i*10,turnAngles=[-25,25,85+i,90,95-i],turnRadii=[22+i]*5,geometrySha256=str(i),finish=False) for i in range(8)]
        initial = preselection.select(tracks)
        for row in tracks:
            row['finish'] = True
            row['cleanFinish'] = True
        self.assertEqual(initial, preselection.select(list(reversed(tracks))))
        self.assertEqual(len(set(initial['selected_seeds'])),5)


if __name__ == '__main__':
    unittest.main()
