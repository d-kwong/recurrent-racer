"""Publish compact task-level evidence; geometry and bulk training logs stay local."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import torch


def summarize(out):
    config = json.loads((out/'config.json').read_text())
    state = json.loads((out/'final-state.json').read_text())
    selected = torch.load(out/'best.pt', map_location='cpu', weights_only=True)
    selected_step = selected['state']['transitions']
    final_step = state['transitions']
    batches = {}
    with (out/'evaluation-episodes.csv').open() as f:
        episode_rows = list(csv.DictReader(f))
    for label, step in [('initial',0),('selected',selected_step),('final',final_step)]:
        telemetry = json.loads((out/f'track-eval-{step:09d}.json').read_text())
        tasks = len(config['track_eval_seeds'])*len(config['track_eval_spawn_indices'])
        endings = telemetry['episodes'][-tasks:]
        starts = {r['taskSha256']:r for r in telemetry['tracks']}
        records = []
        delivered_rows = [row for row in episode_rows if int(row['step']) == step]
        for episode_index,e in enumerate(endings):
            r = starts[e['taskSha256']]
            fields = ('seed','spawnTurnIndex','taskSha256','geometrySha256','reason','interrupted',
                      'physicsTicks','simulatedSeconds','travelMetres','spawnDistance','spawnApproach')
            record = {k:e[k] for k in fields}
            record.update(estimatedSeconds=float(delivered_rows[episode_index]['estimated_seconds']),
                          taskDistance=e['finishDistance'],normalized_progress=1. if e['reason']=='Finish' else
                          max(0,min(1,e['travelMetres']/e['finishDistance'])),
                          actualSpawnPosition=r['actualSpawnPosition'],spawnForward=r['spawnForward'],
                          activeGateIndices=r['activeGateIndices'])
            records.append(record)
        original = [e for e in records if e['spawnTurnIndex']==-1]
        batches[label] = dict(step=step,complete_tasks=len(records),finishes=sum(e['reason']=='Finish' for e in records),
                             original_tasks=len(original),original_finishes=sum(e['reason']=='Finish' for e in original),
                             mean_normalized_progress=sum(e['normalized_progress'] for e in records)/len(records),
                             task_records=records)
    with (out/'evaluations.csv').open() as f: aggregate=list(csv.DictReader(f))
    return dict(state=state,selected_checkpoint_step=selected_step,selected_full_checkpoint_sha256=hashlib.sha256((out/'best.pt').read_bytes()).hexdigest(),
                config={k:config[k] for k in config if k.startswith('track_') or k in
                        ('seed','spawn_min','spawn_max','original_fraction','max_transitions','max_seconds','learning_starts','batch_size','replay_capacity')},
                build_sha256=config['build_sha256'],initialization=config.get('warm_start'),
                aggregate_evaluations=aggregate,batches=batches,
                selection='Completion fraction, normalized task progress; mean estimated time only when all identical tasks finish. Raw return unused for ranking.',
                limitations='One training RNG seed, three training routes, three evaluation routes; evaluation tasks reused for selection, no untouched final track suite. Task suffix finishes distinguished from original-route completion.')


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('run',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    result=summarize(a.run)
    if result['initialization']:
        result['initialization'].pop('path',None)
    a.output.parent.mkdir(parents=True,exist_ok=True)
    a.output.write_text(json.dumps(result,indent=2)+'\n')
    print(f"selected step{result['selected_checkpoint_step']}, {result['batches']['selected']['finishes']}/{result['batches']['selected']['complete_tasks']} task finishes")


if __name__ == '__main__':main()
