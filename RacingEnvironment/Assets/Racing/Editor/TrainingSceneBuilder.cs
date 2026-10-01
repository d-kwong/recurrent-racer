using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
namespace Racing.Editor
{
    public static class TrainingSceneBuilder
    {
        public const string ScenePath="Assets/Racing/Scenes/RacingTraining.unity";
        public const string BuildPath="Builds/macOS/RacingTraining.app";
        [MenuItem("Racing/Build Training Scene")]
        public static void BuildScene()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/Racing/Scenes/ManualRacer.unity");
            // Save a copy before modifying any scene objects; original gameplay stays untouched.
            EditorSceneManager.SaveScene(scene,ScenePath);
            Layer(8,"RacingRoadEdge");Layer(9,"RacingVehicle");
            var episode=Object.FindFirstObjectByType<RaceEpisode>();var car=episode.vehicle.gameObject;
            foreach(var transform in car.GetComponentsInChildren<Transform>()) transform.gameObject.layer=9;
            var track=episode.track;
            CreateEdge(track,-track.roadWidth/2,"Inner sensor edge");CreateEdge(track,track.roadWidth/2,"Outer sensor edge");
            var distances=car.AddComponent<RoadDistanceSensor>();
            var progress=car.AddComponent<RouteProgress>();progress.track=track;
            var behavior=car.AddComponent<BehaviorParameters>();
            behavior.BehaviorName="RecurrentRacer";behavior.TeamId=0;behavior.BehaviorType=BehaviorType.Default;
            behavior.BrainParameters.VectorObservationSize=RacingAgent.ObservationCount;
            behavior.BrainParameters.NumStackedVectorObservations=1;
            behavior.BrainParameters.ActionSpec=ActionSpec.MakeContinuous(2);
            behavior.UseChildSensors=false;behavior.UseChildActuators=false;
            behavior.ObservableAttributeHandling=ObservableAttributeOptions.Ignore;
            var agent=car.AddComponent<RacingAgent>();agent.vehicle=episode.vehicle;agent.episode=episode;agent.distances=distances;agent.progress=progress;agent.MaxStep=0;
            var mode=episode.gameObject.AddComponent<TrainingMode>();mode.agent=agent;mode.keyboard=episode.keyboard;mode.episode=episode;
            episode.automaticStartReset=false;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TRAINING_SCENE_BUILT: "+ScenePath);
        }
        static void Layer(int index,string name)
        {
            var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var entry=manager.FindProperty("layers").GetArrayElementAtIndex(index);
            if(!string.IsNullOrEmpty(entry.stringValue)&&entry.stringValue!=name) throw new System.InvalidOperationException("Layer "+index+" is already used.");
            entry.stringValue=name;manager.ApplyModifiedPropertiesWithoutUndo();
        }
        static void CreateEdge(RaceTrack track,float offset,string name)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var points=track.centerline;
            for(int i=0;i<points.Length;i++)
            {
                int next=(i+1)%points.Length;
                Vector3 normalA=Vector3.Cross(Vector3.up,(points[next]-points[(i-1+points.Length)%points.Length]).normalized);
                Vector3 normalB=Vector3.Cross(Vector3.up,(points[(next+1)%points.Length]-points[i]).normalized);
                Vector3 a=points[i]+normalA*offset,b=points[next]+normalB*offset;int v=vertices.Count;
                vertices.Add(a-Vector3.up);vertices.Add(b-Vector3.up);vertices.Add(a+Vector3.up*2);vertices.Add(b+Vector3.up*2);
                triangles.AddRange(new [] {v,v+2,v+1,v+1,v+2,v+3,v+1,v+2,v,v+3,v+2,v+1});
            }
            string path="Assets/Racing/Settings/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null) {mesh=new Mesh {name=name};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);
            var obj=new GameObject(name);obj.layer=8;obj.transform.SetParent(track.transform,false);obj.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        [MenuItem("Racing/Build macOS Training Player")]
        public static void BuildPlayer()
        {
            BuildPlayerAt(BuildPath);
        }
        [MenuItem("Racing/Build macOS Curriculum Player")]
        public static void BuildCurriculumPlayer()
        {
            BuildPlayerAt("Builds/macOS/RacingCurriculum.app");
        }
        static void BuildPlayerAt(string buildPath)
        {
            PlayerSettings.runInBackground=true;
            Directory.CreateDirectory("Builds/macOS");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new [] {ScenePath},locationPathName=buildPath,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development
            });
            if(report.summary.result!=BuildResult.Succeeded) throw new System.InvalidOperationException("Training build failed: "+report.summary.result);
            Debug.Log("TRAINING_BUILD_SUCCESS: "+Path.GetFullPath(buildPath));
        }
    }
}
