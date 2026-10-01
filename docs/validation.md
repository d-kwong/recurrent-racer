# Validation performed

Validation ran on 2026-10-01 on macOS arm64 with the existing verified Python 3.10.12 environment. Source imports resolve from this curated copy; no excluded historical Python module is imported. The original Unity player was copied to a temporary location before execution. The completed source project was imported and rebuilt with Unity 6000.6.3f1.

[Machine-readable report](../results/verification/validation.json) records the specific checks and build identity.

| Check | Result |
| --- | --- |
| Curated SAC tests | 10 passed: density/reparameterization, Bellman masks, transition alignment/ID reuse, Q/actor isolation, targets, checkpoint/RNG/replay restoration, mean transfer, evaluation isolation, ranking and build fingerprint |
| Numerical learner preservation | SAC, replay, collector, targets, ranking and training function ASTs match the final historical trainer |
| Inference export | 1,600 observations; maximum deterministic-action difference 0 |
| Mean initialization export | 1,000 bounded observations; maximum deterministic-action difference 0 |
| Unity source/assets | Fresh source import, compile and native macOS build succeeded; included GUID references resolve |
| Selected-policy evaluation | 12 episodes: three each at original and 10/15/20 m approach starts; all finish proxy true |
| Original-start contract | Complete observation/action/reward/final arrays exactly equal to original-player evaluation |
| Hero and ray playback | One finished lap each; complete traces exactly equal to original-player evaluation |
| Ray snapshot data | All 200 action-observation ray vectors exactly equal to the captured query data |
| Training smoke | 128 transitions, 100 update cycles, completed |
| Resume smoke | 64 additional transitions; cumulative 192 transitions and 164 updates, completed |
| Documentation/media | Relative links resolve; SVG/PNG diagrams inspected; animated GIFs and MP4s inspected |
| Publication content | Focused path/credential-pattern and asset-provenance audit; no detected machine-specific paths or credential patterns in included text |

The training smoke lowered learning-starts to 32, minibatch to 32 and replay capacity to 256, with evaluation/checkpoint intervals of 64. These overrides exercise collection, learning, saving and restoring within a small budget. Smoke performance is not evidence of learned laps and is not included in the performance figure. The selected policy was evaluated without retraining.

The original source files remain in their original project. The publication copy also removes Unity cloud account identifiers, editor-test/missing template volume components and stale probe-debug references; these are unrelated to the environment contract. The final source was rebuilt and reevaluated after this cleanup.

The only modified existing runtime source in this copy is the optional sensor snapshot hook; vehicle, rewards, timing, gate/progress logic and observation/action mapping are byte-identical. An additional isolated presentation component changes the human camera/GUI and draws query snapshots. This is bounded empirical and source-inspection evidence; it does not prove equivalence for every possible state.

The existing Python environment supplied the dependencies. A fresh virtual-environment installation, clean-machine end-to-end setup, other operating systems, sustained retraining, multiple training seeds and held-out tracks were not tested. The macOS build and generated frames stay outside ordinary Git publication content.
