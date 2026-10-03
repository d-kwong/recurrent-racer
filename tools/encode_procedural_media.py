"""Encode one real procedural rollout: full MP4 and contiguous 12-second GIF.

Requires an external FFmpeg executable. No video frames or results are synthesized.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile

import torch

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('frames', type=Path)
    parser.add_argument('run', type=Path)
    parser.add_argument('--build-provenance', type=Path, required=True)
    parser.add_argument('--ffmpeg', default='ffmpeg')
    args = parser.parse_args()
    config = json.loads((args.run / 'config.json').read_text())
    result = json.loads((args.run / 'result.json').read_text())['0']
    build = json.loads(args.build_provenance.read_text())
    policy = ROOT / 'policies/procedural-selected.pt'
    state = torch.load(policy, map_location='cpu', weights_only=True)['state']
    telemetry = json.loads((args.run / f"track-eval-{state['transitions']:09d}.json").read_text())
    track, episode = telemetry['tracks'][0], telemetry['episodes'][0]
    if episode['seed'] != 1009 or episode['spawnTurnIndex'] != -1:
        raise ValueError('Telemetry must identify seed1009 original start')
    if (config['checkpoint_sha256'] != sha(policy) or config['track_mode'] != 'procedural'
            or config['mode'] != 'view' or config['track_eval_seeds'] != [1009]
            or config['track_eval_spawn_indices'] != [-1]):
        raise ValueError('Capture must use preserved procedural-selected export')
    rows = list(csv.DictReader((args.frames / 'frames.csv').open()))
    times = [float(row['episode_seconds']) for row in rows]
    if not times or any(b <= a for a,b in zip(times,times[1:])):
        raise ValueError('Capture simulation timestamps must increase')
    if times[-1] > result['mean_length'] * 0.1 + 0.1:
        raise ValueError('Capture crosses an episode boundary')
    media = ROOT / 'docs/media'
    with tempfile.TemporaryDirectory() as temporary:
        listing = Path(temporary) / 'frames.txt'
        lines = []
        for index, row in enumerate(rows):
            frame = (args.frames / f"frame-{int(row['frame']):05d}.png").resolve()
            escaped = str(frame).replace("'", "'\\''")
            lines.append("file '" + escaped + "'")
            dt = times[index+1] - times[index] if index+1 < len(rows) else 0.05
            lines.append(f'duration {dt:.8f}')
        lines.append(lines[-2])
        listing.write_text('\n'.join(lines) + '\n')
        base = [args.ffmpeg, '-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(listing)]
        subprocess.run(base + ['-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(media/'procedural.mp4')], check=True)
        subprocess.run(base + ['-t','12','-filter_complex','fps=10,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff:reserve_transparent=0[p];[b][p]paletteuse=dither=bayer:bayer_scale=3','-loop','0',str(media/'procedural.gif')], check=True)
        subprocess.run(base + ['-ss','10.8','-frames:v','1',str(media/'procedural-poster.png')],check=True)
    manifest = dict(source='Actual Unity screenshots; one procedural original-start episode',
        policy='policies/procedural-selected.pt', policy_sha256=sha(policy),
        training_transitions=state['transitions'], build_sha256=build.get('capture_build_sha256', build.get('build_sha256')), pilot_build_sha256=build.get('pilot_build_sha256', build.get('build_sha256')),
        build_provenance=str(args.build_provenance.resolve().relative_to(ROOT)), unity_seed=config['seed'],
        track_seed=1009, start='original centerline s=4m', effective_time_scale=1,
        camera='Chase:35 degrees down,distance22m,FOV48', frame_count=len(rows),
        frames_csv_sha256=sha(args.frames/'frames.csv'),
        source_simulation_interval_seconds=[times[0],times[-1]],
        gif_simulation_interval_seconds=[times[0],min(times[0]+12,times[-1]+0.05)],
        mp4='Entire captured episode,30fps', gif='Contiguous first12 captured seconds,720px10fps',
        editing='Source simulation timestamp spacing retained;1x simulation playback;no invented frames or removed failures',
        result=result, terminal_telemetry=episode, geometry_sha256=track['geometrySha256'], task_sha256=track['taskSha256'], generation_parameters=track['parameters'], actual_spawn_position=track['actualSpawnPosition'], spawn_forward=track['spawnForward'], source_end_note='Capture stops before terminal delivery; finish outcome is independently recorded in runtime telemetry', rendered_parity_evidence='results/procedural/final-rendered-parity.json', files={})
    for name in ('procedural.gif','procedural.mp4','procedural-poster.png'):
        path = media/name
        manifest['files'][name] = dict(bytes=path.stat().st_size,sha256=sha(path))
    (media/'procedural-capture.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps(manifest['files']))


if __name__ == '__main__':
    main()
