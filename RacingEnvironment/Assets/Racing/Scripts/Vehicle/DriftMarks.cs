using UnityEngine;
using UnityEngine.Rendering;
namespace Racing
{
    public sealed class DriftMarks : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        public Transform[] rearTires;
        public Material material;
        [Min(0)] public float minimumSpeed = 5;
        [Range(0, 45)] public float startSlipAngle = 6;
        [Range(0, 45)] public float stopSlipAngle = 3;
        [Min(0.01f)] public float markWidth = 0.36f;
        [Min(0.01f)] public float sampleDistance = 0.15f;
        [Min(32)] public int maximumSegments = 2400;
        public float surfaceHeight = 0.04f;
        public bool IsDrifting { get; private set; }
        public int SegmentCount { get; private set; }
        Mesh mesh;
        GameObject marks;
        Vector3[] vertices;
        int[] indices;
        Vector3[] previous = new Vector3[2];
        bool connected;
        int nextSegment;
        void Awake()
        {
            maximumSegments = Mathf.Clamp(maximumSegments, 32, 12000);
            vertices = new Vector3[maximumSegments * 4]; indices = new int[maximumSegments * 6];
            mesh = new Mesh { name = "Drift marks (bounded runtime buffer)" }; mesh.MarkDynamic();
            marks = new GameObject("Runtime tire marks");
            marks.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = marks.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        void OnEnable() { if (vehicle != null) vehicle.ResetPerformed += Clear; }
        void OnDisable() { if (vehicle != null) vehicle.ResetPerformed -= Clear; connected = false; }
        void FixedUpdate()
        {
            Vector3 local = transform.InverseTransformDirection(vehicle.Body.linearVelocity);
            float slip = Mathf.Atan2(Mathf.Abs(local.x), Mathf.Max(0.1f, Mathf.Abs(local.z))) * Mathf.Rad2Deg;
            IsDrifting = !vehicle.Crashed && local.magnitude >= minimumSpeed && slip >= (IsDrifting ? Mathf.Min(stopSlipAngle,startSlipAngle) : startSlipAngle);
            if (!IsDrifting) { connected = false; return; }
            bool changed = false;
            for (int tire = 0; tire < 2; tire++)
            {
                Vector3 point = rearTires[tire].position; point.y = surfaceHeight;
                if (connected && Vector3.Distance(point, previous[tire]) >= sampleDistance)
                {
                    Vector3 side = Vector3.Cross(Vector3.up,(point - previous[tire]).normalized) * (markWidth/2);
                    int v = nextSegment * 4, t = nextSegment * 6;
                    vertices[v] = previous[tire] - side; vertices[v+1] = previous[tire] + side;
                    vertices[v+2] = point - side; vertices[v+3] = point + side;
                    // Facing +Y.
                    indices[t] = v; indices[t+1] = v+2; indices[t+2] = v+1;
                    indices[t+3] = v+1; indices[t+4] = v+2; indices[t+5] = v+3;
                    nextSegment = (nextSegment+1)%maximumSegments; SegmentCount = Mathf.Min(SegmentCount+1,maximumSegments);
                    previous[tire] = point; changed = true;
                }
                else if (!connected) previous[tire] = point;
            }
            connected = true;
            if (changed) { mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateBounds(); }
        }
        public void Clear()
        {
            IsDrifting = false; connected = false; nextSegment = 0; SegmentCount = 0;
            if (mesh != null) mesh.Clear();
            System.Array.Clear(vertices,0,vertices.Length); System.Array.Clear(indices,0,indices.Length);
        }
        void OnDestroy() { if (marks != null) Destroy(marks); if (mesh != null) Destroy(mesh); }
    }
}
