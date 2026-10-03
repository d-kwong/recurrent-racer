# GitHub publication ledger — 2026-10-03

Operator owns Git operations and this ledger. Parent approves exact publication scope after integrated validation; refiner prepares editorial content. No engineering or editorial ownership is transferred to operator.

## Ready

- Working branch: `codex/seeded-tracks-portfolio`, based on `ccb6b29672b9bf91b4c0e92c822b8af36afad4ee`.
- Remote: `https://github.com/d-kwong/recurrent-racer.git`. Read-only remote verification found only `main`, at the same baseline commit; no branch collision.
- Existing macOS keychain Git credential verified through GitHub API in memory. Repository is active and credentials have push permission. No credential values were printed or saved.
- `gh` is unavailable. Git push plus direct GitHub REST draft-PR creation is viable using the existing credential; no CLI installation required.
- Initial changed-path audit: no generated/private/replay paths or changed files over 5 MB. Ignored `runs/`, Unity `Library/` and player builds remain excluded. Final audit follows completed evidence/media and parent approval.
- Initial `git diff --check` passes.

## Publication gate

Await integrated runtime/smoke evidence, final refiner title/body/file handoff, and parent exact-scope approval. No staging, commits, push, or PR creation has occurred.

User authorizes feature branches and draft PRs for this repository. Operator must not merge, force-push, delete branches, alter visibility/license, overwrite baseline checkpoints/results, or include unrelated changes. Attach every created PR through app tooling and record URL, head SHA, check status and concrete blockers here.

Sandboxed network access initially failed DNS; an escalated read-only request succeeded. This is a sandbox constraint, not a repository/authentication blocker.

## Final-content candidate audit (pilot still running)

Read-only inventory contains 74 candidate files. No credential patterns, private absolute paths, generated folders, logs, replay/checkpoint payloads, fonts or executable dependencies were found in the candidate content. Comparison GIF is 4,510,049 bytes; MP4 is 969,964 bytes. Every historical `policies/`, `results/` and `docs/media/` baseline file is byte-identical to `ccb6b29`, except the explicitly authorized regenerated performance chart PNG/SVG. Selected actor SHA256 remains `13363bc38c08bbaea94c0cfa3817f28cf35d00aa42a012e736cb9dc08d37ed04`. No license changes. `git diff --check` passes.

The following is an audited **candidate list**, awaiting parent approval; pilot evidence and final editorial updates may add or revise paths and require a final re-audit. No staging or publication authorized by this ledger alone.

```text
README.md
RacingEnvironment/Assets/Racing/Editor/ProceduralEpisodeVerification.cs
RacingEnvironment/Assets/Racing/Editor/ProceduralEpisodeVerification.cs.meta
RacingEnvironment/Assets/Racing/Editor/ProceduralTrackVerification.cs
RacingEnvironment/Assets/Racing/Editor/ProceduralTrackVerification.cs.meta
RacingEnvironment/Assets/Racing/Materials/Ground.mat
RacingEnvironment/Assets/Racing/Materials/Red.mat
RacingEnvironment/Assets/Racing/Materials/Road.mat
RacingEnvironment/Assets/Racing/Materials/White.mat
RacingEnvironment/Assets/Racing/Scripts/Episode/RaceEpisode.cs
RacingEnvironment/Assets/Racing/Scripts/Presentation/PortfolioPresentation.cs
RacingEnvironment/Assets/Racing/Scripts/RL/RacingAgent.cs
RacingEnvironment/Assets/Racing/Scripts/RL/RouteProgress.cs
RacingEnvironment/Assets/Racing/Scripts/Track/ProceduralTrack.cs
RacingEnvironment/Assets/Racing/Scripts/Track/ProceduralTrack.cs.meta
RacingEnvironment/Assets/Racing/Scripts/Track/RaceTrack.cs
configs/procedural/curriculum-pilot.json
configs/procedural/curriculum-smoke.json
configs/procedural/original-start-smoke.json
configs/procedural/protocol.json
docs/coordination/aesthetics.md
docs/coordination/decisions.md
docs/coordination/plan.md
docs/coordination/pr-draft.md
docs/coordination/publication.md
docs/coordination/refiner.md
docs/media/comparison-capture.json
docs/media/comparison-poster.png
docs/media/comparison.gif
docs/media/comparison.mp4
docs/media/performance.png
docs/media/performance.svg
docs/procedural-tracks.md
docs/procedural-validation.md
python/test_procedural_evidence.py
python/test_rollout_comparison.py
python/test_track_configuration.py
python/train_sac.py
python/unity_env.py
results/procedural/curriculum-build-provenance.json
results/procedural/curriculum-episode-verification.json
results/procedural/curriculum-fixed-comparison.json
results/procedural/curriculum-fixed-result.json
results/procedural/curriculum-generation-verification.json
results/procedural/curriculum-reproducibility.json
results/procedural/curriculum-selected-baseline.json
results/procedural/curriculum-spawn-coverage.json
results/procedural/curriculum-training-smoke.json
results/procedural/fixed-baseline-comparison.json
results/procedural/fixed-baseline-episodes.csv
results/procedural/fixed-baseline-provenance.json
results/procedural/fixed-baseline-result.json
results/procedural/fixed-capture-provenance.json
results/procedural/generation-verification.json
results/procedural/initial-mean-heldout-episodes.csv
results/procedural/initial-mean-heldout-result.json
results/procedural/initial-mean-heldout-telemetry.json
results/procedural/initial-mean-viewer-conversion.json
results/procedural/integrated-build-provenance.json
results/procedural/integrated-fixed-comparison.json
results/procedural/integrated-fixed-episodes.csv
results/procedural/integrated-fixed-result.json
results/procedural/rendered-fixed-comparison.json
results/procedural/reproducibility.json
results/procedural/selected-heldout-episodes.csv
results/procedural/selected-heldout-result.json
results/procedural/selected-heldout-telemetry.json
results/procedural/training-smoke.json
tools/audit_procedural_log.py
tools/compare_rollouts.py
tools/compose_comparison.py
tools/export_mean_initialization.py
tools/plot_performance.py
tools/run_bounded_training.py
```

## Approved final publication inventory

Parent approved final source/config/test/docs/compact-evidence/media scope, plus the25,232-byte portable procedural inference export and provenance. 92 explicit paths below passed final credentials/private-path/generated-content audit. Historical policies/results/media remain byte-identical except approved performance charts. Corrected procedural GIF/MP4 decode without errors, and manifest media hashes/sizes plus policy hash match. Parent accepted final visual review and exact rendered parity. Pending gates above are superseded; draft publication is authorized.

```text
README.md
RacingEnvironment/Assets/Racing/Editor/ProceduralEpisodeVerification.cs
RacingEnvironment/Assets/Racing/Editor/ProceduralEpisodeVerification.cs.meta
RacingEnvironment/Assets/Racing/Editor/ProceduralTrackVerification.cs
RacingEnvironment/Assets/Racing/Editor/ProceduralTrackVerification.cs.meta
RacingEnvironment/Assets/Racing/Materials/Ground.mat
RacingEnvironment/Assets/Racing/Materials/Red.mat
RacingEnvironment/Assets/Racing/Materials/Road.mat
RacingEnvironment/Assets/Racing/Materials/White.mat
RacingEnvironment/Assets/Racing/Scripts/Episode/RaceEpisode.cs
RacingEnvironment/Assets/Racing/Scripts/Presentation/PortfolioPresentation.cs
RacingEnvironment/Assets/Racing/Scripts/RL/RacingAgent.cs
RacingEnvironment/Assets/Racing/Scripts/RL/RouteProgress.cs
RacingEnvironment/Assets/Racing/Scripts/Track/ProceduralTrack.cs
RacingEnvironment/Assets/Racing/Scripts/Track/ProceduralTrack.cs.meta
RacingEnvironment/Assets/Racing/Scripts/Track/RaceTrack.cs
configs/procedural/curriculum-pilot.json
configs/procedural/curriculum-smoke.json
configs/procedural/original-start-smoke.json
configs/procedural/protocol.json
docs/coordination/aesthetics.md
docs/coordination/decisions.md
docs/coordination/plan.md
docs/coordination/pr-draft.md
docs/coordination/publication.md
docs/coordination/refiner.md
docs/media/comparison-capture.json
docs/media/comparison-poster.png
docs/media/comparison.gif
docs/media/comparison.mp4
docs/media/performance.png
docs/media/performance.svg
docs/media/procedural-capture.json
docs/media/procedural-poster.png
docs/media/procedural.gif
docs/media/procedural.mp4
docs/procedural-tracks.md
docs/procedural-validation.md
policies/procedural-provenance.json
policies/procedural-selected.pt
python/test_procedural_evidence.py
python/test_rollout_comparison.py
python/test_track_configuration.py
python/train_sac.py
python/unity_env.py
results/procedural/curriculum-build-provenance.json
results/procedural/curriculum-episode-verification.json
results/procedural/curriculum-fixed-comparison.json
results/procedural/curriculum-fixed-result.json
results/procedural/curriculum-generation-verification.json
results/procedural/curriculum-reproducibility.json
results/procedural/curriculum-selected-baseline.json
results/procedural/curriculum-spawn-coverage.json
results/procedural/curriculum-training-smoke.json
results/procedural/final-capture-build-provenance.json
results/procedural/final-rendered-parity.json
results/procedural/final-rendered-result.json
results/procedural/fixed-baseline-comparison.json
results/procedural/fixed-baseline-episodes.csv
results/procedural/fixed-baseline-provenance.json
results/procedural/fixed-baseline-result.json
results/procedural/fixed-capture-provenance.json
results/procedural/generation-verification.json
results/procedural/initial-mean-heldout-episodes.csv
results/procedural/initial-mean-heldout-result.json
results/procedural/initial-mean-heldout-telemetry.json
results/procedural/initial-mean-viewer-conversion.json
results/procedural/integrated-build-provenance.json
results/procedural/integrated-fixed-comparison.json
results/procedural/integrated-fixed-episodes.csv
results/procedural/integrated-fixed-result.json
results/procedural/pilot-evaluations.csv
results/procedural/pilot-summary.json
results/procedural/pilot-wall.json
results/procedural/procedural-rendered-comparison.json
results/procedural/rendered-fixed-comparison.json
results/procedural/reproducibility.json
results/procedural/selected-fresh-episodes.csv
results/procedural/selected-fresh-telemetry.json
results/procedural/selected-fresh-verification.json
results/procedural/selected-heldout-episodes.csv
results/procedural/selected-heldout-result.json
results/procedural/selected-heldout-telemetry.json
results/procedural/training-smoke.json
tools/audit_procedural_log.py
tools/compare_rollouts.py
tools/compose_comparison.py
tools/encode_procedural_media.py
tools/export_mean_initialization.py
tools/plot_performance.py
tools/run_bounded_training.py
tools/summarize_curriculum_run.py
```
