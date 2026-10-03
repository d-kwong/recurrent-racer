"""Export actor trunk/mean for fresh SAC transfer, preserving source checkpoint."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import numpy as np
import torch
sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'python'))
from train_sac import Actor


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Output already exists')
    source = torch.load(args.source, map_location='cpu', weights_only=True)
    if source['format'] not in ('racer-sac-v1', 'racer-sac-actor-v1'):
        parser.error('Expected SAC actor/full checkpoint')
    actor = Actor(); actor.load_state_dict(source['actor'])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    torch.save(dict(format='racer-mean-v1', trunk=actor.trunk.state_dict(), mean=actor.mean.state_dict(),
                    source_version=source['state']['transitions']), args.output)
    transferred = Actor(); transferred.warm_start(args.output)
    obs = torch.from_numpy(np.random.default_rng(7).uniform(-1, 1, (1000, 15)).astype(np.float32))
    with torch.no_grad():
        difference = float((actor.distribution(obs).mean.tanh()-transferred.distribution(obs).mean.tanh()).abs().max())
    if difference != 0:
        raise AssertionError('Mean transfer changed deterministic actions')
    report = dict(source=str(args.source), source_sha256=hashlib.sha256(args.source.read_bytes()).hexdigest(),
                  output_sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(),
                  source_training_transitions=source['state']['transitions'], observations=1000,
                  maximum_deterministic_action_difference=difference,
                  rule='Only trunk and mean; fresh critics/replay/optimizers and standard deviation0.3.')
    args.output.with_suffix('.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__': main()
