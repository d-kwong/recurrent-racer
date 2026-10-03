# Quality decisions

## Approved constraints

The user approved a low-poly formula racer, bright green grass, chase and fixed whole-route cameras, procedural defaults gated on reliable driving, and a new draft PR. Training is capped at 200,000 additional transitions or 30 combined session minutes including evaluations. No merge or automatic budget extension is authorized. Historical weights, results, media and the archive remain preserved.

Specialists use GPT-6.1 Sol with low reasoning effort (light), as requested. Replacement agents retain ownership and durable handoffs rather than restarting experiments.

## Measurement and selection

Clean driving requires Finish, at most 5% off-asphalt physics time, reverse spans at most 0.5 s below -0.5 m/s signed forward speed, and stall spans at most 2 s below 0.5 m/s magnitude. The first 2 simulated seconds are excluded from stall measurement only. Full-route completion precedes clean fraction; time breaks ties only across the same fully completed task set. Suffix starts remain diagnostic.

The five capture routes were frozen from geometry before driving: 20001, 20003, 20007, 20002, 20005. No substitutions after outcomes.

## Evidence and parent acceptance

The preserved actor finished all eight original-start development routes (20000–20007) cleanly, with mean measured driving time 17.3175 simulated seconds. Per protocol, additional training was skipped: zero transitions and zero training-session seconds. See results/quality/development-baseline.json.

The actor was frozen before a single untouched evaluation of 30000–30039: 40/40 finished cleanly. All five preselected routes finished cleanly in both camera modes. Headless/chase/overview traces matched exactly. Exported actor actions matched 6,431 held-out and 870 capture-reference actions bit-for-bit. Test outcomes were not used for checkpoint selection. Parent accepted the gate and authorized procedural defaults and the primary README rollout. See held-out.json and capture-gate.json.

The final publication build adds expanded collider-free grass and accepted native defaults. Its eight development traces match the gate build exactly. Preserve distinct build identities; do not rerun final-test seeds for selection. Final chase and overview media use the same publication build.

## Launch semantics

Bare Python view uses the accepted, hash-verified actor on procedural seed 1009 from the original start. Explicit checkpoint, track and configuration options remain authoritative. Passive telemetry follows the accepted manifest and can be disabled explicitly.

Native disconnected Agent mode idles; it has no embedded actor. Parent granted environment ownership of the narrow TrainingMode initialization change: `--racing-manual` configures procedural seed 1009 before reset and keyboard input; `--racing-manual --racing-fixed` preserves the historical keyboard game. Explicit environment parameters override native fallbacks. Actor dynamics, observations, actions, rewards, architecture, colliders, wheel locations and sensor geometry remain protected.

## Publication

The prior README and fixed-track evidence move to linked history without asset or policy overwrites. New media discloses actual acquisition gaps, excerpt intervals, terminal holds and reset trimming. The operator audits exact source/config/tests/compact evidence/media/docs, omits raw frames, players, logs, replay and private execution paths, and publishes an attached draft PR. Merging requires a new user request.
