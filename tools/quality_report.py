"""Audit passive quality telemetry and publish route-by-route evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'python'))
import unity_env


def clean(episode):
    q=episode['quality']
    return (episode['reason']=='Finish' and q['maximumReverseSeconds']<=.5001 and
            q['maximumStallSeconds']<=2.0001 and q['offAsphaltFraction']<=.05)


def report(run,seeds):
    config=json.loads((run/'config.json').read_text())
    logs=unity_env.track_records(run/'unity.log')
    episodes=logs['episodes'][-len(seeds):]
    if [e['seed'] for e in episodes]!=seeds or any(e['spawnTurnIndex']!=-1 for e in episodes):
        raise ValueError('Expected exactly requested original-route episodes in seed order')
    tasks={t['taskSha256']:t for t in logs['tracks']}
    rows=[]
    for e in episodes:
        q=e['quality'];expected=clean(e)
        if bool(q['cleanFinish'])!=expected:raise ValueError('Clean flag disagrees with frozen thresholds')
        if q['ticks']<=0 or q['offAsphaltTicks']>q['ticks'] or abs(q['offAsphaltFraction']-q['offAsphaltTicks']/q['ticks'])>1e-6:
            raise ValueError('Quality fraction/tick denominator inconsistent')
        t=tasks[e['taskSha256']]
        row={k:e[k] for k in ['seed','geometrySha256','taskSha256','reason','interrupted','physicsTicks','simulatedSeconds','spawnTurnIndex']}
        row.update(quality=q,actualSpawnPosition=t['actualSpawnPosition'],spawnForward=t['spawnForward'],
                   taskDistance=t['taskDistance'],turnAngles=t['turnAngles'],turnRadii=t['turnRadii'],
                   generation={k:t[k] for k in ['generatorVersion','parameters','acceptedAttempt','roadWidth','curbWidth','length','spawnSeed','spawnDistance','finishDistance','taskDistance','turnEntryDistances','turnExitDistances','activeGateIndices']})
        rows.append(row)
    policy=Path(config['checkpoint'])
    if policy.is_absolute():policy=policy.relative_to(Path(__file__).resolve().parents[1])
    return dict(policy=policy.as_posix(),policy_sha256=config.get('checkpoint_sha256'),
                build_sha256=config.get('build_sha256'),seeds=seeds,route_count=len(rows),
                finish_count=sum(e['reason']=='Finish' for e in rows),clean_count=sum(e['quality']['cleanFinish'] for e in rows),
                mean_finished_simulated_seconds=sum(e['simulatedSeconds'] for e in rows if e['reason']=='Finish')/max(1,sum(e['reason']=='Finish' for e in rows)),
                routes=rows,thresholds=dict(reverse_seconds=.5,stall_seconds=2,off_asphalt_fraction=.05,stall_launch_grace_seconds=2),
                protocol='Original full routes only; actual physics telemetry, clean audit thresholds frozen before policy outcomes.')


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('run',type=Path);p.add_argument('--seeds',type=int,nargs='+',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    r=report(a.run,a.seeds);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,indent=2)+'\n')
    print(f"{r['finish_count']}/{r['route_count']}finish,{r['clean_count']}/{r['route_count']}clean")


if __name__=='__main__':main()
