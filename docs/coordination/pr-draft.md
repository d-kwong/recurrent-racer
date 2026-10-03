# Publication handoff — approved final capture

Title: **Add seeded tracks with turn-spawn curriculum and arcade presentation**

## Prepared PR body

Procedural mode builds reproducible open roads from varied straights and left/right arcs. One centerline defines road, sensor/collision boundaries, spawn, ordered gates, progress and finish. The seeded curriculum mixes 75% starts 10–20 m before varied turn entries with 25% original starts; midroute starts receive zero skipped gate/progress credit. Geometry, task/pose identities and separate training/validation seeds are recorded. Fixed mode, selected historical weights and numerical evaluation remain preserved.

One bounded pilot completed 20,000 new procedural training transitions in 168.01 session wall seconds. The selected actor at 12,210 new transitions finishes 18/18 validation tasks, including 3/3 original routes, compared with 6/18 shorter suffixes and 0/3 original routes before new training. It transfers the historical fixed actor's trunk/mean, with fresh critics, replay and optimizers. The portable inference export matches every selected-checkpoint validation trace exactly in a fresh run. These three validation geometries are reused for policy selection; this is one training seed and no untouched final test or broad generalization claim. Selected 10.761 s versus final-checkpoint 11.128 s is a mean across the same mixed full/suffix tasks, not lap time.

The README leads with synchronized actual fixed-track controller runs, honest failure/finish and lineage labels, matched starts/cameras/playback, practical GIF and MP4, and capture provenance. Simple dark Matplotlib figures use recorded evaluations. Palette, HUD, low-poly car details and the procedural ground backdrop are presentation-only. Historical media remains preserved. A separate actual procedural seed 1009 original-start demonstration finishes in 16.4 estimated seconds. Its corrected collider-free backdrop capture matches the selected validation trace exactly, and the final poster has passed visual review.

Validation includes 1,000 deterministic geometry seeds; sensor/collision alignment, gate/progress/reset/end diagnostics; fixed original/approach numerical parity; revised 128-transition smoke and resume to 192; 1,000 synthetic export-parity observations; and exact fresh replay of all 18 selected procedural tasks. Raw frames, generated players, bulk logs, replay, full checkpoints and media encoder executables are excluded. The training wrapper enforces the 900-second session ceiling across training and evaluation, with up to 30 seconds separate orderly cleanup.

## Exact refiner deliverables

- `README.md`
- `tools/plot_performance.py`
- `tools/compose_comparison.py`
- `tools/encode_procedural_media.py`
- `docs/media/performance.png`
- `docs/media/performance.svg`
- `docs/media/comparison.gif`
- `docs/media/comparison.mp4`
- `docs/media/comparison-poster.png`
- `docs/media/comparison-capture.json`
- `docs/media/procedural.gif`
- `docs/media/procedural.mp4`
- `docs/media/procedural-poster.png`
- `docs/media/procedural-capture.json`
- `docs/coordination/refiner.md`
- `docs/coordination/pr-draft.md`

Parent approves the full exact path set. Testing owns policy exports/provenance and compact evidence. Keep comparison build/conversion/parity evidence with the fixed media; its earlier capture build remains separate from pilot and corrected procedural capture fingerprints. Operator alone commits, pushes and creates/updates the draft PR, audits the final files and attaches the PR. No license change, merge, force-push or unrelated content.

Publication gate satisfied: corrected media and manifest use final presentation capture build 21d0e5991bf9750c09567dcf63dc4c6c9c96518e86b746dd2f2eb639426033e5; parent accepted exact rendered numerical parity and visual review. Operator completes file/hash/decode audit before publishing the approved scope as a draft PR.
