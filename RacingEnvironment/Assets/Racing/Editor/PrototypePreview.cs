using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Racing.Editor
{
    public static class PrototypePreview
    {
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/Racing/Scenes/ManualRacer.unity");
            var camera = UnityEngine.Object.FindFirstObjectByType<ChaseCamera>();
            camera.Snap(); camera.GetComponent<Camera>().fieldOfView = camera.fieldOfView;
            Save(camera.GetComponent<Camera>(), "/tmp/racer-warmup.png");
            Save(camera.GetComponent<Camera>(), "/tmp/racer-driving-preview.png");
            camera.GetComponent<Camera>().fieldOfView = camera.DesiredFieldOfView(camera.fovFullSpeed);
            Save(camera.GetComponent<Camera>(), "/tmp/racer-speed-preview.png");
            camera.GetComponent<Camera>().fieldOfView = 40;
            camera.transform.position = camera.target.position - camera.target.forward*8 + camera.target.right*8 + Vector3.up*3;
            camera.transform.rotation = Quaternion.LookRotation(camera.target.position-camera.transform.position,Vector3.up);
            Save(camera.GetComponent<Camera>(), "/tmp/racer-car-profile.png");
            camera.transform.position = new Vector3(0,180,0);
            camera.transform.rotation = Quaternion.Euler(90,0,0);
            var lens = camera.GetComponent<Camera>(); lens.orthographic = true; lens.orthographicSize = 90;
            Save(lens, "/tmp/racer-track-preview.png");
        }
        static void Save(Camera camera, string path)
        {
            var rt = new RenderTexture(1280,720,24); rt.Create();
            camera.aspect = 1280f / 720f; camera.ResetProjectionMatrix();
            Debug.Log($"Capture camera pose {camera.transform.position} {camera.transform.eulerAngles} FOV {camera.fieldOfView}");
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt;
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            Debug.Log("Preview saved: "+path);
        }
    }
}
