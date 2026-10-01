using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Racing.Editor
{
    public static class PrototypeBuilder
    {
        const string Root = "Assets/Racing/";
        static Material red, white, black, grey, green;
        [MenuItem("Racing/Build Prototype Scene")]
        public static void Build()
        {
            // Never overwrite an open unsaved scene without offering to save it.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Time.fixedDeltaTime = 0.02f;
            var settings = AssetDatabase.LoadAssetAtPath<PrototypeSettings>(Root+"Settings/PrototypeSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<PrototypeSettings>(); AssetDatabase.CreateAsset(settings,Root+"Settings/PrototypeSettings.asset"); }
            float h = settings.straightLength / 2, r = settings.cornerRadius, edge = h+r;
            float roadHalf = settings.roadWidth/2, curbEdge = roadHalf+settings.curbWidth;
            float w = settings.carWidth, l = settings.carLength, height = settings.carHeight;
            red = Material("Red", settings.bodyColor);
            white = Material("White", settings.accentColor); black = Material("Black", settings.wheelColor);
            grey = Material("Road", settings.roadColor); green = Material("Ground", settings.groundColor);
            var ground = Part("Ground", null, PrimitiveType.Cube, new Vector3(0, -0.25f, 0), new Vector3(240, 0.4f, 240), green);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            var track = new GameObject("Track").AddComponent<RaceTrack>();
            var points = new List<Vector3>();
            // Clockwise rounded square: 100 m tangents, 16 m corner radius.
            float radius = r;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 center = corner == 0 ? new Vector3(h,0,h) : corner == 1 ? new Vector3(h,0,-h) : corner == 2 ? new Vector3(-h,0,-h) : new Vector3(-h,0,h);
                float start = 90 - corner * 90;
                for (int step = 0; step <= 32; step++)
                {
                    float angle = (start - step * 90f / 32) * Mathf.Deg2Rad;
                    points.Add(center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                }
                Vector3 end = points[points.Count - 1];
                Vector3 next = corner == 0 ? new Vector3(edge,0,-h) : corner == 1 ? new Vector3(-h,0,-edge) : corner == 2 ? new Vector3(-edge,0,h) : new Vector3(h,0,edge);
                for (int step = 1; step < 50; step++) points.Add(Vector3.Lerp(end, next, step / 50f));
            }
            track.centerline = points.ToArray(); track.roadWidth = settings.roadWidth; track.curbWidth = settings.curbWidth; track.mapHalfExtent = Vector2.one * (edge+curbEdge+6);
            Strip("Road", track.transform, points, -roadHalf, roadHalf, grey);
            Strip("Inner curb", track.transform, points, -curbEdge, -roadHalf, null);
            Strip("Outer curb", track.transform, points, roadHalf, curbEdge, null);
            Boundary(track.transform, points, -curbEdge, "Inner crash boundary");
            Boundary(track.transform, points, curbEdge, "Outer crash boundary");
            track.spawn = new GameObject("Spawn").transform;
            track.spawn.SetParent(track.transform); track.spawn.position = new Vector3(0, settings.wheelDiameter/2-settings.wheelVerticalOffset, edge); track.spawn.rotation = Quaternion.Euler(0, 90, 0);
            var car = new GameObject("Car"); car.transform.SetPositionAndRotation(track.spawn.position, track.spawn.rotation);
            var body = car.AddComponent<Rigidbody>(); body.mass = 800; body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var collision = car.AddComponent<BoxCollider>(); collision.size = new Vector3(settings.collisionWidth, height, l);
            var vehicle = car.AddComponent<ArcadeVehicle>();
            CarVisualBuilder.Build(car, settings, red, white, black);
            var episode = new GameObject("Race Manager").AddComponent<RaceEpisode>();
            var keyboard = car.AddComponent<KeyboardDriver>(); keyboard.vehicle = vehicle; keyboard.episode = episode;
            episode.vehicle = vehicle; episode.keyboard = keyboard; episode.track = track;
            var camera = new GameObject("Chase Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.52f,0.79f,1f); camera.farClipPlane = 350;
            camera.gameObject.AddComponent<AudioListener>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var chase = camera.gameObject.AddComponent<ChaseCamera>(); chase.target = car.transform; chase.vehicle = vehicle; episode.chaseCamera = chase; camera.fieldOfView = chase.fieldOfView; chase.Snap();
            var hud = episode.gameObject.AddComponent<RaceHud>(); hud.episode = episode;
            Vector3[] gates = { new Vector3(0,0,edge), new Vector3(edge,0,0), new Vector3(0,0,-edge), new Vector3(-edge,0,0) };
            for (int i = 0; i < 4; i++)
            {
                var gate = new GameObject(i == 0 ? "Start finish gate" : "Checkpoint " + i);
                gate.transform.SetParent(track.transform); gate.transform.position = gates[i] + Vector3.up;
                gate.transform.rotation = Quaternion.Euler(0, 90 + i*90, 0);
                var trigger = gate.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(curbEdge*2,3,0.3f);
                var checkpoint = gate.AddComponent<CheckpointGate>(); checkpoint.index = i; checkpoint.episode = episode;
            }
            for (int row = 0; row < 4; row++) for (int col = 0; col < Mathf.CeilToInt(settings.roadWidth/0.5f); col++)
            {
                var tile = Part("Finish checker", track.transform, PrimitiveType.Cube, new Vector3(row*0.5f-0.75f,0.025f,edge+col*0.5f-roadHalf+0.25f), new Vector3(0.5f,0.015f,0.5f), (row+col)%2 == 0 ? white : black);
                Object.DestroyImmediate(tile.GetComponent<Collider>());
            }
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f; sun.transform.rotation = Quaternion.Euler(50,-30,0);
            RenderSettings.ambientLight = new Color(0.65f,0.7f,0.8f);
            PrefabUtility.SaveAsPrefabAsset(car, Root+"Prefabs/Car.prefab");
            EditorSceneManager.SaveScene(scene, Root+"Scenes/ManualRacer.unity");
            var builds = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(Root+"Scenes/ManualRacer.unity", true) };
            foreach (var existing in EditorBuildSettings.scenes) if (existing.path != Root+"Scenes/ManualRacer.unity") builds.Add(existing);
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Racing prototype scene built successfully.");
        }
        static Material Material(string name, Color color)
        {
            string path = Root+"Materials/"+name+".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.color = color; material.SetFloat("_Smoothness",0); EditorUtility.SetDirty(material); return material;
        }
        static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition = position; obj.transform.localScale = scale; obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
        }
        static Vector3 Normal(List<Vector3> points, int i)
        {
            var tangent = (points[(i+1)%points.Count]-points[(i-1+points.Count)%points.Count]).normalized;
            return Vector3.Cross(Vector3.up,tangent);
        }
        static void Strip(string name, Transform parent, List<Vector3> points, float inner, float outer, Material material)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var alternate = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i+1)%points.Count; int v = vertices.Count;
                vertices.Add(points[i]+Normal(points,i)*inner); vertices.Add(points[i]+Normal(points,i)*outer);
                vertices.Add(points[next]+Normal(points,next)*inner); vertices.Add(points[next]+Normal(points,next)*outer);
                var target = material == null && i % 2 != 0 ? alternate : triangles;
                target.AddRange(new [] {v,v+2,v+1,v+1,v+2,v+3});
            }
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.subMeshCount = material == null ? 2 : 1;
            mesh.SetTriangles(triangles,0); if (material == null) mesh.SetTriangles(alternate,1); mesh.RecalculateNormals();
            string path = Root+"Settings/"+name+".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) { saved.Clear(); saved.vertices = mesh.vertices; saved.subMeshCount = mesh.subMeshCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) saved.SetTriangles(mesh.GetTriangles(sub),sub);
                saved.RecalculateNormals(); saved.RecalculateBounds(); EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(mesh); mesh = saved; } else AssetDatabase.CreateAsset(mesh,path);
            var obj = new GameObject(name); obj.transform.SetParent(parent); obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials = material == null ? new [] {red,white} : new [] {material};
        }
        static void Boundary(Transform parent, List<Vector3> points, float offset, string name)
        {
            // Continuous solid vertical ribbon, invisible in the game; curb stays drivable.
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i+1)%points.Count; int v = vertices.Count;
                Vector3 a = points[i]+Normal(points,i)*offset, b = points[next]+Normal(points,next)*offset;
                vertices.Add(a-Vector3.up); vertices.Add(b-Vector3.up); vertices.Add(a+Vector3.up*3); vertices.Add(b+Vector3.up*3);
                // Double sided triangles: both sides stop rigidbody collisions.
                triangles.AddRange(new [] {v,v+2,v+1,v+1,v+2,v+3,v+1,v+2,v,v+3,v+2,v+1});
            }
            var mesh = new Mesh {name = name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals();
            string path = Root+"Settings/"+name+".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) { saved.Clear(); saved.vertices = mesh.vertices; saved.subMeshCount = mesh.subMeshCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) saved.SetTriangles(mesh.GetTriangles(sub),sub);
                saved.RecalculateNormals(); saved.RecalculateBounds(); EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(mesh); mesh = saved; } else AssetDatabase.CreateAsset(mesh,path);
            var obj = new GameObject(name); obj.transform.SetParent(parent); obj.AddComponent<MeshCollider>().sharedMesh = mesh; obj.AddComponent<CrashBoundary>();
        }
    }
}
