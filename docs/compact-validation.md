# Compact circuit validation ledger

The existing 24 Python tests passed before compact edits using the preserved Python runtime. Unity execution is testing-owned and begins only after source freeze. The preserved actor has now passed the development gate and the one untouched test. No additional training ran; presentation capture auditing is underway.

Frozen protocol: development geometry and original starts 50000–50007; training 40000–40031; untouched test 60000–60039. Geometry-only five-route selection precedes all compact driving outcomes. The first preselected route is also the saved-checkpoint comparison route. Historical routes, checkpoints and media remain preserved.

Current actor runs first on all eight original development starts. Only a failure of eight clean finishes permits fresh SAC training initialized from the saved actor trunk/mean, at most 500,000 additional transitions or 3,600 cumulative session seconds including evaluations. Stages end at 100k, 250k and 500k; evaluations occur every 10k. Freeze the winner before one forty-route test; require at least 38 finishes and five clean preselected demonstrations.

Clean finish uses actual Finish termination, reverse span ≤0.5 seconds, qualifying stall span ≤2 seconds after the first two launch seconds, and off-asphalt physics-tick fraction ≤0.05. No outcome will be inferred from visuals or returns alone.

Numerical actor parity uses exact rowwise observations, pre-tanh means and actions; rendered episode traces must equal headless records. Pose/ray data are passive presentation data, outside actor observations.

Verified so far: compact geometry audit passes on all forty development/training seeds, both directions are represented, and seed 1009 generates successfully. Cheap rejection reordering preserves all eight development hashes and accepted attempts; the forty-seed duplicate audit takes 2.008 seconds and seed 1009 takes 0.022 seconds. All eight historical open geometry hashes match prior frozen evidence. Three successive compact regenerations retain red/white tile parity and collider-free curbs.

The preserved 12,210-transition actor finishes all eight original compact development routes cleanly and was frozen before the single forty-route untouched test. All forty test routes finish cleanly, with zero off-asphalt ticks, reverse spans and qualifying stall spans. Additional training is zero transitions and zero session seconds. Rowwise exported actor evaluation exactly matches all 1,261 development and 6,693 untouched actions and pre-tanh means.

The presentation build reproduces all eight gate-build development traces bit for bit. Geometry-only capture seeds were frozen as 50007, 50001, 50000, 50002 and 50005 before any compact driving outcomes. First ray capture 50007 reproduces its headless trace bit for bit, with 139 exact observed callback timestamps and 694 consecutive physics pose samples including tick zero and terminal.

Final acceptance is complete. All five preselected routes finish cleanly in both cameras, and all ten observation/pre-tanh/action/reward/terminal/boundary arrays exactly equal their original headless records. Actual ray capture also matches. Full capture provenance is in `results/compact/capture-manifests.json`; numerical gate is `results/compact/capture-gate.json`.

Historical checkpoint comparison on the locked route 50007 records 4,006 steps crashing at 6.54 seconds, 8,018 crashing at 5.56 seconds, 12,210 finishing at 13.86 seconds and 16,248 finishing at 15.18 seconds. All original poses/rotations match, each actor was evaluated independently, and rowwise actions/pre-tanh means match exactly. The collider-free replay renders their exact recorded trajectories with terminal holds. It records 481 acquired frames and includes a disclosed two-second final display hold; encoding trims extra endpoint acquisition frames. No checkpoint or route was substituted.

The final publication player compiles successfully. Three explicit fixed episodes, eight explicit open episodes and eight compact development episodes reproduce historical/gate numerical traces exactly. Bare viewer resolves the unchanged hash-verified actor and compact original seed 1009, finishing cleanly in 16.94 physics seconds. Telemetry disabled on that route gives the identical numerical trace. Disconnected manual startup selects compact by default; explicit open and fixed flags preserve their modes. These are startup checks, not keyboard driving tests.

Final Python suite: 26 tests pass. The durable explicit-layout precedence assertion also passes after its narrow addition. Build identity and compatibility evidence are in `results/compact/final-compatibility.json`; native startup evidence is `results/compact/native-smoke.json`. The forty-route untouched test was run exactly once. All started player/build/capture processes are closed; no training process ran.
