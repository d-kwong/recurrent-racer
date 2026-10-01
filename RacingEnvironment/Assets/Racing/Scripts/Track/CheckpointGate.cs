using UnityEngine;
namespace Racing
{
    public sealed class CheckpointGate : MonoBehaviour
    {
        public int index;
        public RaceEpisode episode;
        void OnTriggerEnter(Collider other)
        {
            var vehicle = other.GetComponent<ArcadeVehicle>();
            if (vehicle == episode.vehicle && Vector3.Dot(vehicle.Body.linearVelocity, transform.forward) > 0.1f)
                episode.PassCheckpoint(index);
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.matrix = transform.localToWorldMatrix; Gizmos.DrawWireCube(Vector3.zero, GetComponent<BoxCollider>().size); }
    }
}
