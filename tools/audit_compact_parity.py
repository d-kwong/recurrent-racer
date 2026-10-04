"""Verify frozen exported actions and camera trace parity without consuming new tasks."""
import argparse,json,sys
from pathlib import Path
import numpy as np
import torch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'python'))
import train_sac
from quality_report import report


def actor_trace(policy,trace):
    torch.set_num_threads(1);actor=train_sac.Actor();actor.load_state_dict(torch.load(policy,map_location='cpu',weights_only=True)['actor'])
    count=0
    with np.load(trace) as arrays,torch.no_grad():
        for key in arrays.files:
            if not key.endswith('_observations'):continue
            base=key[:-len('observations')];observations=arrays[key]
            if key.endswith('terminal_observations'):continue
            for i,observation in enumerate(observations):
                mean=actor.distribution(torch.from_numpy(observation[None,:].copy())).mean
                action=torch.tanh(mean)[0].numpy()
                if not np.array_equal(action,arrays[base+'actions'][i]) or not np.array_equal(mean[0].numpy(),arrays[base+'pre_tanh'][i]):
                    raise ValueError(f'Exported actor mismatch {trace}:{key}:{i}')
                count+=1
    return count


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--policy',type=Path,required=True);p.add_argument('--held-out-trace',type=Path,required=True)
    p.add_argument('--headless',type=Path,required=True);p.add_argument('--captures',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    seeds=[50007,50001,50000,50002,50005];rows=[]
    with np.load(a.headless) as reference:
        for i,seed in enumerate(seeds):
            for camera in ['chase','overview']:
                trace=a.captures/f'capture-{seed}-{camera}'/'eval-000012210-start0.npz'
                with np.load(trace) as actual:
                    for key in actual.files:
                        expected_key=key.replace('episode_0_',f'episode_{seed-50000}_')
                        if not np.array_equal(actual[key],reference[expected_key]):raise ValueError(f'Camera changed numerical trace: {seed}:{camera}:{key}')
                quality=report(trace.parent,[seed])
                if quality['clean_count']!=1:raise ValueError(f'Capture failed frozen clean gate: {seed}:{camera}')
                rows.append(dict(seed=seed,camera=camera,bitwise_equal=True,trace_sha256=train_sac.file_sha(trace),clean_finish=True,episode=quality['routes'][0],build_sha256=quality['build_sha256']))
    result=dict(policy_sha256=train_sac.file_sha(a.policy),held_out_actor_actions_checked=actor_trace(a.policy,a.held_out_trace),headless_actor_actions_checked=actor_trace(a.policy,a.headless),camera_runs=rows,comparison='Exact array equality for observations,pre_tanh,actions,rewards,terminal_observation,boundary; rowwise exported mean actor recomputation.')
    a.output.write_text(json.dumps(result,indent=2)+'\n');print(result['held_out_actor_actions_checked'], 'held-out actions and all camera traces match')


if __name__=='__main__':main()
