# Arcade quality and procedural showcase

## Scope and ownership

Implement the approved procedural quality gate, bright green grass, a refined low-poly formula racer, chase and fixed whole-route footage, and accepted procedural defaults. Preserve historical policies, evidence, media and the archive. The feature branch is `codex/arcade-quality-showcase`, based on merged main `52394f4`. A draft PR is authorized; merging requires a separate request.

Specialists use GPT-6.1 Sol with low reasoning effort, as requested. They share this checkout with exclusive ownership:

- Parent: this plan, decisions, evidence review and consequential approvals.
- Environment: RacingAgent, DrivingQuality, the approved manual initialization in TrainingMode, train_sac.py and unity_env.py. No presentation edits.
- Aesthetics: Ground material and presentation code. No dynamics, colliders, wheel positions, sensors or scenes.
- Testing: tests, configurations, quality tools/results, validation ledger, Unity builds, experiments and captures.
- Refiner: README, historical/editorial documentation, figures and media encoding.
- Operator: Git/GitHub operations and docs/coordination/quality-publication.md.

Five roles are staged under the four-slot concurrency limit. Exchanges use compact findings and file references.

## Frozen acceptance protocol

1. Evaluate the existing procedural actor on eight original-start development routes, seeds 20000–20007. If all finish cleanly, skip training.
2. If needed, transfer the current actor trunk/mean into fresh SAC state. Train on 32 seeds, 10000–10031, with 75% near-turn starts (10–20 m before turns) and 25% original starts. Evaluate every 10,000 transitions. Stop at development acceptance, 200,000 additional transitions, or 1,800 combined session seconds including evaluations. Stage at 100,000, then resume only if needed. Allow orderly shutdown separately; no extra training budget.
3. Rank full original routes by completion, clean-driving fraction, then time only when the same complete task set finishes. Near-turn suffix probes are diagnostic only.
4. Freeze the actor before one untouched test on seeds 30000–30039. Require at least 38/40 full finishes. Never use these outcomes for selection or retraining in this phase.
5. Require clean full runs on five routes chosen from geometry before driving: 20001, 20003, 20007, 20002 and 20005. Require exact headless/chase/overview trace parity and exported-action agreement.

Clean means Finish, off-asphalt physics-time fraction at most 5%, reverse span at most 0.5 s (signed forward speed below -0.5 m/s), and stall span at most 2 s (speed below 0.5 m/s), excluding the first 2 simulated seconds only from stall measurement. Off asphalt means lateral distance exceeds half the road width. Telemetry adds no actor inputs or simulation behavior.

## Presentation and rollout

The overview camera fits complete road bounds with a 10% margin and remains fixed within each episode. Both modes have minimal, unclipped labels. Media records policy, build, geometry, spawn, camera, timing and outcomes. GIF excerpts, acquisition gaps, terminal holds and reset trimming are disclosed; full recorded MP4s accompany previews.

Only after acceptance, bare Python view selects the hash-verified procedural actor on seed 1009 from the original start. Native manual play uses the same route; `--racing-manual --racing-fixed` retains historical keyboard play. Disconnected Agent mode idles, so autonomous viewing uses Python. Explicit options remain authoritative. Training/evaluation configurations retain explicit historical compatibility.

After acceptance, the main README uses procedural imagery and actual quality graphs; the previous README moves to linked history. On failure, retain the old primary showcase/defaults and provide a reviewable candidate. No historical assets are overwritten.

## Milestones and evidence

- Interfaces, presentation and geometry-only route selection: complete.
- Development baseline: 8/8 clean; additional training skipped.
- Frozen untouched test: 40/40 clean; capture gate: all five routes clean in both cameras with exact traces and exported actions. Parent accepted rollout.
- Publication build: all eight development traces match the gate build exactly; all ten final camera runs are clean with exact parity. The default viewer and explicit seed override finish, native manual/fixed configuration starts correctly, and three historical fixed episodes plus telemetry on/off traces match exactly. All 24 Python tests pass.
- Publication readiness: ten MP4s and ten GIFs decode and pass hash checks; final visuals and scope audit are accepted. The operator publishes an attached draft PR and records status in its ledger. Merging remains separate.

See decisions.md, validation.md, results/quality/ and the operator ledger for final hashes and publication status.
