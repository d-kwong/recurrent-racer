"""Extract one actual episode and exact observation callback timeline; never infer offsets."""
import argparse
import csv
import json
from pathlib import Path
import numpy as np


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--poses',type=Path,required=True)
    p.add_argument('--trace',type=Path,required=True)
    p.add_argument('--seed',type=int,required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args(); a.output.mkdir(parents=True,exist_ok=True)
    with a.poses.open() as f:
        reader=csv.DictReader(f); fields=reader.fieldnames; rows=list(reader)
    episodes=[];current=[]
    for row in rows:
        if int(row['tick'])==0: current=[]
        current.append(row)
        if row['terminal_reason']!='None':
            if int(row['seed'])==a.seed: episodes.append(current)
            current=[]
    if len(episodes)!=1: raise ValueError(f'Expected one actual target episode, got {len(episodes)}')
    episode=episodes[0]
    if [int(r['tick']) for r in episode]!=list(range(len(episode))): raise ValueError('Incomplete physics pose ticks')
    if len({r['geometry_sha256'] for r in episode})!=1: raise ValueError('Geometry changed inside episode')
    with (a.output/'poses.csv').open('w') as f:
        writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(episode)
    with np.load(a.trace) as data:
        observations=data['episode_0_observations'];terminal=data['episode_0_terminal_observation']
    raw_observations=Path(str(a.poses)+'.observations.csv')
    with raw_observations.open() as f: records=[r for r in csv.DictReader(f) if int(r['seed'])==a.seed]
    expected=np.concatenate([observations,terminal[None,:]])
    samples=np.array([[float(r[f'observation_{j}']) for j in range(15)] for r in records],dtype=np.float32)
    starts=[i for i in range(len(samples)-len(expected)+1) if np.array_equal(samples[i:i+len(expected)],expected)]
    if len(starts)!=1: raise ValueError(f'Expected one exact contiguous observation sequence, got {len(starts)}')
    chosen=records[starts[0]:starts[0]+len(observations)]
    with (a.output/'decision-timeline.csv').open('w') as f:
        writer=csv.DictWriter(f,fieldnames=['observation_index','physics_ticks','episode_seconds','sample_index'])
        writer.writeheader()
        for index,row in enumerate(chosen): writer.writerow(dict(observation_index=index,**{k:row[k] for k in ['physics_ticks','episode_seconds','sample_index']}))
    (a.output/'telemetry-audit.json').write_text(json.dumps(dict(seed=a.seed,physics_rows=len(episode),observations=len(observations),terminal_reason=episode[-1]['terminal_reason'],terminal_seconds=float(episode[-1]['simulated_seconds']),exact_observation_callback_match=True,geometry_sha256=episode[0]['geometry_sha256']),indent=2)+'\n')
    print(len(episode),'physics poses;',len(observations),'exact decision timestamps')


if __name__=='__main__':main()
