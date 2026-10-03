# Editorial and media handoff — 2026-10-03

Refiner owns README, performance chart generator/output and this handoff. No remote mutations or checkpoint/environment edits.

## Completed independent pass

- README opens with the concrete perception/control task, navigation and actual historical gameplay. Compact baseline table keeps completion, estimated time and training selection step; detailed protocol/provenance remain linked.
- Historical hero/perception media preserved and linked. The new actual synchronized comparison leads the README; its selected run matches the historical numerical trace exactly.
- `tools/plot_performance.py` now renders two simple dark Matplotlib panels with shared transition axes: finished-lap time and finish proxy. Twelve recorded checkpoint batches from the preserved selection CSVs are used; failures have no invented times. Selected step comes from `policies/provenance.json`. Approach starts are excluded from this chart because they shorten the route; their recorded data remains documented in `docs/evaluation.md`.
- `docs/media/performance.{png,svg}` regenerated. PNG is 110,894 bytes (was 208,306); typography, units, timing caveat and one-track protocol are consistent. Visual inspection confirms labels, selected marker and footnotes fit without clipping.
- README links the testing owner's fresh clean-source baseline comparison: exact observation/action/reward/final-state equality, 3/3 finish proxies and 20.0 s. This statement is about the preserved fixed source baseline, not unverified integrated procedural behavior.

Validation: chart command `.venv/bin/python tools/plot_performance.py` rendered 12 batches, selected step 49,232. Existing dependency environment was reused read-only; no archive files changed. No environment tests needed for these presentation-only chart/README changes. `git diff --check` is run on the integrated diff before publication.

## Comparison capture contract

Agreed with testing/training: included initial mean vs preserved selected vs optional pilot. Use fixed Unity seed 7, original start, same camera, time scale 1 and cap. Capture separate actual runs and composite using recorded simulated time, not frame index or encoder wall time. Labels must identify distinct lineage: historical initial mean is not a SAC checkpoint. Keep contiguous failures, identify hashes and source intervals, hold terminated panels visibly with outcome/time while others continue. No intermediate archive checkpoint search required.

Testing owns player execution/raw frames and manifest evidence. Refiner may encode comparison only after accepted build/smoke and comparable frames exist. Output practical GIF plus higher-quality MP4. If capture is unavailable, preserve historical media and record the reason; no simulated substitute or unsupported demonstration.

## Publication gate

README procedural usage/claims and PR body await the testing owner's integrated evidence. Parent reviews exact final diff and ledger. Operator alone owns commits, pushes and PRs. Publish only compact evidence/media, never raw frames, full traces, replay or generated players. Preserve existing selected actor and historical records. Final title/body/files will be supplied here after evidence review.

## Actual comparison completed

- `tools/compose_comparison.py` renders two real fixed-track captures on a common simulated-time axis (0.24–20.0 s, plus one-second final hold). Both use Unity seed 7, original start, identical chase camera and effective time scale 1. The stored trainer config has time_scale 20 and curriculum enabled, but viewer/evaluation explicitly override these; the metadata records this distinction.
- Historical initial mean: did not finish, estimated 8.7 s. Selected SAC at 49,232 transitions: finish proxy, estimated 20.0 s, exact historical numerical parity. No crash reason invented; no intermediate SAC progression implied. The compositor replaces the generic source title (which incorrectly called initial mean SAC) with lineage labels and overlays status on source HUD areas.
- Shared timestamps select the previous real screenshot; no position/frame interpolation or accelerated driving. Ended panels visibly hold their last captured frame. Metadata records hashes, counts, source intervals, protocol, outcomes and the immutable actual capture build fingerprint. Initial-mean source export and converted viewer-actor formats/hashes are distinguished and linked to 1,000 synthetic plus 87 actual-observation parity checks (all max difference zero).
- New deliverables: `docs/media/comparison.gif`, `.mp4`, `-poster.png`, `-capture.json`. GIF is approximately 4.5 MB at 960px / 10fps; MP4 approximately 1 MB at 1280px / 30fps with repeated source frames. Poster at common simulated time 10 s shows the failure held while selected continues.
- Encoding uses externally supplied FFmpeg. A temporary isolated media dependency was installed outside the repository; no executable, new asset pipeline or archive dependency changes are published. Visual QA covered poster labels, synchronized outcomes, visible wing/sidepods and preserved wheels.
- Procedural README evidence is explicitly the original-start pre-curriculum transfer probe. Revised near-turn spawn evidence and final pilot/publication draft remain pending.

## Revised curriculum documentation

README now documents the independent spawn seed,75% near-turn10–20m /25%original mix and zero skipped progress/gate credit. Original-only evaluation explicitly uses spawn index−1; default18tasks include original+five15m turn approaches on each of three validation seeds. Before new training,6/18suffix finishes are distinguished from0/3original routes. These tasks select policies and are not untouched final tests. Procedural training instructions export the preserved selected mean with the existing tool, use fresh SAC state, and link the20k/900s operational config. Revised smoke/resume supports infrastructure only. Pilot evidence remains pending; PR draft includes exact refiner files and publication gate.

## Final pilot evidence and provisional procedural media

Fresh selected export verification is recorded in `policies/procedural-provenance.json` and `results/procedural/selected-fresh-verification.json`: selected at12,210 **new procedural** transitions after fixed49,232-transition trunk/mean transfer,18/18validation tasks including3/3original routes. One pilot completed20,000new transitions in168.01external session wall seconds. Final20k actor also finished18/18 but had slower matched mixed-task mean;10.761s vs11.128s is not lap time. README preserves that distinction and states one training seed/three validation geometries reused for selection.

Bounded training instructions now use `tools/run_bounded_training.py` so the900s ceiling includes evaluation; up to30s orderly cleanup is explicitly separate. Title emphasizes the user-requested turn-spawn curriculum.

`tools/encode_procedural_media.py` encodes actual seed1009original capture into a contiguous12s720pxGIF, full captured1280pxMP4 and turn poster, with policy/build/geometry/task/pose identities and runtime finish telemetry. Current provisional output is about2.7MBGIF/1.1MBMP4. Source capture ends slightly before terminal delivery, so metadata records the finish independently; no terminal frame is manufactured.

**Do not publish provisional procedural media yet:** testing detected the generated route extends beyond the fixed ground plane. Parent/testing are coordinating a collider-free presentation ground extension and corrected capture with separate build provenance. Replace these provisional media entirely after recapture; fixedcomparison remains accepted and unchanged. Policy/validation claims remain supported independently of rendering.

## Parent-approved final corrected capture

The provisional procedural media were fully replaced. Parent accepted the corrected manifest and turn poster after clear-backdrop/car/HUD review; final rendered observations, actions, rewards and terminal state match the selected validation trace exactly. Actual seed1009 original-start result is16.4 estimated decision seconds, with independent runtime Finish telemetry. Capture build `21d0e5991bf9750c09567dcf63dc4c6c9c96518e86b746dd2f2eb639426033e5` is distinct from pilot build `127c413280637dc9e72058ba1331fa9c14087dd7c155038f762f27e1d00157dc`; the later change is the collider-free presentation backdrop. Manifest records the complete distinction and source interval; no terminal frame is manufactured. GIF3,335,103bytes and MP41,159,935bytes. Earlier provisional publication gate is superseded by this acceptance. Operator applies only this parent-approved handoff correction and performs final publication audits.
