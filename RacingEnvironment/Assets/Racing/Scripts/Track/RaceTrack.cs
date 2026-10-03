using UnityEngine;
namespace Racing
{
    public sealed class RaceTrack : MonoBehaviour
    {
        public Transform spawn;
        public bool closedLoop = true;
        public Vector3[] centerline;
        public float roadWidth = 9f;
        public float curbWidth = 1f;
        public int checkpointCount = 4;
        public int firstCheckpoint = 1;
        public Vector2 mapHalfExtent = new Vector2(78, 78);
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            if (centerline != null) for (int i = 0; i < centerline.Length - (closedLoop ? 0 : 1); i++)
                Gizmos.DrawLine(centerline[i] + Vector3.up * 0.15f, centerline[(i + 1) % centerline.Length] + Vector3.up * 0.15f);
            if (spawn != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(spawn.position, 1); Gizmos.DrawRay(spawn.position, spawn.forward * 5); }
        }
    }
}
