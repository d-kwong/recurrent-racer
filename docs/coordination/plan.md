# Recurrent Racer continuation

Parent owns milestones, interface approvals, evidence review and this plan. Specialists own implementation and publication. Historical archive is preserved; inspect only specific necessary files read-only. Working checkout is the managed worktree; local main remains untouched.

## Baseline — 2026-10-03

- Local main and managed detached HEAD: ccb6b29672b9bf91b4c0e92c822b8af36afad4ee, clean; origin d-kwong/recurrent-racer.
- No applicable AGENTS.md found in repository or ancestors. README and environment/training/evaluation/checkpoint/capture/reproduction/validation/publication docs read.
- Fresh SAC unit run: 10 tests pass using existing Python 3.10 archive environment (execution only; archive unchanged).
- Selected actor SHA256: 13363bc38c08bbaea94c0cfa3817f28cf35d00aa42a012e736cb9dc08d37ed04.
- Initial mean SHA256: 84d307a3812db50f9c43eb5f7d70538bbaff86fc36a402b35d7dd30067aa512d.
- Preserved recorded fixed-track baseline: original 3/3 finish proxies, 20.0s estimated decision timing. Fresh player/runtime validation pending; no generated player in current checkout.
- Unity 6000.6.3f1 installed. Never overwrite policies/selected.pt, existing results or historical media provenance.

## Phases and acceptance

1. Interface and validation contract: track and testing agree fixed default, explicit procedural mode/seed/parameters, recorded geometry identity, disjoint train/eval seeds. Preserve vehicle, 15 observations, 2 actions and 0.02s/5-tick timing. Parent approves any necessary contract change before implementation.
2. Environment: deterministic open routes with varied straights/left/right arcs; one centerline drives road, curbs, sensor edges, crash boundaries, spawn, ordered gates, progress and finish. Reject overlap/shortcut-prone or degenerate geometry. Tests cover determinism, validity, alignment, reset, rewards, endings and fixed compatibility. Fresh Unity compile/build required.
3. Evidence: bounded fixed/procedural policy baseline, then training smoke/resume before a conservatively budgeted pilot. Default pilot ceiling 20,000 transitions and 15 minutes wall time, whichever first; document initialization choice before run. Held-out completion/time evidence selects policy; no unsupported generalization claim. Any additional sustained budget requires parent review.
4. Presentation: small isolated aesthetics change verified with protected-file diff plus visual check. Refiner simplifies recorded-data graphs to dark Matplotlib, improves hierarchy and documents limitations. Synchronized checkpoint capture only if comparable artifacts/evidence exist; honest failures, practical GIF plus MP4.
5. Publication: parent reviews exact diffs/evidence; refiner prepares title/body and explicit files; operator audits and publishes feature branch/draft PR, attaches PR. Never merge, force-push, delete branches, alter visibility/license or include generated folders/bulk logs/replay/private content.

## Ownership and concurrency

At most 3 specialists active alongside parent (4-slot runtime limit); all five roles are created in stages. Shared checkout with exclusive path ownership; no concurrent Git mutations.

- Track: Track/, RL/RouteProgress.cs, RL/RacingAgent.cs, Episode/ and new procedural editor verification/build integration; python/unity_env.py and python/train_sac.py for control/config only. Own environment docs in docs/procedural-tracks.md. No materials/presentation edits.
- Testing/training: new Python tests/tools/configs, new results/procedural/, docs/procedural-validation.md. Request fixes in owner files; only testing owns Unity build/player execution, coordinated after implementation. No learner/transport edits without ownership handoff.
- Aesthetics: Materials/*.mat and Scripts/Presentation/PortfolioPresentation.cs only, plus docs/coordination/aesthetics.md. No shared scenes, physics or sensor changes.
- Refiner: README.md, tools/plot_performance.py, docs/media/performance.*, docs/coordination/refiner.md and publication draft. Request edits elsewhere from owner.
- Operator: Git operations and docs/coordination/publication.md only after approved scope. No engineering/editorial redesign.
- Parent: docs/coordination/plan.md and decisions.md only. Resolve ownership transfers explicitly.

## Handoffs

Agents send concise findings, file references, concrete blockers and commands/results. No full conversation relay or broad historical reloads. Each owns a durable handoff with checks and uncertainty. Build/training runs use ignored runs/; publish only compact audited evidence.

## Progress

- Clean-source fixed baseline rebuilt with installed Unity. Selected actor: 3/3 finishes, 20.0s, mean return 5.884618813. Full recorded episode arrays exactly match historical selected-start0.npz, max difference 0. Evidence: results/procedural/fixed-baseline-comparison.json and fixed-baseline result/episodes files.
- Environment and curriculum complete:75% seeded10–20m near-turn starts across locations on the same route;25%original.18 explicit seed×spawn validation tasks distinguish3 original starts. Geometry/playmode checks,18 Python tests,1200 sampled-start coverage checks and training/resume smoke pass.
- One20k-transition pilot completed in168.01s. Selected12,210 checkpoint finishes18/18 validation tasks including3/3 original routes. Separate inference export reproduces all18 traces exactly; original selected policy remains unchanged. Evidence and limitations: docs/procedural-validation.md and policies/procedural-provenance.json.
- Materials, low-poly car and HUD accepted; actual synchronized fixed comparison and simpler dark chart verified. Final collider-free procedural backdrop build succeeds; one corrected capture/parity review remains before publication.
- Operator candidate audit passes; final exact inventory, corrected media and refiner handoff await parent approval before draft PR publication. No remote publication yet.

- Published and attached [draft PR #1](https://github.com/d-kwong/recurrent-racer/pull/1) from reviewed implementation head `3a9e5a91cc60568eb14c98521af4098a092af8a9`; GitHub mergeability clean, zero reported check runs/statuses. Selected procedural export finishes18/18 validation tasks including3/3 original routes; selection reused these routes, so no untouched-test/generalization claim. Final corrected actual capture/rendered parity accepted; historical baseline preserved. Documentation publication-status followup advances PR head without engineering changes.
