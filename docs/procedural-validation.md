# Procedural validation and training evidence

The seeded near-turn curriculum completed one bounded SAC pilot. The selected new actor finishes all 18 recorded validation tasks, including all three original-route starts. These tasks were reused for checkpoint selection; they are not an untouched final test suite. The historical fixed actor, evidence and media remain preserved.

## Protocol

Training uses geometry seeds **101, 211, 307**, independent spawn seed **7**, and learner/Unity seed **7**. On each route, 75% of starts sample 10–20 m before a genuine turn entry, covering left/right and early/interior/late turns; 25% use the original start. Spawn selection is deterministic from track seed, spawn seed and episode index, independently of geometry rejection attempts. Actual pose/tangent, selected turn, approach distance, active gate suffix, remaining task distance, geometry hash and task hash are logged per episode. Midroute resets start with zero reward progress and zero validated gates; earlier gates confer no skipped-distance credit.

Validation uses geometry seeds **1009, 2003, 3001**, crossed with explicit spawn indices **−1 (original), 0–4 (turn entries)** at a fixed **15 m approach**: 18 tasks per batch. Original-route completion is reported separately from shorter suffix completion. Policies rank by completion fraction, then normalized remaining-task progress; estimated time breaks ties only when every identical task finishes. Raw return does not rank policies across route lengths. A true `Finish` has normalized progress 1 because the front trigger can precede the car centre reaching the nominal finish position.

[Protocol manifest](../configs/procedural/protocol.json), [smoke configuration](../configs/procedural/curriculum-smoke.json), [pilot configuration](../configs/procedural/curriculum-pilot.json), [track interface](procedural-tracks.md).

## Acceptance evidence

| Check | Evidence |
| --- | --- |
| Python | 18 tests pass: SAC probability/updates, terminal alignment and bootstrapping, replay/RNG restore, fixed approach regression, explicit seed × spawn scheduling, procedural evaluation bounds, resume environment guards, ranking fairness, rollout/telemetry audits |
| Generator | 1,000 seed pairs reproduce identical valid geometry; road/sensor/crash ribbons and ray hits align; gates, cap clearance, open-route progress and same-count cache invalidation pass |
| Near-turn starts | Every representative turn at 10/15/20 m lies on the centreline; zero skipped credit and correct remaining gate suffix; deterministic spawn identity |
| Sampled coverage | 400 starts per training seed: near-turn fractions 75.00%, 75.75%, 75.00%; original and every turn index, both directions, reached |
| Playmode diagnostics | Exact midroute physics spawn, signed progress/time reward and retrace, held-action/velocity/steering reset, Crash, 750-interval NoProgress termination, 6-interval diagnostic Timeout interruption, physical suffix finish with one bonus before cap, premature finish rejection |
| Fixed compatibility | Final curriculum player reproduces complete historical original-start observations/actions/rewards/final state exactly; approach starts retain 18.6/18.8/19.0 s results |
| Numerical reproducibility | All 18 initial curriculum rollouts match across independent simulator launches, including observations, actions, rewards, final states and interruption flags |
| Training/resume smoke | 128 transitions / 100 updates / 1 training episode; then 64 additional transitions / 164 cumulative updates / 2 cumulative episodes; separate output directory and checkpoint restoration |
| New inference export | 1,000 synthetic observations: deterministic action difference 0; fresh 18-task inference-export rollouts exactly equal selected full-checkpoint evaluation |

[Geometry](../results/procedural/curriculum-generation-verification.json), [spawn coverage](../results/procedural/curriculum-spawn-coverage.json), [playmode](../results/procedural/curriculum-episode-verification.json), [fixed comparison](../results/procedural/curriculum-fixed-comparison.json), [smoke](../results/procedural/curriculum-training-smoke.json), [reproducibility](../results/procedural/curriculum-reproducibility.json), [fresh export verification](../results/procedural/selected-fresh-verification.json).

The timeout diagnostic temporarily shortened the threshold to 0.12 s and restored it without saving a scene. Scripted diagnostic driving is not learned-policy evidence. Unity Editor QuickSearch emitted an unrelated startup indexing exception; diagnostic assertions and process exit succeeded. An editor-only coverage patch initially had two local variable scope collisions; these were fixed and the verifier rerun successfully. Production source hashes remained unchanged.

## Pilot and selection

The historical selected mean advanced farther than the historical initial mean on matched original procedural starts: **38.53% versus 12.27%** normalized progress, with **0/3 finishes for both**. Parent approved transferring the selected trunk/mean, with fresh critics, optimizers, replay and standard deviation 0.3. [Pre-curriculum selected](../results/procedural/selected-heldout-telemetry.json), [initial mean](../results/procedural/initial-mean-heldout-telemetry.json). This is transfer learning, not training from scratch or resuming the historical SAC state.

The single revised pilot consumed **20,000 additional training transitions**, **18,004 update cycles**, **232 completed training episodes**, and **168.01 wall seconds including startup and evaluations**. Its budget was 20,000 transitions or 900 wall seconds, with an external SIGINT watchdog covering final evaluation as well as training. The run completed normally; the watchdog did not fire. No old original-only pilot had started when the user clarified near-turn spawning. Earlier original-only smoke remains separately labeled and preserved.

| Policy snapshot | Validation tasks finished | Original routes finished | Mean finished task time |
| --- | --- | --- | --- |
| Initial selected-mean transfer | 6 / 18 | 0 / 3 | 7.767 s among only the six shorter completed tasks |
| **Selected at 12,210 additional transitions** | **18 / 18** | **3 / 3** | **10.761 s across the full task set** |
| Final at 20,000 additional transitions | 18 / 18 | 3 / 3 | 11.128 s across the full task set |

Initial timing is not comparable to the completed full task set. Selected original-route times are **16.4 / 16.9 / 15.8 s** for seeds 1009 / 2003 / 3001; final times are **16.8 / 18.0 / 16.2 s**. The 12,210 checkpoint wins the actual same-task ranking; higher training return is not the selection criterion.

[Task-level initial/selected/final evidence](../results/procedural/pilot-summary.json), [evaluation CSV](../results/procedural/pilot-evaluations.csv), [wall budget](../results/procedural/pilot-wall.json), [checkpoint provenance](../policies/procedural-provenance.json), [build and production source hashes](../results/procedural/curriculum-build-provenance.json). The new inference actor is `policies/procedural-selected.pt`; the historical `policies/selected.pt` remains unchanged. Full resume checkpoints, replay, players, meshes and bulk logs stay in ignored `runs/`.

## Timing and real capture

Estimated episode time is delivered transition count × 0.1 s, from 0.02 s physics and five-tick decisions. The final interval can overestimate by approximately 0.08 s. Structured telemetry also records actual physics ticks and simulated seconds. Training transitions are counted separately from evaluation transitions; the external session wall measurement includes both.

The fixed comparison uses actual separate captures of the historical initial mean and historical selected actor: same player, fixed seed 7, original start, chase camera and time scale 1. Initial mean fails at an estimated 8.7 s; selected finishes at 20.0 s. Source mean conversion is verified on 1,000 synthetic and 87 actual captured observations with action difference 0. [Conversion](../results/procedural/initial-mean-viewer-conversion.json), [capture build](../results/procedural/fixed-capture-provenance.json), [selected rendered parity](../results/procedural/rendered-fixed-comparison.json).

The new selected actor's actual seed-1009 original-route playback finishes at 16.4 s and matches the headless full numerical trace exactly. Initial visual inspection found the road beyond the fixed ground plane. A rendered-only collider-free backdrop corrected it; final actual playback still finishes 16.4 s and matches every headless numerical array exactly. The final frame was inspected: continuous dark ground, no blue void/tile boundary, readable label and clear car/curbs. [Final rendered comparison](../results/procedural/final-rendered-parity.json), [capture build and protocol](../results/procedural/final-capture-build-provenance.json). The pilot build remains separately identified; no retraining occurred. Capture does not add observations, change policy actions or alter physics.

## Limits

One training RNG seed, three training geometries, three validation geometries and one actor family were tested. Validation starts and geometry were reused for checkpoint selection; no independent final track suite, independent training seeds, perturbation study, clean-machine dependency installation or other platform was tested. These results establish performance on the recorded tasks, not broad unseen-track robustness or an optimal racing line. The earlier source baseline and selected fixed checkpoint are preserved; archive inspection remained targeted and read-only, with the existing Python executable reused.
