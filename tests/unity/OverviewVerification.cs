using System;
using Racing;
using UnityEngine;
namespace Racing.Editor
{
    public static class OverviewVerification
    {
        public static void Run()
        {
            var holder=new GameObject("Overview diagnostic camera");var camera=holder.AddComponent<Camera>();
            try
            {
                foreach(float aspect in new[] {16f/9f,1f,9f/16f}) foreach(var size in new[] {new Vector3(80,0,500),new Vector3(500,0,80),new Vector3(100,10,100)})
                {
                    var bounds=new Bounds(new Vector3(15,0,-30),size);var rotation=Quaternion.Euler(62,0,0);
                    camera.fieldOfView=48;camera.aspect=aspect;camera.nearClipPlane=.1f;camera.farClipPlane=3000;
                    float distance=PortfolioOverview.FitDistance(bounds,rotation,camera.fieldOfView,aspect);
                    holder.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*distance,rotation);
                    foreach(var point in PortfolioOverview.Corners(bounds))
                    {
                        var p=camera.WorldToViewportPoint(point);
                        if(p.z<=0 || p.x<.0454f || p.x>.9546f || p.y<.0454f || p.y>.9546f)
                            throw new InvalidOperationException("Overview corner clipping: "+p+" aspect="+aspect);
                    }
                }
                var roadObject=new GameObject("Reset diagnostic road");
                try
                {
                    var road=roadObject.AddComponent<RaceTrack>();road.centerline=new[] {Vector3.zero,new Vector3(100,0,200)};
                    var overview=holder.AddComponent<PortfolioOverview>();overview.track=road;
                    overview.SendMessage("LateUpdate");var position=holder.transform.position;var rotation=holder.transform.rotation;
                    float fov=camera.fieldOfView,far=camera.farClipPlane;
                    for(int reset=0;reset<3;reset++)
                    {
                        holder.transform.SetPositionAndRotation(new Vector3(9,3,7),Quaternion.Euler(11,22,33));
                        camera.fieldOfView=70;camera.farClipPlane=800;
                        overview.SendMessage("LateUpdate");
                        if(holder.transform.position!=position||Quaternion.Angle(holder.transform.rotation,rotation)>.001f||camera.fieldOfView!=fov||camera.farClipPlane!=far)
                            throw new InvalidOperationException("Identical geometry reset moved observer pose/lens");
                    }
                }
                finally {UnityEngine.Object.DestroyImmediate(roadObject);}
                Debug.Log("OVERVIEW_VERIFICATION_SUCCESS: 9aspect/bounds cases all8corners within10%margin");
            }
            finally {UnityEngine.Object.DestroyImmediate(holder);}
        }
    }
}
