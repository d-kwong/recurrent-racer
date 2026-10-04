"""Render all actor neurons from actual recorded decisions, never synthetic input.

Input JSONL rows: physics_ticks, episode_seconds, observations[15], actions[2].
Actions must match fresh CPU actor inference bit for bit. The recorder must emit
round-trip float32 values and preserve the exact observations used for action.
"""
import argparse
import csv
import hashlib
import html
import json
from pathlib import Path
import torch
import numpy as np


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('checkpoint', type=Path)
    parser.add_argument('decisions', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--decision-timeline', type=Path,
                        help='For NPZ: CSV observation_index,physics_ticks,episode_seconds; exact recorder mapping, no guessed ticks')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    torch.set_num_threads(1)
    data = torch.load(args.checkpoint, map_location='cpu', weights_only=True)
    state = data['actor']
    layers = [torch.nn.Linear(15, 64), torch.nn.Linear(64, 64), torch.nn.Linear(64, 2)]
    for layer, prefix in zip(layers, ['trunk.0', 'trunk.2', 'mean']):
        layer.load_state_dict({name: state[prefix+'.'+name] for name in ['weight', 'bias']})
    links = []
    for layer in layers:
        weights = layer.weight.detach().tolist()
        candidates = [(abs(value), source, target, value)
                      for target, row in enumerate(weights) for source, value in enumerate(row)]
        links.append([{'source': s, 'target': t, 'weight': v}
                      for _, s, t, v in sorted(candidates, key=lambda item: (-item[0], item[1], item[2]))[:24]])
    records = []
    if args.decisions.suffix == '.npz':
        if args.decision_timeline is None:
            raise ValueError('NPZ requires an exact decision-to-physics timeline')
        archive = np.load(args.decisions)
        timeline = list(csv.DictReader(args.decision_timeline.open()))
        if [int(row['observation_index']) for row in timeline] != list(range(len(archive['episode_0_observations']))):
            raise ValueError('Timeline must map every recorded observation once in original order')
        rows = [{'physics_ticks': int(row['physics_ticks']), 'episode_seconds': float(row['episode_seconds']),
                 'observations': archive['episode_0_observations'][i].tolist(),
                 'actions': archive['episode_0_actions'][i].tolist(),
                 'pre_tanh': archive['episode_0_pre_tanh'][i].tolist()}
                for i, row in enumerate(timeline)]
    else:
        rows = [json.loads(line) for line in args.decisions.read_text().splitlines() if line.strip()]
    for row in rows:
        obs = torch.tensor(row['observations'], dtype=torch.float32)
        recorded = torch.tensor(row['actions'], dtype=torch.float32)
        if obs.shape != (15,) or recorded.shape != (2,) or not torch.isfinite(obs).all():
            raise ValueError('Expected finite 15-input / 2-action recorded decision')
        with torch.no_grad():
            h1 = torch.tanh(layers[0](obs.unsqueeze(0))); h2 = torch.tanh(layers[1](h1))
            means = layers[2](h2); actions = torch.tanh(means).squeeze(0)
        if not torch.equal(actions, recorded):
            raise ValueError(f"Recorded action mismatch at tick {row['physics_ticks']}")
        if 'pre_tanh' in row and not torch.equal(means.squeeze(0), torch.tensor(row['pre_tanh'], dtype=torch.float32)):
            raise ValueError('Recorded pre-tanh mean mismatch; deterministic capture required')
        if torch.any(obs.abs() > 1):
            raise ValueError('Inputs outside documented bounded observation contract')
        records.append({'physics_ticks': row['physics_ticks'], 'episode_seconds': row['episode_seconds'],
                        'layers': [obs.tolist(), h1.squeeze(0).tolist(), h2.squeeze(0).tolist(), actions.tolist()],
                        'pre_tanh_means': means.squeeze(0).tolist()})
    if not records:
        raise ValueError('No actual decisions')
    manifest = {'policy_sha256': hashlib.sha256(args.checkpoint.read_bytes()).hexdigest(),
                'decision_sha256': hashlib.sha256(args.decisions.read_bytes()).hexdigest(),
                'timeline_sha256': hashlib.sha256(args.decision_timeline.read_bytes()).hexdigest() if args.decision_timeline else None,
                'action_validation': 'bitwise equal float32 CPU inference at every recorded decision',
                'links': links, 'link_rule': '24 globally strongest absolute weights per layer; source then target tie order',
                'full_connection_counts': [960, 4096, 128], 'records': records}
    (args.output / 'activations.json').write_text(json.dumps(manifest, indent=2)+'\n')
    for index, record in enumerate(records):
        (args.output / f'activation-{index:05d}.svg').write_text(render(record, links))


def render(record, links):
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1040" height="640" viewBox="0 0 1040 640"><rect width="1040" height="640" fill="#0d1726"/>']
    def text(x, y, value, size=16):
        parts.append(f'<text x="{x}" y="{y}" font-family="Arial,Helvetica,sans-serif" font-size="{size}" fill="#eef5fa">{html.escape(value)}</text>')
    text(25, 35, 'Actual deterministic actor activations', 25)
    text(25, 65, f"Physics tick {record['physics_ticks']} · episode {record['episode_seconds']:.2f} s")
    positions = []
    for layer, values in enumerate(record['layers']):
        if layer == 0:
            positions.append([(55+(i%3)*34, 130+(i//3)*66) for i in range(15)])
        elif layer in [1, 2]:
            positions.append([(240+(layer-1)*340+(i%8)*30, 140+(i//8)*43) for i in range(64)])
        else:
            positions.append([(960, 220+i*100) for i in range(2)])
    for layer, connections in enumerate(links):
        for link in connections:
            x1,y1 = positions[layer][link['source']];x2,y2 = positions[layer+1][link['target']]
            color = '#459bea' if link['weight'] >= 0 else '#ee6572'
            parts.append(f'<path d="M{x1} {y1}L{x2} {y2}" stroke="{color}" stroke-width="1" opacity=".22"/>')
    for layer, values in enumerate(record['layers']):
        for i, (value, (x,y)) in enumerate(zip(values, positions[layer])):
            radius=12;clip=f'n{layer}-{i}';height=24*abs(value)
            parts.append(f'<defs><clipPath id="{clip}"><circle cx="{x}" cy="{y}" r="{radius}"/></clipPath></defs><circle cx="{x}" cy="{y}" r="{radius}" fill="#17263a" stroke="#a8bdcf" stroke-width="1"/><rect x="{x-radius}" y="{y+radius-height}" width="24" height="{height}" fill="'+('#459bea' if value >= 0 else '#ee6572')+f'" clip-path="url(#{clip})"/>')
    for x, label in [(25,'15 inputs'),(230,'64 tanh'),(570,'64 tanh'),(890,'2 tanh actions')]:text(x, 105, label)
    for i, name in enumerate(['Steer','Throttle']):
        text(880, 250+i*100, f"{name}: {record['layers'][3][i]:+.3f}", 14)
    text(25, 505, 'Pre-tanh means: '+', '.join(f'{value:+.5f}' for value in record['pre_tanh_means']))
    text(25, 540, 'Blue positive / red negative · fill height = |bounded activation| · every neuron shown')
    text(25, 574, 'Only 24 strongest actual weights per layer shown; links are fixed, not attention or causal importance.')
    text(25, 609, 'Standard-deviation branch is training-only; this animation follows tanh(mean), without sampling.')
    return ''.join(parts)+'</svg>\n'


if __name__ == '__main__':
    main()
