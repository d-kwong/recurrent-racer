"""Freeze five capture routes using geometry only, before policy evaluation."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np


def select(records, count=5):
    records = sorted(records, key=lambda r:r['seed'])
    raw = np.array([[r['length'],sum(a<0 for a in r['turnAngles']),sum(a>0 for a in r['turnAngles']),
                     sum(abs(a) for a in r['turnAngles']),min(r['turnRadii'])] for r in records], dtype=float)
    low, span = raw.min(0), np.ptp(raw, axis=0)
    features = (raw-low)/np.where(span==0,1,span)
    bins = [{(column,min(2,int(value*3))) for column,value in enumerate(row)} for row in features]
    chosen, covered, steps = [], set(), []
    for _ in range(count):
        candidates=[]
        for i,r in enumerate(records):
            if i in chosen:continue
            fresh=len(bins[i]-covered)
            diversity=min(float(np.linalg.norm(features[i]-features[j])) for j in chosen) if chosen else float(np.linalg.norm(features[i]-.5))
            candidates.append((fresh,diversity,-r['seed'],i))
        fresh,diversity,_,i=max(candidates)
        chosen.append(i);covered|=bins[i]
        steps.append(dict(seed=records[i]['seed'],new_feature_bins=fresh,diversity=diversity))
    return dict(selected_seeds=[records[i]['seed'] for i in chosen],
                feature_names=['length','left_turn_count','right_turn_count','total_absolute_angle','minimum_radius'],
                candidates=[dict(seed=r['seed'],geometrySha256=r['geometrySha256'],features=raw[i].tolist()) for i,r in enumerate(records)],
                selection_steps=steps,
                algorithm='Normalize each feature over the eight routes; greedily maximize previously uncovered low/mid/high feature bins, then minimum Euclidean distance from selected routes (first: distance from centre); ties choose lower seed.',
                note='Geometry only. Frozen before policy results. No replacement after capture failure.')


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('geometry',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    if a.output.exists():p.error('Frozen capture selection already exists')
    data=json.loads(a.geometry.read_text());records=data if isinstance(data,list) else data['tracks']
    if sorted(r['seed'] for r in records)!=list(range(20000,20008)):p.error('Expected all eight development geometry records')
    report=select(records);report['geometry_export_sha256']=hashlib.sha256(a.geometry.read_bytes()).hexdigest()
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,indent=2)+'\n');print(report['selected_seeds'])


if __name__=='__main__':main()
