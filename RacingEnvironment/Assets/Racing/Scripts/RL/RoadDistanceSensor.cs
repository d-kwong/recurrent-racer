using UnityEngine;
namespace Racing
{
    // Plain scalar distances appended to the Agent vector; not ML-Agents RayPerceptionSensor encoding.
    public sealed class RoadDistanceSensor : MonoBehaviour
    {
        public static readonly float[] Angles = {-90,-60,-45,-30,-15,0,15,30,45,60,90};
        public const int RayCount = 11;
        public float rayLength = 40;
        public Vector3 localOrigin = new Vector3(0,0.15f,0);
        public LayerMask roadEdgeMask = 1 << 8;
        Rigidbody body;
        Rigidbody Body => body!=null ? body : (body=GetComponent<Rigidbody>());
        // Use the physics pose; rendering interpolation must not delay observations.
        public Vector3 Origin => Body.position+Body.rotation*localOrigin;
        public Vector3 Direction(int index) => Body.rotation*Quaternion.AngleAxis(Angles[index],Vector3.up)*Vector3.forward;
        // Optional presentation snapshot of the SAME queries used for vector observations.
        // No subscriber during training: no snapshot allocations or extra raycasts.
        public event System.Action<Vector3,Vector3[],float[],bool[]> Sampled;
        public float Sample(int index) => Sample(index,out _);
        float Sample(int index,out bool didHit)
        {
            didHit=Physics.Raycast(Origin,Direction(index),out var hit,rayLength,roadEdgeMask,QueryTriggerInteraction.Collide);
            return didHit ? Mathf.Clamp01(hit.distance/rayLength) : 1;
        }
        public void Fill(float[] destination)
        {
            var subscriber=Sampled;
            Vector3[] directions=subscriber==null ? null : new Vector3[RayCount];
            float[] values=subscriber==null ? null : new float[RayCount];
            bool[] hits=subscriber==null ? null : new bool[RayCount];
            for(int i=0;i<RayCount;i++)
            {
                destination[i]=Sample(i,out var hit);
                if(subscriber!=null) { directions[i]=Direction(i); values[i]=destination[i]; hits[i]=hit; }
            }
            subscriber?.Invoke(Origin,directions,values,hits);
        }
        void OnDrawGizmosSelected()
        {
            for (int i=0;i<RayCount;i++)
            {
                float distance=Sample(i)*rayLength; Gizmos.color=distance<rayLength ? Color.cyan : Color.yellow;
                Gizmos.DrawLine(Origin,Origin+Direction(i)*distance); Gizmos.DrawSphere(Origin+Direction(i)*distance,0.08f);
            }
        }
    }
}
