# Arcade quality validation ledger

Baseline branch: codex/arcade-quality-showcase from main52394f4. Prior policies/results/media and historical archive are preserved. Testing owns Unity execution, experiments and captures; engine/presentation owners edit separately.

## Frozen protocol

- Training geometry seeds10000–10031; independent spawn/learner seed7. 75% approaches10–20m before varied genuine turn entries,25%original. Evaluation/selection: eight original-route starts20000–20007; no suffix tasks in quality ranking.
- Clean finish: actual Finish, maximum consecutive reverse span<=0.5s (signed forward<−0.5m/s), maximum consecutive stall<=2s (speed magnitude<0.5m/s, excluding first2s), off-asphalt fraction<=0.05 (lateral distance>road width/2 at all driving physics ticks). Telemetry samples once per physics interval; no actor fields/rewards/dynamics change.
- Quality selection ranks original-route completion, clean-driving fraction, then time only across fully finished identical tasks. Near-turn suffix diagnostics remain separate.
- Existing procedural actor evaluated first; skip training if eight/eight clean. Otherwise fresh trunk/mean transfer, fresh SAC state;100k then resume200k only if needed, combined training-session1800s incl evaluations, hard SIGINT with orderly cleanup grace separately recorded. Evaluate every10k; stop after development acceptance.
- Capture five development routes preselected from geometry alone by tools/preselect_quality_captures.py, frozen before policy outcomes. No substitutions after failures. Five complete chase/overview episodes with identical seeds/spawns/policy and full trace parity to headless; cameras affect people only.
- Freeze winner policy/hash before exactly one40route untouched test30000–30039 original starts. At least38finishes required. Never select/retrain from test results.

## Status

Completed. The five capture routes were frozen from geometry before policy outcomes. The preserved actor passed development, the one untouched test and both camera captures; no additional training ran. Final publication player and defaults are validated below. Historical archive and policies were preserved.

## Verified driving evidence

The unchanged procedural export `policies/procedural-selected.pt` (SHA256 `55e3c08236ef676bd5938743375cca8032b3d0b53228d76dfee146fcb8d65a21`, historical 12,210 procedural transitions) finished all eight development original routes cleanly. No additional training was needed or run: zero transitions and zero training-session seconds. The initial eight routes averaged 17.3175 actual physics seconds; this is different from the 17.35-second decision-count estimate.

The actor was frozen before the single untouched forty-route evaluation. All forty original routes finished cleanly (mean actual physics time 16.0335 seconds). Every outcome is in `results/quality/held-out.json`; none informed checkpoint selection or subsequent training. All development and test episodes reported zero off-asphalt ticks, reverse spans and qualifying stall spans. These results describe this frozen generated-route distribution and checkpoint, not unrestricted generalization.

The five geometry-preselected original routes finished cleanly in both chase and overview. All ten rendered traces exactly match their headless references, including observations, pre-tanh values, actions, rewards, terminal observation and boundary flags. Recomputing the exported mean actor row by row exactly matches all 6,431 fresh held-out actions and 870 headless capture actions. Evidence: `results/quality/capture-gate.json`. Gate player SHA256: `0d191acfbf7d435279cf5baa796ed3ff40abb70965e9a0b1f6f2401100c85d72`.

Unity helper validation passed passive quality thresholds/ticks/reset/corner/termination checks, nine overview aspect/bounds combinations (all eight bounding corners within the 10% framing margin), and repeated identical-geometry pose/FOV/far-clip restoration after simulated reset camera movement. Player compilation succeeded. The final presentation player and all ten replacement media captures passed. The extended collider-free grass backdrop removes blue outer overview edges without altering gate evidence.


## Final publication validation

Publication player SHA256 `559b3cc5f34710ce67ab7a393a69e54b9ed230c71026e7099adec1fb49126fbe` includes extended green scenery plus accepted native procedural routing. Its eight development traces match the original baseline bit for bit. All five final chase/overview route pairs finish cleanly and match their original headless traces bit for bit. `results/quality/publication-parity.json` records this independently of the untouched gate-build evaluation. `results/quality/capture-manifests.json` records actual generator version/parameters/rejection attempt, road/curb widths, route and task distances, geometry/task hashes, spawn pose, camera pose/configuration, terminal ticks, clean outcomes and acquisition timing. No full-route outcome was replaced or omitted.

The original fixed selected policy hash remains `13363bc38c08bbaea94c0cfa3817f28cf35d00aa42a012e736cb9dc08d37ed04`. Three explicit fixed-mode episodes on the publication build exactly match recorded historical baseline observations, pre-tanh/actions, rewards, terminal observations and boundaries. Passive telemetry enabled/disabled also gives identical arrays on development seed 20001. Evidence: `results/quality/publication-build.json`.

No-checkpoint/no-track viewer startup resolves the accepted preserved procedural actor, seed 1009 original start and passive telemetry; its episode finishes cleanly. An explicit `--track-seed 77` is realized as seed 77 while retaining the accepted actor. Resolver tests also cover explicit checkpoint/mode/config authority and telemetry disable override. Evidence: `results/quality/viewer-default-smoke.json`.

Disconnected native `--racing-manual` startup generates seed 1009 original; adding `--racing-fixed` bypasses generation and retains the fixed scene. These are configuration startup checks without keyboard driving or policy inference. Native Agent mode alone does not load the Python policy. Evidence: `results/quality/native-smoke.json` and the separate fixed numerical regression.

Final Python suite: 24 tests pass, including seed/spawn/task configuration, measured quality validation, original-route ranking, training/test separation, budget/clean-development early stop, gated defaults and explicit seed/telemetry precedence. Unity build compilation and `git diff --check` pass. Geometry/camera/reset diagnostics are recorded above. All started build/player/capture processes have finished or been closed; no training process remains.

Media capture starts at or just after 0.1 simulated seconds; per-frame simulation timestamps define playback. First-episode reset frames are excluded explicitly. The encoder's sidecars report any final-frame-to-terminal gap and actual hold frames; a hold is not fabricated when none was acquired. Raw frames remain in ignored run directories. Numerical traces are preserved locally, and compact public JSON records their exact-equality checks and hashes.

Remaining limits: forty untouched routes are evidence for this frozen generator distribution, not an unlimited guarantee. No additional-training comparison or keyboard-driving validation was performed because the development policy already met the approved gate. The fixed historical files and checkpoint remain reproducible through explicit fixed commands.
