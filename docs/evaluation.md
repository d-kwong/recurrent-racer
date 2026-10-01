# Evaluation protocol and evidence

The showcased actor is `policies/selected.pt`, exported from `runs/sac/lap-v2/best.pt` at **49,232 cumulative SAC training transitions**. It is the selected fastest reliable original-start checkpoint among the recorded periodic evaluations, not a claim of a globally best policy or best algorithm.

![Measured evaluation performance](media/performance.png)

## Protocol

A single car drives the same fixed track. Evaluation uses `tanh(mean)` with exploration disabled, Unity seed 7, three repeated episodes per start, a 120 s cap, 50 Hz physics and 10 Hz decisions. Headless verification uses time scale 20; recorded playback uses time scale 1. It explicitly disables random curriculum starts. No held-out track is involved.

Finish is a **reward-inferred proxy**: a non-interrupted terminal delivery above 0.5 identifies the +1 finish bonus under the current reward contract. It is not inferred from total return. Maximum ordinary step progress/time delivery is well below that threshold with this vehicle/rate. There is no fabricated crash classification or exact end-reason channel. Estimated time is transition count × 0.1 s; the last interval may be shorter, producing up to about 0.08 s overestimate.

## Fresh verification of the selected actor

The original-player copy and newly built curated player returned identical results:

| Start | Finish proxy | Estimated time, mean [min, max] | Raw return, mean |
| --- | --- | --- | --- |
| Original | 3 / 3 | 20.0 [20.0, 20.0] s | 5.884618813 |
| 10 m before first corner | 3 / 3 | 18.6 [18.6, 18.6] s | 5.494760140 |
| 15 m before first corner | 3 / 3 | 18.8 [18.8, 18.8] s | 5.539016095 |
| 20 m before first corner | 3 / 3 | 19.0 [19.0, 19.0] s | 5.593471167 |

All estimated times within each batch are identical (standard deviation zero). Original, 10 m and 20 m returns are also identical; the 15 m batch has a tiny numerical return spread of 5.5390160916–5.5390161019 (population standard deviation 4.8842 × 10⁻⁹). These are repeated fixed starts, not independent stochastic trials; no confidence interval or robustness claim is justified. Approach starts shorten the required distance and cannot be compared as equivalent full laps.

## Checkpoint selection

[Preserved comparison CSVs](../results/selection) contain the periodic original-start evaluations from the two SAC sessions. At 20,066 transitions, the recorded policy first finished in 22.0 s. At 30,071 it took 20.7 s. The selected 49,232 actor took 20.0 s. Later recorded actors took 20.1–22.5 s. For example, the 69,453 actor had higher return (5.88744) but took 20.1 s, so it did not outrank the selected actor. Each recorded batch contains three repeat episodes, not three training seeds.

A one-million-transition budget was requested. The run was interrupted after 119,798 cumulative training transitions. The selected checkpoint contains 47,236 SAC update cycles and 426 completed training episodes in its saved state. The initial periodic evaluation and requested budget should not be confused with the checkpoint's consumed training budget. No result is claimed for unrecorded future training or the final interrupted actor.

## Commands and source records

```sh
.venv/bin/python python/train_sac.py evaluate \
  --checkpoint policies/selected.pt --approaches --eval-episodes 3 \
  --seed 7 --output runs/selected-evaluation
```

Use a new output directory for each run. `evaluations.csv` contains aggregate metrics; `evaluation-episodes.csv` contains each return, length, estimated time and finish proxy; compressed NPZ files retain aligned observations/actions/rewards and the final observation.

- [Historical selected original-start trace](../results/selection/selected-start0.npz).
- [Original-player fresh aggregate results](../results/verification/original-player-result.json).
- [Curated-player fresh aggregate results](../results/verification/curated-headless-result.json) and [per-episode evidence](../results/verification/curated-headless-evaluation-episodes.csv).
- [Presentation contract comparison](../results/verification/contract-comparison.json) and [recorded playback traces](../results/capture).

Known gaps: one training seed, one fixed track, one controller family, no held-out geometry, no deliberate noise/friction/speed perturbations, and no exact telemetry lap timer. The finish/time surrogate favors completed fast laps, but does not prove the fastest racing line.
