"""Check native manual startup routing; no keyboard input or policy inference."""
import argparse,json,subprocess,sys,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'python'))
import train_sac,unity_env


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--env',type=Path,required=True);p.add_argument('--logs',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    player=next((a.env/'Contents/MacOS').iterdir()).resolve();results=[];a.logs.mkdir(parents=True,exist_ok=True)
    for fixed in [False,True]:
        name='native-manual-'+('fixed' if fixed else 'default');log=(a.logs/(name+'.log')).resolve()
        command=[str(player),'-batchmode','-nographics','--racing-no-render','--racing-manual','-logFile',str(log)]
        if fixed:command+=['--racing-fixed']
        process=subprocess.Popen(command,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        try:
            for tick in range(15):
                time.sleep(1)
                if not fixed and unity_env.track_records(log)['tracks']:break
                if fixed and tick>=5:break
            if process.poll() is not None:raise RuntimeError('Native manual startup exited unexpectedly')
            records=unity_env.track_records(log)['tracks'];text=log.read_text(errors='replace')
            if not fixed:
                if not records or records[-1]['seed']!=1009 or records[-1]['spawnTurnIndex']!=-1:raise RuntimeError('Native default did not use1009original')
            elif records:raise RuntimeError('Native explicit fixed generated procedural geometry')
            if 'NullReferenceException' in text or 'InvalidOperationException' in text:raise RuntimeError('Native manual startup exception')
            results.append(dict(mode='manual',track='fixed' if fixed else 'procedural',seed=None if fixed else 1009,original_start=True,started=True,procedural_record_count=len(records),explicit_fixed_bypass=bool(fixed),note='Startup configuration only; no keyboard driving or policy inference. Fixed route checked by explicit bypass/no generated record, unchanged scene and separate historical fixed numerical regression.'))
        finally:
            process.terminate()
            try:process.wait(timeout=5)
            except subprocess.TimeoutExpired:process.kill();process.wait()
        print(name,'PASS',flush=True)
    a.output.write_text(json.dumps(dict(build_sha256=train_sac.build_manifest(a.env)['sha256'],checks=results),indent=2)+'\n')


if __name__=='__main__':main()
