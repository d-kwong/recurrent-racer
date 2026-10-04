"""Compose actual ray gameplay beside validated offline actor activations.

Run render_actor_activations.py first. This compositor requires exact same-tick
ray samples and never infers decision times from nominal control frequency.
Raw PNG output belongs in ignored runs/, not public media.
"""
import argparse
import bisect
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image
import numpy as np


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('frames', type=Path)
    parser.add_argument('activations', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--node', required=True)
    parser.add_argument('--sharp', required=True, help='Installed sharp module directory')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    metadata = json.loads((args.activations/'activations.json').read_text())
    records = metadata['records']; ticks = [record['physics_ticks'] for record in records]
    if any(b <= a for a, b in zip(ticks, ticks[1:])):
        raise ValueError('Decision ticks must strictly increase within one episode')
    ray_path = args.frames/'ray-samples.csv'
    samples = {}
    previous_tick = -1
    with ray_path.open() as source:
        for row in csv.DictReader(source):
            tick = int(row['physics_ticks']); ray = int(row['ray'])
            if tick < previous_tick:
                break  # Automatic episode reset; retain original raw CSV untouched.
            previous_tick = tick
            if ray in samples.setdefault(tick, {}):
                raise ValueError('Duplicate ray tick; isolate the first episode before composing')
            samples[tick][ray] = float(row['normalized_distance'])
    for record in records:
        sample = samples.get(record['physics_ticks'], {})
        if set(sample) != set(range(11)):
            raise ValueError(f"Missing same-tick actual rays at {record['physics_ticks']}")
        if not np.array_equal(np.array([sample[i] for i in range(11)], dtype=np.float32),
                              np.array(record['layers'][0][:11], dtype=np.float32)):
            raise ValueError('Recorded actual ray samples differ from actor inputs')
    # Batch rasterization, no dependency installation or historical rendering imports.
    script = "const fs=require('fs'),path=require('path'),sharp=require(process.argv[1]);const folder=process.argv[2];(async()=>{for(const name of fs.readdirSync(folder).filter(x=>/^activation-[0-9]+\\.svg$/.test(x))){await sharp(path.join(folder,name)).png().toFile(path.join(folder,name.replace(/svg$/,'png')))}})().catch(e=>{console.error(e);process.exit(1)});"
    subprocess.run([args.node, '-e', script, args.sharp, str(args.activations.resolve())], check=True)
    frames = list(csv.DictReader((args.frames/'frames.csv').open()))
    reset = next((i for i in range(1, len(frames)) if
                  float(frames[i]['episode_seconds']) < float(frames[i-1]['episode_seconds'])), len(frames))
    excluded_reset_frames = len(frames)-reset
    frames = frames[:reset]
    mapping = []
    kept = []
    for frame in frames:
        tick = int(frame['physics_ticks']); decision = bisect.bisect_right(ticks, tick)-1
        if decision < 0:
            continue  # No invented activation before the first recorded decision.
        index = int(frame['frame'])
        with Image.open(args.frames/f'frame-{index:05d}.png') as source:
            game = source.convert('RGB'); game.thumbnail((1040, 585), Image.Resampling.LANCZOS)
        with Image.open(args.activations/f'activation-{decision:05d}.png') as source:
            network = source.convert('RGB')
        canvas = Image.new('RGB', (2080, 720), '#0d1726')
        canvas.paste(game, ((1040-game.width)//2, (720-game.height)//2))
        canvas.paste(network, (1040, 40))
        canvas.save(args.output/f'frame-{index:05d}.png')
        mapping.append({'frame': index, 'physics_ticks': tick,
                        'decision_index': decision, 'decision_physics_ticks': ticks[decision]})
        kept.append(frame)
    if not kept:
        raise ValueError('No frames with actual recorded activations')
    with (args.output/'frames.csv').open('w') as target:
        writer = csv.DictWriter(target, fieldnames=list(kept[0]));writer.writeheader();writer.writerows(kept)
    manifest = {'policy_sha256': metadata['policy_sha256'],
                'activations_sha256': hashlib.sha256((args.activations/'activations.json').read_bytes()).hexdigest(),
                'ray_samples_sha256': hashlib.sha256(ray_path.read_bytes()).hexdigest(),
                'frame_decision_mapping': mapping,
                'synchronization': 'latest actual recorded decision at or before each frame physics tick; same-tick rays bitwise float32 validated',
                'initial_composed_episode_seconds': float(kept[0]['episode_seconds']),
                'source_reset_frames_excluded': excluded_reset_frames,
                'note': 'Recorded inputs and activations, not an attention or causal explanation.'}
    (args.output/'composition.json').write_text(json.dumps(manifest, indent=2)+'\n')


if __name__ == '__main__':
    main()
