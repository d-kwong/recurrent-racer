"""Export a local SAC checkpoint to portable inference-only actor tensors."""
import argparse,hashlib,json,sys
from pathlib import Path
import numpy as np
import torch
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'python'))
from train_sac import Actor
p=argparse.ArgumentParser(description=__doc__);p.add_argument('source',type=Path);p.add_argument('output',type=Path);args=p.parse_args()
if args.output.exists():p.error('Output already exists')
d=torch.load(args.source,map_location='cpu',weights_only=True)
if d['format']!='racer-sac-v1':p.error('Expected full SAC checkpoint')
original=Actor();original.load_state_dict(d['actor'])
args.output.parent.mkdir(parents=True,exist_ok=True)
torch.save(dict(format='racer-sac-actor-v1',actor=d['actor'],state={'transitions':d['state']['transitions']}),args.output)
export=Actor();export.load_state_dict(torch.load(args.output,map_location='cpu',weights_only=True)['actor'])
obs=torch.from_numpy(np.random.default_rng(7).uniform(-1,1,(1000,15)).astype(np.float32))
with torch.no_grad():
 a=torch.tanh(original.distribution(obs).mean);b=torch.tanh(export.distribution(obs).mean)
assert torch.equal(a,b)
print(json.dumps(dict(source_sha256=hashlib.sha256(args.source.read_bytes()).hexdigest(),export_sha256=hashlib.sha256(args.output.read_bytes()).hexdigest(),bytes=args.output.stat().st_size,synthetic_observations=1000,maximum_action_difference=0),indent=2))
