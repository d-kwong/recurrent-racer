"""Compare all delivered numerical rollout arrays, excluding transport agent IDs."""
import argparse
import json
from pathlib import Path
import numpy as np

FIELDS = ('observations', 'pre_tanh', 'actions', 'rewards', 'terminal_observation')


def compare(reference, candidate, reference_episode=0, candidate_episode=0):
    records = {}
    with np.load(reference) as left, np.load(candidate) as right:
        for field in FIELDS:
            a = left[f'episode_{reference_episode}_{field}']
            b = right[f'episode_{candidate_episode}_{field}']
            same_shape = a.shape == b.shape
            records[field] = dict(reference_shape=list(a.shape), candidate_shape=list(b.shape),
                                  exact_equal=same_shape and bool(np.array_equal(a, b)),
                                  maximum_absolute_difference=float(np.max(np.abs(a.astype(float)-b.astype(float))))
                                  if same_shape and a.size else None)
        a = left[f'episode_{reference_episode}_boundary']
        b = right[f'episode_{candidate_episode}_boundary']
        records['interrupted'] = dict(exact_equal=bool(a[1] == b[1]), reference=bool(a[1]), candidate=bool(b[1]))
    return dict(reference=str(reference), candidate=str(candidate), fields=records,
                exact_equal=all(row['exact_equal'] for row in records.values()),
                note='Agent IDs excluded: transport identifiers may differ across launches.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('candidate', type=Path)
    parser.add_argument('--candidate-episode', type=int, default=0)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = compare(args.reference, args.candidate, candidate_episode=args.candidate_episode)
    output = json.dumps(report, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output+'\n')
    print(output)
    raise SystemExit(0 if report['exact_equal'] else 1)


if __name__ == '__main__':
    main()
