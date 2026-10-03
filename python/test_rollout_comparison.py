"""Full contract comparison must catch internal differences hidden by equal returns."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import numpy as np

_spec = importlib.util.spec_from_file_location('compare_rollouts', Path(__file__).resolve().parents[1]/'tools/compare_rollouts.py')
comparison = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(comparison)


class RolloutComparison(unittest.TestCase):
    def test_transport_identity_ignored_but_reward_order_and_interruption_checked(self):
        arrays = {f'episode_0_{field}': np.zeros((2, 15) if field == 'observations' else
                 (2, 2) if field in ('pre_tanh', 'actions') else (15,) if field == 'terminal_observation' else (2,), dtype=np.float32)
                 for field in comparison.FIELDS}
        arrays['episode_0_rewards'] = np.array([.1, .2], dtype=np.float32)
        arrays['episode_0_boundary'] = np.array([4, 0])
        with tempfile.TemporaryDirectory() as folder:
            a, b = Path(folder)/'a.npz', Path(folder)/'b.npz'
            np.savez(a, **arrays)
            arrays['episode_0_boundary'][0] = 99
            np.savez(b, **arrays)
            self.assertTrue(comparison.compare(a, b)['exact_equal'])
            arrays['episode_0_rewards'] = arrays['episode_0_rewards'][::-1]
            np.savez(b, **arrays)
            self.assertFalse(comparison.compare(a, b)['exact_equal'])
            arrays['episode_0_rewards'] = arrays['episode_0_rewards'][::-1]
            arrays['episode_0_boundary'][1] = 1
            np.savez(b, **arrays)
            self.assertFalse(comparison.compare(a, b)['exact_equal'])
