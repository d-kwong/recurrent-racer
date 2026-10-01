using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Racing.Editor
{
    public static class DrivingUpgrade
    {
        public static void Apply() => ApplyChanges(true);
        public static void SleekArcade() => ApplyChanges(false, true);
        [MenuItem("Racing/Apply Slim Visual Profile")]
        public static void SlimProfile() => ApplyChanges(false, false, true);
        public static void AdjustWheels() => ApplyChanges(false);
        static void ApplyChanges(bool upgradeDriving, bool sleek = false, bool slim = false)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before applying a saved car profile."); return; }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/Racing/Scenes/ManualRacer.unity");
            var episode=Object.FindFirstObjectByType<RaceEpisode>(); var vehicle=episode.vehicle;
            var settings=AssetDatabase.LoadAssetAtPath<PrototypeSettings>("Assets/Racing/Settings/PrototypeSettings.asset");
            if (sleek)
            {
                settings.carLength=5.4f; settings.carHeight=0.95f; settings.noseWidthRatio=0.56f; settings.cockpitHeightRatio=0.52f;
                settings.wheelGap=0.14f; settings.wheelWidth=0.55f; settings.wheelDiameter=0.95f; settings.wheelVerticalOffset=-0.08f;
                vehicle.acceleration=12; vehicle.maximumSpeed=48; vehicle.drag=0.15f; episode.chaseCamera.fovFullSpeed=48;
                float spawnHeight=settings.wheelDiameter/2-settings.wheelVerticalOffset;
                var spawn=episode.track.spawn; spawn.position=new Vector3(spawn.position.x,spawnHeight,spawn.position.z);
                vehicle.transform.SetPositionAndRotation(spawn.position,spawn.rotation);
                vehicle.GetComponent<BoxCollider>().size=new Vector3(settings.collisionWidth,settings.carHeight,settings.carLength);
            }
            if (slim)
            {
                // Exact proportions from the preceding 2 m body and 2.83 m axle span.
                settings.carWidth=2f*2/3;
                settings.wheelGap=(2.83f*0.75f-settings.carWidth-settings.wheelWidth)/2;
                settings.cockpitLongitudinalOffset=-0.3f; settings.cockpitHeightRatio=0.52f;
                settings.collisionWidth=vehicle.GetComponent<BoxCollider>().size.x;
                episode.chaseCamera.downwardAngle=20;
            }
            CarVisualBuilder.Build(vehicle.gameObject,settings,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing/Materials/Red.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing/Materials/White.mat"),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing/Materials/Black.mat"));
            if (upgradeDriving)
            {
            vehicle.steeringLimit=22; vehicle.steeringResponse=1.5f; vehicle.speedSteeringReduction=0.025f;
            var camera=episode.chaseCamera;camera.vehicle=vehicle;camera.downwardAngle=25;camera.lookAhead=4;camera.speedFovBoost=10;camera.Snap();
            }
            episode.chaseCamera.Snap();
            // Preserve the existing track, manager, camera configuration, and remaining tuning.
            PrefabUtility.SaveAsPrefabAsset(vehicle.gameObject,"Assets/Racing/Prefabs/Car.prefab");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("CAR_VISUAL_UPDATE_SUCCESS");
        }
    }
}
