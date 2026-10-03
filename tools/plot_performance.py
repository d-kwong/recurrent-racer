"""Render preserved fixed-track evaluations; never substitute training returns."""
import csv
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
BACKGROUND, TEXT, MUTED = "#101820", "#edf3f5", "#a7b6be"
CYAN, GOLD = "#6ed4cc", "#f2bd71"


def main():
    rows = []
    for run in ("lap-v1", "lap-v2"):
        with (ROOT / f"results/selection/{run}-evaluations.csv").open() as source:
            rows.extend(csv.DictReader(source))
    rows.sort(key=lambda row: int(row["step"]))
    steps = np.array([int(row["step"]) for row in rows])
    finish = np.array([float(row["reward_inferred_finish_fraction"]) for row in rows])
    times = np.array([float(row["mean_finished_seconds"]) if row["mean_finished_seconds"] else np.nan for row in rows])
    provenance = json.loads((ROOT / "policies/provenance.json").read_text())
    selected_step = provenance["state"]["transitions"]
    selected_index = list(steps).index(selected_step)

    plt.rcParams.update({
        "font.family": "DejaVu Sans", "font.size": 11, "svg.fonttype": "none",
        "text.color": TEXT, "axes.labelcolor": MUTED, "xtick.color": MUTED,
        "ytick.color": MUTED, "axes.edgecolor": "#35434d", "axes.facecolor": BACKGROUND,
        "figure.facecolor": BACKGROUND, "savefig.facecolor": BACKGROUND,
    })
    fig, axes = plt.subplots(1, 2, figsize=(11, 4.8))
    x = steps / 1000
    left, right = axes
    left.plot(x, times, "o-", color=CYAN, linewidth=1.6, markersize=5)
    left.scatter(x[selected_index], times[selected_index], color=GOLD, s=70, zorder=3)
    left.annotate(f"Selected · {times[selected_index]:.1f} s", xy=(x[selected_index], times[selected_index]),
                  xytext=(x[selected_index] + 8, times[selected_index] - 0.2), color=GOLD, fontsize=10)
    left.set(title="Finished-lap time", ylabel="Estimated seconds", ylim=(19.3, 23.1), yticks=[20, 21, 22, 23])
    right.scatter(x, finish * 100, color=CYAN, s=32)
    right.scatter(x[selected_index], finish[selected_index] * 100, color=GOLD, s=70, zorder=3)
    right.set(title="Finish proxy", ylabel="Repeated episodes finished (%)", ylim=(-12, 112), yticks=[0, 50, 100])
    for axis in axes:
        axis.set(xlabel="SAC training transitions (thousands)", xlim=(-4, 115), xticks=[0, 25, 50, 75, 100])
        axis.grid(axis="y", color="#35434d", alpha=0.6, linewidth=0.6)
        axis.set_axisbelow(True)
        axis.spines[["top", "right"]].set_visible(False)
        axis.set_title(axis.get_title(), loc="left", fontweight="bold", pad=14)
        axis.set_title("")
    fig.suptitle("Learning on one fixed track", x=0.065, y=0.97, ha="left", fontsize=19, fontweight="bold")
    fig.text(0.065, 0.89, "Deterministic policy · Unity seed 7 · 3 repeated original starts per checkpoint", color=MUTED, fontsize=10)
    fig.subplots_adjust(left=0.065, right=0.975, top=0.73, bottom=0.27, wspace=0.35)
    fig.text(0.065, 0.12, "Time = decisions × 0.1 s; final interval may overestimate by ≈0.08 s. No time is plotted for failures.", color=MUTED, fontsize=9)
    fig.text(0.065, 0.065, "Finish inferred from terminal reward · 120 s cap · repeated starts measure repeatability, not generalization.", color=MUTED, fontsize=9)
    for extension in ("png", "svg"):
        fig.savefig(ROOT / f"docs/media/performance.{extension}", dpi=160)
    plt.close(fig)
    print(f"Rendered {len(rows)} recorded checkpoint batches; selected step {selected_step}.")


if __name__ == "__main__":
    main()
