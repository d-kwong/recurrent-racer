"""Capture real full-route episodes with explicit policy/seed/camera; no training."""
import argparse
import json
from pathlib import Path
import signal
import sys
import torch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'python'))
import train_sac as trainer
import unity_env


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--config',type=Path,default=Path('configs/quality/development.json'))
    p.add_argument('--env',type=Path,required=True);p.add_argument('--checkpoint',type=Path,required=True)
    p.add_argument('--seed',type=int,required=True);p.add_argument('--camera',choices=['chase','overview'],required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--frames',type=Path,required=True)
    p.add_argument('--episodes',type=int,default=1)
    p.add_argument('--sensor-overlay',action='store_true')
    p.add_argument('--poses',type=Path)
    a=p.parse_args();a.output.mkdir(parents=True,exist_ok=False);a.frames.mkdir(parents=True,exist_ok=False)
    config=json.loads(a.config.read_text())
    config.update(env=str(a.env.resolve()),output=str(a.output.resolve()),mode='view',render=True,time_scale=1,
                  track_eval_seeds=[a.seed],track_eval_spawn_indices=[-1],quality_selection=True,quality_telemetry=True,
                  checkpoint=str(a.checkpoint),checkpoint_sha256=trainer.file_sha(a.checkpoint),trainer_sha256=trainer.file_sha(Path(trainer.__file__)),editor=False,worker_id=0,
                  timeout=60,port=5505,camera=a.camera,build_sha256=trainer.build_manifest(a.env)['sha256'],
                  player_args=['--portfolio-camera',a.camera,'--portfolio-capture',str(a.frames.resolve()),
                               '--portfolio-label',f'POLICY / seed {a.seed} / original',
                               '-screen-width','1280','-screen-height','720','-screen-fullscreen','0'])
    if a.sensor_overlay: config['player_args']+=['--portfolio-rays']
    if a.poses: config['player_args']+=['--portfolio-poses',str(a.poses.resolve())]
    (a.output/'config.json').write_text(json.dumps(config,indent=2)+'\n')
    torch.set_num_threads(1);learner=trainer.SAC(dict(config,initial_alpha=.02,learning_rate=.0003,target_entropy=-2,tau=.005))
    data=trainer.load(a.checkpoint,learner)
    signal.signal(signal.SIGINT,lambda *_:setattr(unity_env,'STOP',True))
    env=unity_env.connect(config,a.output/'unity.log',viewer=True)
    try:
        result=trainer.evaluate(env,learner.actor,config,a.output,data['state']['transitions'],a.episodes)
        (a.output/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    finally:env.close()


if __name__=='__main__':main()
