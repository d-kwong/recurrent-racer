using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace Racing
{
    // Opt-in human presentation. No actions, rewards, physics or clock settings.
    public sealed class PortfolioPresentation : MonoBehaviour
    {
        RacingAgent agent;
        RoadDistanceSensor sensor;
        bool rays;
        string output, label;
        LineRenderer[] lines;
        int frames;
        float nextCapture;
        StreamWriter timeline, samples;
        static string Arg(string[] args,string flag)
        {
            int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length ? args[i+1] : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args=Environment.GetCommandLineArgs();
            // Headless training explicitly excludes every presentation component.
            if(Array.IndexOf(args,"--racing-no-render")>=0||Application.isBatchMode) return;
            bool rays=Array.IndexOf(args,"--portfolio-rays")>=0;
            string output=Arg(args,"--portfolio-capture"),label=Arg(args,"--portfolio-label");
            if(!rays&&output==null&&label==null) return;
            var agent=FindFirstObjectByType<RacingAgent>();if(agent==null) return;
            var view=new GameObject("Portfolio presentation (opt-in)").AddComponent<PortfolioPresentation>();
            view.agent=agent;view.sensor=agent.distances;view.rays=rays;view.output=output;view.label=label??"SAC policy";
            var chase=agent.episode.chaseCamera;
            chase.downwardAngle=rays ? 65 : 35;chase.distance=rays ? 48 : 22;
            chase.fieldOfView=rays ? 65 : 48;chase.lookAhead=rays ? 10 : 4;
            chase.speedFovBoost=0;chase.Snap();
            // Replace the default game HUD only for captures, with a compact labelled presentation.
            agent.episode.GetComponent<RaceHud>().enabled=false;
            if(output!=null)
            {
                Directory.CreateDirectory(output);
                view.timeline=new StreamWriter(Path.Combine(output,"frames.csv"));
                view.timeline.WriteLine("frame,realtime_seconds,physics_ticks,episode_seconds,speed_mps");
                if(rays) { view.samples=new StreamWriter(Path.Combine(output,"ray-samples.csv"));view.samples.WriteLine("physics_ticks,ray,normalized_distance,hit,origin_x,origin_y,origin_z,direction_x,direction_y,direction_z"); }
            }
            if(rays)
            {
                view.lines=new LineRenderer[RoadDistanceSensor.RayCount];
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                for(int i=0;i<view.lines.Length;i++)
                {
                    var obj=new GameObject("Observed ray "+RoadDistanceSensor.Angles[i]);obj.transform.SetParent(view.transform);
                    var line=obj.AddComponent<LineRenderer>();line.positionCount=2;line.useWorldSpace=true;
                    line.startWidth=.11f;line.endWidth=.11f;line.material=new Material(shader);line.enabled=false;
                    view.lines[i]=line;
                }
                view.sensor.Sampled+=view.OnSampled;
            }
            view.StartCoroutine(view.Capture());
        }
        void OnSampled(Vector3 origin,Vector3[] directions,float[] values,bool[] hits)
        {
            for(int i=0;i<lines.Length;i++)
            {
                var color=hits[i] ? new Color(.1f,.9f,1) : new Color(1,.72f,.15f);
                lines[i].material.SetColor("_BaseColor",color);lines[i].startColor=color;lines[i].endColor=color;
                lines[i].SetPosition(0,origin);lines[i].SetPosition(1,origin+directions[i]*values[i]*sensor.rayLength);lines[i].enabled=true;
                if(samples!=null) samples.WriteLine(FormattableString.Invariant($"{agent.PhysicsTicks},{i},{values[i]:R},{hits[i]},{origin.x:R},{origin.y:R},{origin.z:R},{directions[i].x:R},{directions[i].y:R},{directions[i].z:R}"));
            }
        }
        IEnumerator Capture()
        {
            while(output!=null&&frames<500)
            {
                yield return new WaitForEndOfFrame();
                if(agent.LastActions.y==0||agent.ElapsedSeconds<.1f||Time.realtimeSinceStartup<nextCapture) continue;
                nextCapture=Time.realtimeSinceStartup+.05f;
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output,$"frame-{frames:00000}.png"),image.EncodeToPNG());Destroy(image);
                timeline.WriteLine(FormattableString.Invariant($"{frames},{Time.realtimeSinceStartup:R},{agent.PhysicsTicks},{agent.ElapsedSeconds:R},{agent.vehicle.Body.linearVelocity.magnitude:R}"));timeline.Flush();frames++;
            }
        }
        void OnGUI()
        {
            var title=new GUIStyle(GUI.skin.label) {fontSize=24,fontStyle=FontStyle.Bold};title.normal.textColor=Color.white;
            var detail=new GUIStyle(GUI.skin.label) {fontSize=17};detail.normal.textColor=new Color(.68f,.82f,.9f);
            GUI.Box(new Rect(12,12,Screen.width-24,86),"");
            GUI.Label(new Rect(30,23,Screen.width-60,32),"RECURRENT RACER  /  "+label,title);
            GUI.Label(new Rect(30,59,Screen.width-60,27),rays ? "Decision sensor snapshot  |  cyan: hit  /  amber: no hit  |  11 rays / 40 m" : "Numerical perception  /  continuous steering + signed throttle  /  fixed track",detail);
            GUI.Box(new Rect(16,Screen.height-60,400,42),"");
            GUI.Label(new Rect(30,Screen.height-54,380,30),$"{agent.vehicle.SpeedKmh:0} km/h   |   {agent.ElapsedSeconds:0.0} s   |   {agent.LastActions.x:+0.00;-0.00;0.00} steer",detail);
        }
        void OnDestroy()
        {
            if(sensor!=null) sensor.Sampled-=OnSampled;
            timeline?.Dispose();samples?.Dispose();
        }
    }
}
