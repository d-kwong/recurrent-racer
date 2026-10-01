from types import SimpleNamespace
import numpy as np
from mlagents_envs.base_env import DecisionSteps, TerminalSteps

def steps(ids, rewards, terminal=False, interrupted=None):
    obs=[np.zeros((len(ids),15),dtype=np.float32)]
    args=(obs,np.array(rewards,dtype=np.float32))
    if terminal:
        return TerminalSteps(*args,np.array(interrupted or [False]*len(ids)),np.array(ids),np.zeros(len(ids),dtype=np.int32),np.zeros(len(ids),dtype=np.float32))
    return DecisionSteps(*args,np.array(ids),None,np.zeros(len(ids),dtype=np.int32),np.zeros(len(ids),dtype=np.float32))


class FakeUnity:
    def __init__(self):
        spec=SimpleNamespace(observation_specs=[SimpleNamespace(shape=(15,))],
            action_spec=SimpleNamespace(continuous_size=2,discrete_size=0))
        self.behavior_specs={'RecurrentRacer?team=7':spec}
        d=lambda ids,r: steps(ids,r)
        t=lambda ids,r,interrupt=None: steps(ids,r,True,interrupt)
        self.deliveries=[(d([4],[0]),t([],[])),(d([],[]),t([],[])),
            (d([4],[2]),t([],[])),(d([9],[0]),t([4],[3])),
            (d([11],[0]),t([9],[5],[True]))]
        self.sent=[]
    def reset(self): self.index=0
    def get_steps(self,key): return self.deliveries[self.index]
    def set_actions(self,key,actions):
        ids=self.deliveries[self.index][0].agent_id.tolist()
        assert actions.continuous.shape==(len(ids),2)
        self.sent.append(ids)
    def step(self): self.index+=1
