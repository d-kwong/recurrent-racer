"""Make compact capture provenance from actual terminal logs and camera/timing metadata."""
import argparse,csv,json
from pathlib import Path
from quality_report import report


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('captures',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--seeds',type=int,nargs='+',default=[20001,20003,20007,20002,20005]);a=p.parse_args();rows=[]
    for seed in a.seeds:
        for mode in ['chase','overview']:
            run=a.captures/f'capture-{seed}-{mode}';frames=a.captures/f'frames-{seed}-{mode}'
            evidence=report(run,[seed]);config=json.loads((run/'config.json').read_text());episode=evidence['routes'][0]
            camera=dict(mode=mode,vertical_fov=48)
            if mode=='overview':
                actual=list(csv.DictReader((frames/'camera.csv').open()))
                matching=[c for c in actual if c['geometry']==episode['geometrySha256']]
                if len(matching)!=1:raise ValueError('Missing unique realized overview geometry/pose')
                camera.update(matching[0]);camera.update(fixed_during_episode=True)
            else:camera.update(downward_angle=35,distance=22,look_ahead=4,speed_fov_boost=0)
            timestamps=list(csv.DictReader((frames/'frames.csv').open()))
            driving=[]
            for f in timestamps:
                if driving and float(f['episode_seconds'])<float(driving[-1]['episode_seconds']):break
                driving.append(f)
            rows.append(dict(seed=seed,checkpoint=evidence['policy'],checkpoint_sha256=evidence['policy_sha256'],build_sha256=evidence['build_sha256'],
                generation_parameters=episode['generation'],requested_track_configuration={k:v for k,v in config.items() if k.startswith('track_')},spawn_index=-1,actual_spawn_position=episode['actualSpawnPosition'],spawn_forward=episode['spawnForward'],
                geometry_sha256=episode['geometrySha256'],task_sha256=episode['taskSha256'],outcome=episode['reason'],quality=episode['quality'],
                physics_ticks=episode['physicsTicks'],simulated_seconds=episode['simulatedSeconds'],camera=camera,
                captured_frames=len(timestamps),contiguous_first_episode_frames=len(driving),reset_frames_excluded=len(timestamps)-len(driving),
                first_visual_episode_seconds=float(driving[0]['episode_seconds']),last_visual_episode_seconds=float(driving[-1]['episode_seconds']),
                timing='Actual physics ticks at .02s; rendered PNG acquisition cadence .05 wall seconds with simulated-time timestamps. Encoded media sidecars define excerpt/hold/playback.',
                visual_acquisition_note='Capture begins at or just after .1 simulated seconds; terminal reset frames are excluded, not failures or stalls.'))
    a.output.write_text(json.dumps(dict(captures=rows,protocol='Five geometry-preselected original full routes, same frozen checkpoint and publication build, both cameras. No seed replacement.'),indent=2)+'\n')


if __name__=='__main__':main()
