using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Unity.MLAgents;

namespace Racing
{
    [Serializable]
    public sealed class TrackParameters
    {
        public int segments = 5;
        public float straightMin = 25, straightMax = 55;
        public float radiusMin = 22, radiusMax = 42;
        public float angleMin = 25, angleMax = 100;
        public float spacing = 2;
        public void Validate()
        {
            if (segments < 2 || segments > 12 || !Range(straightMin, straightMax, 15, 100) ||
                !Range(radiusMin, radiusMax, 18, 80) || !Range(angleMin, angleMax, 10, 120) ||
                !Range(spacing, spacing, .5f, 3))
                throw new ArgumentException("Invalid procedural track parameters");
        }
        static bool Range(float a, float b, float lower, float upper) =>
            !float.IsNaN(a) && !float.IsNaN(b) && a >= lower && b >= a && b <= upper;
    }
    [Serializable]
    public sealed class TrackRecord
    {
        public string generatorVersion = "open-arcs-v1";
        public int seed, acceptedAttempt, sequenceIndex, spawnSeed, spawnTurnIndex = -1, skippedGateCount;
        public TrackParameters parameters;
        public string geometrySha256, taskSha256;
        public float roadWidth, curbWidth, length, spawnDistance = 4, finishDistance, taskDistance;
        public Vector3 spawnPosition, finishPosition, actualSpawnPosition, spawnForward;
        public Vector3 actualFinishGatePosition, finishForward;
        public Vector3[] centerline, leftEdge, rightEdge, leftBoundary, rightBoundary;
        public float[] cumulativeMetres, gateDistances, turnEntryDistances, turnExitDistances, turnAngles, turnRadii;
        public int[] activeGateIndices;
        public float spawnApproach;
    }

    // Optional environment geometry. Instantiated by the agent only when requested.
    public sealed class ProceduralTrack : MonoBehaviour
    {
        public TrackRecord Record { get; private set; }
        RaceTrack track;
        Vector3[] fixedCenterline;
        Transform fixedSpawn;
        int fixedGates, fixedFirstCheckpoint;
        bool fixedClosed;
        readonly List<GameObject> fixedObjects = new List<GameObject>();
        GameObject generated;
        string activeKey;
        public static TrackParameters ReadParameters()
        {
            var p = Academy.Instance.EnvironmentParameters;
            return new TrackParameters {
                segments = Integer(p.GetWithDefault("racing_track_segments", 5), 2, 12, "segments"),
                straightMin = p.GetWithDefault("racing_track_straight_min", 25), straightMax = p.GetWithDefault("racing_track_straight_max", 55),
                radiusMin = p.GetWithDefault("racing_track_radius_min", 22), radiusMax = p.GetWithDefault("racing_track_radius_max", 42),
                angleMin = p.GetWithDefault("racing_track_angle_min", 25), angleMax = p.GetWithDefault("racing_track_angle_max", 100),
                spacing = p.GetWithDefault("racing_track_spacing", 2)
            };
        }
        public static int Integer(float value, int min, int max, string name)
        {
            if (float.IsNaN(value) || value < min || value > max || value != Mathf.Floor(value))
                throw new ArgumentException("Invalid track " + name);
            return (int)value;
        }
        public void Configure(RaceTrack target, RaceEpisode episode, int mode, int seed, TrackParameters parameters, int sequenceIndex = 0)
        {
            if (track == null)
            {
                track = target; fixedCenterline = track.centerline; fixedSpawn = track.spawn;
                fixedGates = track.checkpointCount; fixedFirstCheckpoint=track.firstCheckpoint; fixedClosed = track.closedLoop;
                foreach (Transform child in track.transform) fixedObjects.Add(child.gameObject);
            }
            if (mode == 0)
            {
                if (generated != null) generated.SetActive(false);
                foreach (var obj in fixedObjects) obj.SetActive(true);
                track.centerline = fixedCenterline; track.spawn = fixedSpawn;
                track.checkpointCount = fixedGates; track.firstCheckpoint=fixedFirstCheckpoint; track.closedLoop = fixedClosed;
                Record = null; activeKey = null; return;
            }
            if (mode != 1) throw new ArgumentException("Unknown track mode");
            parameters.Validate();
            string key = seed + ":" + JsonUtility.ToJson(parameters);
            if (activeKey != key || generated == null)
            {
                // Validate before replacing the active track. Failure cannot leave a partial route.
                TrackRecord record = Generate(seed, parameters, track.roadWidth, track.curbWidth);
                Material road = null, curb = null;
                foreach (var renderer in track.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name == "Road") road = renderer.sharedMaterial;
                    if (renderer.name.Contains("curb")) curb = renderer.sharedMaterial;
                }
                if (generated != null) { generated.SetActive(false); ReleaseMeshes(generated); ReleaseObject(generated); }
                foreach (var obj in fixedObjects) obj.SetActive(false);
                Record = record; generated = BuildGeometry(track, episode, record, road, curb, fixedSpawn.position.y);
                activeKey = key;
            }
            else
            {
                foreach (var obj in fixedObjects) obj.SetActive(false);
                generated.SetActive(true);
            }
            track.centerline = Record.centerline; track.closedLoop = false;
            track.spawn = generated.transform.Find("Spawn"); track.checkpointCount = Record.gateDistances.Length;
            SetSpawn(episode,0,0,sequenceIndex,10,20,.25f,-1,15);
            Physics.SyncTransforms();
        }
        public void SetSpawn(RaceEpisode episode,int mode,int spawnSeed,int episodeIndex,float minimum,float maximum,float originalFraction,int turnIndex,float approach)
        {
            var record=Record;
            if(record==null) return;
            Integer(spawnSeed,0,16777215,"spawn seed");
            if(float.IsNaN(minimum) || float.IsNaN(maximum) || float.IsNaN(originalFraction) || float.IsNaN(approach) ||
                float.IsInfinity(minimum) || float.IsInfinity(maximum) || float.IsInfinity(originalFraction) || float.IsInfinity(approach) ||
                mode<0 || mode>2 || minimum<10 || maximum<minimum || maximum>20 || originalFraction<0 || originalFraction>1)
                throw new ArgumentException("Invalid procedural spawn parameters");
            if((mode==1 && maximum>record.parameters.straightMin-4) || (mode==2 && turnIndex>=0 && approach>record.parameters.straightMin-4))
                throw new ArgumentException("Near-turn starts require straightMin >= spawnMax+4m for start-cap clearance");
            if(mode==1)
            {
                // Independent deterministic spawn RNG, no dependence on geometry retry count or Unity Random.
                var rng=new StableRandom(unchecked(spawnSeed*73856093 ^ record.seed*19349663 ^ episodeIndex*83492791));
                bool original=rng.Next()<originalFraction;
                turnIndex=original ? -1 : Mathf.Min((int)(rng.Next()*record.turnEntryDistances.Length),record.turnEntryDistances.Length-1);
                approach=original ? 0 : rng.Between(minimum,maximum);
            }
            else if(mode==0) {turnIndex=-1; approach=0;}
            if(turnIndex<-1 || turnIndex>=record.turnEntryDistances.Length || (turnIndex>=0 && (approach<10 || approach>20)))
                throw new ArgumentException("Invalid explicit procedural spawn turn/approach");
            if(turnIndex<0) approach=0;
            float spawnS=turnIndex<0 ? 4 : record.turnEntryDistances[turnIndex]-approach;
            if(spawnS<4 || spawnS>record.finishDistance-10) throw new ArgumentException("Procedural spawn lacks cap/finish clearance");
            Vector3 pose=At(record,spawnS,out var forward); pose.y=fixedSpawn.position.y;
            track.spawn.SetPositionAndRotation(pose,Quaternion.LookRotation(forward));
            track.firstCheckpoint=0;
            for(int index=1;index<record.gateDistances.Length;index++)
                if(record.gateDistances[index]>spawnS+5) {track.firstCheckpoint=index;break;}
            var suffix=new List<int>();
            if(track.firstCheckpoint!=0) for(int index=track.firstCheckpoint;index<record.gateDistances.Length;index++) suffix.Add(index);
            suffix.Add(0); record.activeGateIndices=suffix.ToArray();
            record.skippedGateCount=track.firstCheckpoint==0 ? record.gateDistances.Length-1 : track.firstCheckpoint-1;
            record.spawnDistance=spawnS; record.spawnPosition=At(record,spawnS,out _);
            record.taskDistance=record.finishDistance-spawnS; record.spawnSeed=spawnSeed;
            record.spawnTurnIndex=turnIndex; record.spawnApproach=approach; record.sequenceIndex=episodeIndex;
            record.actualSpawnPosition=pose; record.spawnForward=forward;
            record.actualFinishGatePosition=record.finishPosition+Vector3.up;
            At(record,record.finishDistance,out record.finishForward);
            string identity=record.geometrySha256+"|"+spawnS.ToString("R",CultureInfo.InvariantCulture)+"|"+string.Join(",",record.activeGateIndices);
            using(var sha=SHA256.Create()) record.taskSha256=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
            Physics.SyncTransforms();
        }
        struct StableRandom
        {
            uint state;
            public StableRandom(int seed) { state = (uint)seed + 1; }
            public float Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state >> 8) / 16777216f; }
            public float Between(float a, float b) => a + (b - a) * Next();
        }
        public static TrackRecord Generate(int seed, TrackParameters parameters, float roadWidth = 9, float curbWidth = 1)
        {
            Integer(seed, 0, 16777215, "seed"); parameters.Validate();
            if (roadWidth != 9 || curbWidth != 1) throw new ArgumentException("Generator v1 requires the baseline 9m road/1m curbs");
            var rng = new StableRandom(seed);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var points = new List<Vector3> { Vector3.zero };
                Vector3 position = Vector3.zero; float heading = 0;
                var entries=new List<int>(); var exits=new List<int>(); var angles=new List<float>(); var radii=new List<float>();
                int firstSign = rng.Next() < .5f ? -1 : 1;
                for (int section = 0; section < parameters.segments; section++)
                {
                    float straight = rng.Between(parameters.straightMin, parameters.straightMax);
                    int steps = Mathf.CeilToInt(straight / parameters.spacing);
                    Vector3 start = position, forward = Quaternion.Euler(0, heading, 0) * Vector3.forward;
                    for (int i = 1; i <= steps; i++) points.Add(start + forward * (straight * i / steps));
                    position = points[points.Count - 1];
                    // Alternating turns ensure both directions; sampled angles/radii and straights vary combinations.
                    float angle = rng.Between(parameters.angleMin, parameters.angleMax) * firstSign * (section % 2 == 0 ? 1 : -1);
                    float radius = rng.Between(parameters.radiusMin, parameters.radiusMax);
                    entries.Add(points.Count-1); angles.Add(angle); radii.Add(radius);
                    steps = Mathf.CeilToInt(Mathf.Abs(angle) * Mathf.Deg2Rad * radius / parameters.spacing);
                    Vector3 right = Quaternion.Euler(0, heading, 0) * Vector3.right;
                    Vector3 center = position + right * (Mathf.Sign(angle) * radius), radial = position - center;
                    for (int i = 1; i <= steps; i++) points.Add(center + Quaternion.Euler(0, angle * i / steps, 0) * radial);
                    position = points[points.Count - 1]; heading += angle; exits.Add(points.Count-1);
                }
                // The finish stays on a straight, leaving room behind its passable gate.
                Vector3 last = position, direction = Quaternion.Euler(0, heading, 0) * Vector3.forward;
                int finalSteps = Mathf.CeilToInt(20 / parameters.spacing);
                for (int i = 1; i <= finalSteps; i++) points.Add(last + direction * (20f * i / finalSteps));
                var route = points.ToArray(); var cumulative = new float[route.Length];
                for (int i = 1; i < route.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(route[i - 1], route[i]);
                if (!IsValid(route, cumulative, roadWidth + 2 * curbWidth + 4)) continue;
                var record = new TrackRecord { seed = seed, acceptedAttempt = attempt, parameters = parameters,
                    roadWidth = roadWidth, curbWidth = curbWidth, centerline = route, cumulativeMetres = cumulative,
                    length = cumulative[cumulative.Length - 1] };
                record.leftEdge = Offset(route, -roadWidth / 2); record.rightEdge = Offset(route, roadWidth / 2);
                record.leftBoundary = Offset(route, -roadWidth / 2 - curbWidth); record.rightBoundary = Offset(route, roadWidth / 2 + curbWidth);
                record.finishDistance = record.length - 6; record.taskDistance = record.finishDistance - record.spawnDistance;
                record.spawnPosition = At(record, record.spawnDistance, out _); record.finishPosition = At(record, record.finishDistance, out _);
                int gates = Mathf.CeilToInt(record.length / 35) + 1;
                record.gateDistances = new float[gates];
                record.gateDistances[0] = record.finishDistance; // End cap 6m beyond finish, no closing chord.
                for (int i = 1; i < gates; i++) record.gateDistances[i] = record.gateDistances[0] * i / gates;
                record.turnEntryDistances=entries.ConvertAll(i=>cumulative[i]).ToArray();
                record.turnExitDistances=exits.ConvertAll(i=>cumulative[i]).ToArray();
                record.turnAngles=angles.ToArray(); record.turnRadii=radii.ToArray();
                record.geometrySha256 = Digest(record); return record;
            }
            throw new InvalidOperationException("No valid procedural track after 64 deterministic attempts, seed=" + seed);
        }
        public static bool IsValid(Vector3[] route, float[] cumulative, float separation)
        {
            for (int i = 1; i < route.Length; i++)
            {
                if ((route[i] - route[i - 1]).sqrMagnitude < .01f) return false;
                for (int j = 1; j < i; j++)
                {
                    // Nearby samples on the same local bend are not shortcuts. Compare segment separation,
                    // not only vertices, once along-route distance exceeds twice the clearance.
                    if (cumulative[i - 1] - cumulative[j] < separation * 2) continue;
                    if (SegmentDistance(route[i - 1], route[i], route[j - 1], route[j]) < separation) return false;
                }
            }
            return true;
        }
        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float Cross(Vector3 u, Vector3 v) => u.x * v.z - u.z * v.x;
            Vector3 u = b - a, v = d - c; float denominator = Cross(u, v);
            if (Mathf.Abs(denominator) > 1e-6f)
            {
                float t = Cross(c - a, v) / denominator, s = Cross(c - a, u) / denominator;
                if (t >= 0 && t <= 1 && s >= 0 && s <= 1) return 0;
            }
            float Distance(Vector3 p, Vector3 x, Vector3 y) => Vector3.Distance(p, x + (y - x) * Mathf.Clamp01(Vector3.Dot(p - x, y - x) / (y - x).sqrMagnitude));
            return Mathf.Min(Distance(a, c, d), Distance(b, c, d), Distance(c, a, b), Distance(d, a, b));
        }
        public static Vector3 Tangent(Vector3[] points, int i) =>
            (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
        public static Vector3[] Offset(Vector3[] points, float offset)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = points[i] + Vector3.Cross(Vector3.up, Tangent(points, i)) * offset;
            return result;
        }
        public static Vector3 At(TrackRecord record, float distance, out Vector3 tangent)
        {
            for (int i = 1; i < record.centerline.Length; i++)
                if (record.cumulativeMetres[i] >= distance)
                {
                    tangent = (record.centerline[i] - record.centerline[i - 1]).normalized;
                    return Vector3.Lerp(record.centerline[i - 1], record.centerline[i],
                        (distance - record.cumulativeMetres[i - 1]) / (record.cumulativeMetres[i] - record.cumulativeMetres[i - 1]));
                }
            tangent = Tangent(record.centerline, record.centerline.Length - 1); return record.centerline[record.centerline.Length - 1];
        }
        static string Digest(TrackRecord record)
        {
            var canonical = new StringBuilder(record.generatorVersion);
            foreach (var point in record.centerline)
                canonical.Append('|').Append(point.x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(point.z.ToString("R", CultureInfo.InvariantCulture));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        public static GameObject BuildGeometry(RaceTrack track, RaceEpisode episode, TrackRecord record, Material road, Material curb, float spawnHeight)
        {
            var root = new GameObject("Procedural geometry"); root.transform.SetParent(track.transform, false);
            Strip(root.transform, "Road", record.leftEdge, record.rightEdge, road);
            Strip(root.transform, "Left curb", record.leftBoundary, record.leftEdge, curb);
            Strip(root.transform, "Right curb", record.rightEdge, record.rightBoundary, curb);
            Ribbon(root.transform, "Left sensor edge", record.leftEdge, true);
            Ribbon(root.transform, "Right sensor edge", record.rightEdge, true);
            Ribbon(root.transform, "Left crash boundary", record.leftBoundary, false);
            Ribbon(root.transform, "Right crash boundary", record.rightBoundary, false);
            Ribbon(root.transform, "Start crash cap", new[] { record.leftBoundary[0], record.rightBoundary[0] }, false);
            int end = record.centerline.Length - 1;
            Ribbon(root.transform, "End crash cap", new[] { record.leftBoundary[end], record.rightBoundary[end] }, false);
            var spawn = new GameObject("Spawn").transform; spawn.SetParent(root.transform, false);
            // 4m clearance behind the car against the start cap; route credit begins at that pose.
            spawn.position = At(record, record.spawnDistance, out var initialDirection) + Vector3.up * spawnHeight;
            spawn.rotation = Quaternion.LookRotation(initialDirection);
            for (int i = 0; i < record.gateDistances.Length; i++)
            {
                var gate = new GameObject(i == 0 ? "Finish gate" : "Checkpoint " + i); gate.transform.SetParent(root.transform, false);
                gate.transform.position = At(record, record.gateDistances[i], out var direction) + Vector3.up;
                gate.transform.rotation = Quaternion.LookRotation(direction);
                var box = gate.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(record.roadWidth + 2 * record.curbWidth, 3, .3f);
                var checkpoint = gate.AddComponent<CheckpointGate>(); checkpoint.index = i; checkpoint.episode = episode;
            }
            return root;
        }
        static void Strip(Transform root, string name, Vector3[] a, Vector3[] b, Material material)
        {
            var vertices = new Vector3[a.Length * 2]; var triangles = new int[(a.Length - 1) * 6];
            for (int i = 0; i < a.Length; i++) { vertices[i * 2] = a[i]; vertices[i * 2 + 1] = b[i]; }
            for (int i = 0; i < a.Length - 1; i++) { int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1; triangles[t+3] = v+1; triangles[t+4] = v+2; triangles[t+5] = v+3; }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals();
            var obj = new GameObject(name); obj.transform.SetParent(root, false); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        static void Ribbon(Transform root, string name, Vector3[] points, bool sensor)
        {
            var vertices = new Vector3[points.Length * 2]; var triangles = new int[(points.Length - 1) * 12];
            for (int i = 0; i < points.Length; i++) { vertices[i * 2] = points[i] - Vector3.up; vertices[i * 2 + 1] = points[i] + Vector3.up * (sensor ? 2 : 3); }
            for (int i = 0; i < points.Length - 1; i++) { int v = i * 2, t = i * 12;
                int[] faces = { v,v+1,v+2,v+2,v+1,v+3,v+2,v+1,v,v+3,v+1,v+2 };
                Array.Copy(faces, 0, triangles, t, 12); }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles }; mesh.RecalculateNormals();
            var obj = new GameObject(name); obj.transform.SetParent(root, false); obj.layer = sensor ? 8 : 0;
            obj.AddComponent<MeshCollider>().sharedMesh = mesh;
            if (!sensor) obj.AddComponent<CrashBoundary>();
        }
        static void ReleaseObject(UnityEngine.Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void OnDestroy() { if(generated != null) ReleaseMeshes(generated); }
        static void ReleaseMeshes(GameObject geometry)
        {
            foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>()) ReleaseObject(filter.sharedMesh);
            foreach (var collider in geometry.GetComponentsInChildren<MeshCollider>()) ReleaseObject(collider.sharedMesh);
        }
    }
}
