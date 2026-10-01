# Checkpoint provenance and exports

The selected policy is an inference-only export of the recorded SAC actor. No policy retraining or weight modification was performed during curation.

| Item | Value |
| --- | --- |
| Historical source | `runs/sac/lap-v2/best.pt` |
| Source size | 714,035 bytes |
| Source SHA-256 | `fc2665086094b05968f0f99f8169579e161e45f4ff4f83ca65877e91594c1257` |
| Public export | `policies/selected.pt`, 25,100 bytes |
| Format | `racer-sac-actor-v1` |
| Training seed | 7 |
| Selection step | 49,232 cumulative training transitions |
| Update cycles / completed training episodes | 47,236 / 426 |
| Ranking | Finish fraction → shorter estimated finished time → raw return |

[Machine-readable provenance](../policies/provenance.json) records export hashes, source hashes, build identity, run counters, the one-million requested budget and 119,798-transition observed session total. The selected actor's step is separate from the session's final stopped state. The selected file was saved during the initial evaluation of the resumed SAC session.

The original artifact also included critics, target networks, optimizer states, RNG metadata and machine-specific configuration. The 25.1 kB public export contains only actor tensors, format and selection step. At this size it can be stored directly in ordinary Git; no large checkpoint host or replay download is required.

## Export parity

Deterministic actions from the original and export matched **exactly**, maximum absolute difference 0, over 600 saved observations plus 1,000 synthetic bounded observations (seed 7). The exported actor then reproduced recorded original-start performance in both a copy of the historical player and the rebuilt source player. See [validation evidence](../results/verification/validation.json).

## Initialization lineage

The historical SAC run copied only the deterministic trunk and mean from a previous controller at source update 75. Its source SHA-256 is `8149e1c626cacbe1938cfb49d1c9d3d95a9a6ec8c844bc86ec361664c5500fd0`. That controller was trained in a historical REINFORCE/value-baseline experiment. The public repository presents SAC only; its weights are preserved as a small `racer-mean-v1` initialization export, `policies/initial-mean.pt`, to make this transfer explicit and reproducible without carrying the earlier learning implementation.

The initial Gaussian std is 0.30. Critic networks, target critics, replay and all optimizers start fresh. Initialization weights are not presented as a SAC-trained policy or a lap-completing expert. A from-scratch experiment is available but has not been validated as producing the selected performance.

## Export and checkpoint use

The documented watching/evaluation commands load the export with `torch.load(..., weights_only=True)` on CPU. `tools/export_policy.py` can export another local full SAC checkpoint to this format and verify deterministic parity. Resumable training state remains in ignored local `runs/` directories. The selected export intentionally cannot resume training.

Build fingerprints exclude only mutable `Contents/ML-Agents/Timers/` files. The original training build identity is recorded in provenance. The curated build differs because of isolated presentation code/package curation; exact trace comparison supports behavior parity for the tested laps, not byte identity or universal equivalence.
