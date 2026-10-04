"""Record actual independent checkpoint physics poses without rendering/training."""
import argparse
import json
from pathlib import Path
import sys
import torch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'python'))
import train_sac as trainer
import unity_env


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--env',type=Path,required=True)
    parser.add_argument('--checkpoint',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--seed',type=int,default=50007)
    args=parser.parse_args()
    config=json.loads(Path('configs/train.json').read_text())
    config.update(json.loads(Path('configs/compact/development.json').read_text()))
    config.update(env=str(args.env.resolve()),checkpoint=str(args.checkpoint.resolve()),
        checkpoint_sha256=trainer.file_sha(args.checkpoint),build_sha256=trainer.build_manifest(args.env)['sha256'],
        output=str(args.output.resolve()),mode='evaluate',render=False,editor=False,worker_id=0,timeout=60,
        track_eval_seeds=[args.seed],track_eval_spawn_indices=[-1],
        player_args=['--portfolio-poses',str((args.output/'poses.raw.csv').resolve())])
    args.output.mkdir(parents=True,exist_ok=False)
    (args.output/'config.json').write_text(json.dumps(config,indent=2)+'\n')
    torch.set_num_threads(1); learner=trainer.SAC(config); data=trainer.load(args.checkpoint,learner)
    env=unity_env.connect(config,args.output/'unity.log',viewer=False)
    try:
        result=trainer.evaluate(env,learner.actor,config,args.output,data['state']['transitions'],1)
        (args.output/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    finally:
        env.close()


if __name__=='__main__':
    main()
