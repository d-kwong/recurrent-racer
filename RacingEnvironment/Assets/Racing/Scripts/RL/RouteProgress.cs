using UnityEngine;
namespace Racing
{
    // Reward-only route information. Never exposed to the policy.
    public sealed class RouteProgress : MonoBehaviour
    {
        public RaceTrack track;
        public float gateTolerance = 5;
        public float TravelMetres { get; private set; }
        public float LateralDistance { get; private set; }
        public float RouteLength { get; private set; }
        public float RewardPosition => Mathf.Clamp(TravelMetres,-gateTolerance,nextGateDistance+gateTolerance);
        float[] lengths;
        float previousS, spawnS, nextGateDistance;
        int validatedGates;
        public int ValidatedGates => validatedGates;
        public void ResetProgress() => ResetProgress(track.spawn.position);
        public void ResetProgress(Vector3 episodeSpawn)
        {
            if (lengths == null || lengths.Length != track.centerline.Length+1)
            {
                lengths=new float[track.centerline.Length+1];
                for (int i=0;i<track.centerline.Length;i++) lengths[i+1]=lengths[i]+Vector3.Distance(track.centerline[i],track.centerline[(i+1)%track.centerline.Length]);
            }
            RouteLength=lengths[lengths.Length-1]; spawnS=Project(episodeSpawn,out _);
            previousS=spawnS; TravelMetres=0; LateralDistance=0; validatedGates=0; nextGateDistance=GateDistance(1);
        }
        public float Project(Vector3 position,out float distance)
        {
            Vector3 p=position; p.y=0; float best=float.PositiveInfinity,s=0;
            for (int i=0;i<track.centerline.Length;i++)
            {
                Vector3 a=track.centerline[i], edge=track.centerline[(i+1)%track.centerline.Length]-a;
                float t=Mathf.Clamp01(Vector3.Dot(p-a,edge)/edge.sqrMagnitude);
                float squared=(p-(a+t*edge)).sqrMagnitude;
                if (squared<best) { best=squared; s=lengths[i]+t*edge.magnitude; }
            }
            distance=Mathf.Sqrt(best); return s;
        }
        public bool Advance(Vector3 position,float maximumDelta)
        {
            float s=Project(position,out var lateral);
            float delta=Mathf.Repeat(s-previousS+RouteLength/2,RouteLength)-RouteLength/2;
            previousS=s; LateralDistance=lateral;
            if (Mathf.Abs(delta)>maximumDelta) return false;
            TravelMetres+=delta; return true;
        }
        float GateDistance(int index)
        {
            if(index==0)
            {
                float remaining=Mathf.Repeat(Project(track.spawn.position,out _)-spawnS,RouteLength);
                return remaining<0.001f ? RouteLength : remaining;
            }
            foreach(var gate in track.GetComponentsInChildren<CheckpointGate>())
                if(gate.index==index) return Mathf.Repeat(Project(gate.transform.position,out _)-spawnS,RouteLength);
            throw new System.InvalidOperationException("Missing ordered checkpoint " + index);
        }
        public bool ValidateGate(int index)
        {
            int expected=(validatedGates+1)%track.checkpointCount;
            // A trigger entry can precede center crossing by half the 5.4 m collider length.
            return index==expected && Mathf.Abs(TravelMetres-GateDistance(index))<=gateTolerance;
        }
        public void AcceptGate(int index)
        {
            validatedGates++;
            nextGateDistance= index==0 ? RouteLength : GateDistance((index+1)%track.checkpointCount);
        }
    }
}
