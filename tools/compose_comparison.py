"""Compare two actual fixed-track captures on their shared simulated-time axis.

Requires Pillow, NumPy and an external FFmpeg executable. No encoder is bundled.
Example: python tools/compose_comparison.py --ffmpeg /path/to/ffmpeg
Raw inputs are ignored runs/capture-{initial-mean,selected}-fixed{,-frames}.
"""
import argparse
import bisect
import csv
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def font(size):
    try:
        from matplotlib import font_manager
        return ImageFont.truetype(font_manager.findfont("DejaVu Sans"), size)
    except ImportError:
        return ImageFont.load_default()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", default="ffmpeg")
    args = parser.parse_args()
    panels = []
    for name, label, lineage in (
        ("initial-mean", "Historical initial mean", "Earlier controller lineage; no SAC training"),
        ("selected", "Selected SAC · 49,232 transitions", "Preserved fixed-track selected actor"),
    ):
        run = ROOT / f"runs/capture-{name}-fixed"
        frames = ROOT / f"runs/capture-{name}-fixed-frames"
        config = json.loads((run / "config.json").read_text())
        result = json.loads((run / "result.json").read_text())["0"]
        if config["mode"] != "view" or config["track_mode"] != "fixed" or config["seed"] != 7:
            raise ValueError("Comparison requires fixed-track rendered seed-7 episodes")
        rows = list(csv.DictReader((frames / "frames.csv").open()))
        times = [float(row["episode_seconds"]) for row in rows]
        if not times or any(b <= a for a, b in zip(times, times[1:])):
            raise ValueError("Source simulation timestamps must increase")
        ended = float(result["mean_length"]) * 0.1
        if times[-1] > ended + 0.1:
            raise ValueError("Capture crosses the episode boundary")
        panels.append(dict(name=name, label=label, lineage=lineage, config=config, rows=rows,
                           times=times, frames=frames, result=result, end=ended, cache={}))
    if panels[0]["config"]["env"] != panels[1]["config"]["env"]:
        raise ValueError("Both captures must use the same player")
    start = max(panel["times"][0] for panel in panels)
    end = max(panel["end"] for panel in panels)
    fps = 10
    timeline = np.arange(start, end + 1.0, 1 / fps)
    media = ROOT / "docs/media"
    with tempfile.TemporaryDirectory() as temporary:
        temporary = Path(temporary)
        for number, moment in enumerate(timeline):
            canvas = Image.new("RGB", (1280, 500), "#101820")
            draw = ImageDraw.Draw(canvas)
            draw.text((24, 15), "Same track. Same start. Two controller lineages.", font=font(25), fill="#edf3f5")
            draw.text((24, 53), f"Fixed track · Unity seed 7 · synchronized simulation time {moment:.1f} s", font=font(17), fill="#a7b6be")
            for column, panel in enumerate(panels):
                index = max(0, bisect.bisect_right(panel["times"], moment) - 1)
                if index not in panel["cache"]:
                    panel["cache"].clear()
                    path = panel["frames"] / f"frame-{int(panel['rows'][index]['frame']):05d}.png"
                    with Image.open(path) as image:
                        panel["cache"][index] = image.convert("RGB").resize((640, 360), Image.Resampling.LANCZOS)
                x = column * 640
                canvas.paste(panel["cache"][index], (x, 92))
                # Replace the source title only: the initial mean was mislabeled SAC by the generic viewer.
                draw.rectangle((x, 92, x + 640, 155), fill="#172630")
                draw.text((x + 15, 100), panel["label"], font=font(20), fill="#6ed4cc" if column else "#f2bd71")
                draw.text((x + 15, 128), panel["lineage"], font=font(13), fill="#a7b6be")
                if moment >= panel["end"]:
                    status = "Finish proxy" if panel["result"]["reward_inferred_finish_fraction"] == 1 else "Did not finish"
                    status += f" · {panel['end']:.1f} s · last captured frame held"
                elif moment > panel["times"][-1]:
                    status = "Last captured frame held; episode ending"
                else:
                    status = f"Actual gameplay · source frame {panel['times'][index]:.2f} s"
                draw.rectangle((x, 402, x + 640, 452), fill="#172630")
                draw.text((x + 15, 419), status, font=font(14), fill="#edf3f5")
            draw.text((24, 466), "Separate actual runs · 1× simulation playback · terminal time estimated from decisions · no held-out-track claim", font=font(16), fill="#a7b6be")
            canvas.save(temporary / f"frame-{number:05d}.png")
            if abs(moment - 10) < 0.05:
                canvas.save(media / "comparison-poster.png")
        command = [args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-framerate", str(fps), "-i", str(temporary / "frame-%05d.png")]
        subprocess.run(command + ["-vf", "fps=30", "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(media / "comparison.mp4")], check=True)
        subprocess.run(command + ["-filter_complex", "scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff:reserve_transparent=0[p];[b][p]paletteuse=dither=bayer:bayer_scale=3", "-loop", "0", str(media / "comparison.gif")], check=True)
    build_provenance = json.loads((ROOT / "results/procedural/fixed-capture-provenance.json").read_text())
    conversion = json.loads((ROOT / "results/procedural/initial-mean-viewer-conversion.json").read_text())
    manifest = dict(source="Actual Unity screenshots from separate fixed-track episodes", build_sha256=build_provenance["build_sha256"], unity_version=build_provenance["unity"], build_provenance="results/procedural/fixed-capture-provenance.json", unity_seed=7, track_mode="fixed", start="original", camera="Chase: 35 degrees down, distance 22m, FOV48", effective_time_scale=1,
                    config_note="Viewer overrides stored trainer time_scale20 to effective1; evaluation disables stored curriculum flag",
                    simulation_interval_seconds=[float(start), float(end)], final_hold_seconds=1, playback_speed=1,
                    alignment="Previous captured frame at shared simulated time, 10fps; MP4 duplicates to30fps. No interpolated positions. Last source frame visibly held after terminal.",
                    source_title_edit="Header replaced to correct generic viewer SAC label on historical initial mean; Only title and status overlays replace source HUD areas; driving imagery unchanged", panels=[], files={})
    for panel in panels:
        manifest["panels"].append(dict(label=panel["label"], lineage=panel["lineage"], checkpoint_sha256=panel["config"]["checkpoint_sha256"], source_frames_csv=f"runs/capture-{panel['name']}-fixed-frames/frames.csv", frames_csv_sha256=digest(panel["frames"] / "frames.csv"), frame_count=len(panel["rows"]), source_simulation_interval_seconds=[panel["times"][0],panel["times"][-1]], estimated_episode_seconds=panel["end"], finish_proxy=bool(panel["result"]["reward_inferred_finish_fraction"]), result=panel["result"]))
    manifest["panels"][0].update(source_checkpoint=conversion["source"], source_checkpoint_format=conversion["source_format"], source_checkpoint_sha256=conversion["source_sha256"], actual_capture_actor_format=conversion["viewer_actor_format"], conversion_parity_evidence="results/procedural/initial-mean-viewer-conversion.json")
    manifest["panels"][1].update(source_checkpoint="policies/selected.pt", source_checkpoint_format="racer-sac-actor-v1", source_checkpoint_sha256=panels[1]["config"]["checkpoint_sha256"], exact_trace_evidence="results/procedural/rendered-fixed-comparison.json")
    for name in ("comparison.gif", "comparison.mp4", "comparison-poster.png"):
        path = media / name
        manifest["files"][name] = dict(bytes=path.stat().st_size, sha256=digest(path))
    (media / "comparison-capture.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps(manifest["files"]))


if __name__ == "__main__":
    main()
