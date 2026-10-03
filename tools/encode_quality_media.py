"""Encode complete captured frame sequences and explicitly timed GIF excerpts."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import shutil
import statistics
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('frames', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--start', type=float, required=True, help='Excerpt episode seconds')
    parser.add_argument('--duration', type=float, default=8)
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--policy-sha256', required=True)
    parser.add_argument('--build-sha256', required=True)
    parser.add_argument('--seed', type=int, required=True)
    parser.add_argument('--camera', choices=['chase', 'overview'], required=True)
    parser.add_argument('--capture-result', type=Path, required=True)
    args = parser.parse_args()
    result = json.loads(args.capture_result.read_text())['0']
    terminal_seconds = result['quality_mean_finished_seconds']
    rows = list(csv.DictReader((args.frames / 'frames.csv').open()))
    if len(rows) < 2:
        raise ValueError('At least two timestamped frames required')
    reset_index = next((i for i in range(1, len(rows)) if
                        float(rows[i]['episode_seconds']) < float(rows[i-1]['episode_seconds'])), len(rows))
    discarded_reset_frames = len(rows) - reset_index
    rows = rows[:reset_index]
    times = [float(row['episode_seconds']) for row in rows]
    deltas = [b-a for a, b in zip(times, times[1:])]
    positive_deltas = [delta for delta in deltas if delta > 0]
    if not positive_deltas:
        raise ValueError('No recorded driving interval')
    cadence = statistics.median(positive_deltas)
    terminal_hold_frames = sum(time == times[-1] for time in times) - 1
    if any(delta == 0 for delta in deltas[:-terminal_hold_frames or None]):
        raise ValueError('Frozen timestamp inside driving interval requires manual audit')
    deltas = [delta if delta > 0 else cadence for delta in deltas]
    if args.duration <= 0 or args.start < times[0] or args.start + args.duration > times[-1]:
        raise ValueError('Excerpt must lie inside recorded episode timestamps')
    paths = [(args.frames / f"frame-{int(row['frame']):05d}.png").resolve() for row in rows]
    if any(not path.is_file() for path in paths):
        raise ValueError('Missing captured frame')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    mp4, gif = args.output.with_suffix('.mp4'), args.output.with_suffix('.gif')
    if mp4.exists() or gif.exists():
        raise FileExistsError('Refusing to overwrite existing media')
    with tempfile.TemporaryDirectory() as directory:
        folder = Path(directory)
        lines = []
        for i, (path, duration) in enumerate(zip(paths, deltas + [cadence])):
            name = f'frame-{i:05d}.png'
            (folder / name).symlink_to(path)
            lines.extend([f"file '{name}'", f'duration {duration:.9f}'])
        lines.append(f"file 'frame-{len(paths)-1:05d}.png'")
        concat = folder / 'frames.txt'
        concat.write_text('\n'.join(lines) + '\n')
        subprocess.run([args.ffmpeg, '-v', 'error', '-n', '-f', 'concat', '-safe', '0',
                        '-i', str(concat), '-fps_mode', 'vfr', '-c:v', 'libx264',
                        '-crf', '22', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(mp4)], check=True)
        subprocess.run([args.ffmpeg, '-v', 'error', '-n', '-ss', str(args.start-times[0]),
                        '-t', str(args.duration), '-i', str(mp4), '-filter_complex',
                        'fps=12,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse',
                        '-loop', '0', str(gif)], check=True)
    shutil.copyfile(args.frames / 'frames.csv', args.output.with_suffix('.frames.csv'))
    args.output.with_suffix('.media.json').write_text(json.dumps({
        'source_frames': args.frames.name, 'frame_count': len(rows),
        'policy_sha256': args.policy_sha256, 'build_sha256': args.build_sha256,
        'seed': args.seed, 'camera': args.camera, 'spawn_turn_index': -1,
        'measured_finish_fraction': result['measured_finish_fraction'],
        'clean_finish_fraction': result['clean_finish_fraction'],
        'terminal_simulated_seconds': terminal_seconds,
        'final_visual_to_terminal_seconds': terminal_seconds - times[-1],
        'full_recorded_interval_seconds': [times[0], times[-1]],
        'initial_visual_acquisition_seconds': times[0],
        'terminal_hold_frames': terminal_hold_frames,
        'terminal_hold_playback_seconds': terminal_hold_frames * cadence,
        'discarded_post_reset_frames': discarded_reset_frames,
        'gif_episode_interval_seconds': [args.start, args.start + args.duration],
        'timing': 'episode_seconds from physics-tick capture telemetry',
        'full_mp4': mp4.name, 'excerpt_gif': gif.name,
        'asset_sha256': {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                         for path in [mp4, gif, args.output.with_suffix('.frames.csv')]},
        'note': 'Run outcome, checkpoint/build identity and camera parity belong to the capture manifest.'
    }, indent=2) + '\n')


if __name__ == '__main__':
    main()
