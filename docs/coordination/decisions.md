# Decisions and evidence

## Preservation and ownership

Continue in the managed worktree on `codex/seeded-tracks-portfolio`; preserve local main, the historical archive, existing selected policy and recorded fixed-track evidence. Five specialist roles have distinct file ownership and staged execution under the runtime's four-slot limit. Parent reviews scope/evidence; refiner prepares content; operator alone commits, pushes and creates draft PRs. No merge, force push, branch deletion, visibility or license change is authorized.

## Environment contract

Fixed track remains the default. Procedural mode uses seeded open straights/arcs with one centerline defining road, curbs, sensor/collision boundaries, spawn, ordered gates, progress and finish. Preserve vehicle dynamics/hitbox, road width, ray interface, 15 observations, two actions, reward coefficients and 0.02s physics/five-tick decisions. Open progress intentionally omits loop wrapping and the closing chord. Require deterministic bounded rejection, effective parameters, generator/geometry identities and reset cache/collider invalidation.

Original procedural spawn is s=4m; finish is length−6m for cap clearance. User correction established that track variety alone is insufficient: training must encounter turns early. Adopt 75% seeded 10–20m approaches to varied turn entries on the same route, 25% original starts, with independent spawn RNG. Record actual poses, turn geometry, active checkpoint suffix and task identity. Midroute resets credit zero skipped distance/gates; finish-only suffixes are supported. Reject nonfinite/invalid inputs and guard task settings on resume.

## Validation protocol

Training routes: 101/211/307. Validation routes: 1009/2003/3001. RNG/spawn seed: 7. Explicit validation tasks are original start plus 15m approaches to five turns on each route (18 tasks). Separate original-route and suffix completion. These held-out-from-training routes are reused for selection, not an untouched independent final test set.

Rank policies by matched-task completion, normalized progress, then estimated time only when all identical tasks finish. Actual Finish maps progress to one despite front-trigger clearance. Preserve fixed-track ranking. Timing is decision count ×0.1s, with up to about0.08s terminal overestimate; mixed-task means are not lap times.

Fresh baseline and final fixed original traces match historical arrays exactly; approach times remain18.6/18.8/19.0s. Geometry checks cover1000 deterministic seeds, sensor/collider alignment, gates, cache refresh and fixed restoration. Playmode diagnostics cover signed rewards/retrace, cleared controls/motion, crash/no-progress, timeout interruption and scripted ordered finish. Scripted driving is environment verification, not policy evidence.18 Python tests pass.400 spawn draws per training route reach all turns/both directions/original starts; near-turn fractions75.00/75.75/75.00%.

## Training and selection

Pre-curriculum original-start transfer probes: historical selected0/3, mean progress38.53%; initial mean0/3,12.27%. Choose selected trunk/mean transfer, with fresh critics, replay, optimizers and std0.3. Preserve the original49,232-transition actor; new procedural transitions are additional experience, not a from-scratch result or full SAC resume.

Revised smoke:128 transitions100 updates/one training episode; resume192 cumulative164 updates/two episodes. Smoke final0/18 supports infrastructure only. Authorize one pilot capped20,000 training transitions/900s external SIGINT, with up to30s orderly cleanup. No other pilot was started.

Pilot completed20,000 transitions18,004 updates232 episodes in168.01s external wall, watchdog unused. Selected12,210 checkpoint:18/18 tasks,3/3 original routes,10.761s mixed-task mean. Final20,000 checkpoint also18/18 but11.128s; evidence selects earlier checkpoint. Separate `policies/procedural-selected.pt` passes1000-observation export parity and fresh18-task exact trace comparison. No additional training budget is implicit.

## Presentation and publication

Small aesthetics scope: four material colors, restrained HUD, render-only low-poly sidepods/rear wing; existing prefab, wheels, colliders and vehicle code preserved. Actual synchronized fixed comparison identifies earlier initial-mean lineage, preserves its failure and selected finish, and records capture/converted-actor/build provenance. Dark Matplotlib chart uses preserved recorded data.

Procedural capture exposed the old finite ground edge. Approve only a collider-free backdrop over realized route bounds+120m using Ground material; never resize the original Ground/collider. Pilot source/build identity stays recorded separately from final presentation build. One corrected seed1009 original-start recapture verifies appearance and full numerical parity; no retraining.

Operator setup/audit passed, existing credentials verified without printing/saving values. Publication awaits parent review of corrected media and exact final inventory. Publish compact evidence/inference exports/media only; exclude players, raw frames/logs, replay, caches, private paths and executable dependencies.

Final corrected procedural media reviewed: continuous dark backdrop and low-poly car clear in actual turn poster; final rendered trace equals verified headless rollout exactly. Final capture build21d0e5991bf9750c09567dcf63dc4c6c9c96518e86b746dd2f2eb639426033e5 differs from pilot solely in presentation source. Parent approves final scope for operator audit/commit/push/draftPR, conditioned on media hash/decode QA and removal of stale provisional handoff wording. Refiner usage limit interrupted final prose updates after corrected media saved; operator receives exact minimal editorial corrections from parent, no redesign ownership. Existing baselines preserved.
