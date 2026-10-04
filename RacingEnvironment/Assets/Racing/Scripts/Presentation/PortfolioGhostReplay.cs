using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace Racing
{
    // Explicit recorded-pose presentation only. Ghosts contain transforms and renderers.
    public sealed class PortfolioGhostReplay : MonoBehaviour
    {
        [Serializable] public sealed class Run { public int steps; public string poses, policy_sha256, terminalReason; public bool interrupted; public string initialization; }
        [Serializable] public sealed class Manifest { public int seed; public string geometrySha256; public TrackParameters parameters; public float durationSeconds, terminalHoldSeconds; public bool originalSpawn; public Run[] runs; }
        struct Pose { public int tick; public float time, steering; public Vector3 position; public Quaternion rotation; public string terminal; public bool interrupted; }
        sealed class Ghost { public Run run; public Pose[] poses; public Transform root; public int cursor; public Color color; public Transform leftFront, rightFront; }
        readonly List<Ghost> ghosts=new List<Ghost>();
        readonly List<Material> materials=new List<Material>();
        Manifest manifest; RacingAgent agent; string output, directory; StreamWriter timeline;
        float wheelbase, frontTrackWidth;
        float started, clock, nextCapture; int frame; bool ready; GUIStyle style;
        static readonly Color[] Colors={new Color(.35f,.72f,.86f),new Color(.83f,.66f,.34f),new Color(.62f,.72f,.46f),new Color(.76f,.49f,.63f)};
        static string Arg(string[] args,string flag) { int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();string path=Arg(args,"--portfolio-replay");if(path==null)return;
            if(Application.isBatchMode||Array.IndexOf(args,"--racing-no-render")>=0)throw new InvalidOperationException("Replay requires rendered player");
            var view=new GameObject("Recorded checkpoint REPLAY").AddComponent<PortfolioGhostReplay>();
            view.manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path));view.directory=Path.GetDirectoryName(Path.GetFullPath(path));view.output=Arg(args,"--portfolio-capture");
            view.agent=FindFirstObjectByType<RacingAgent>();if(view.agent==null)throw new InvalidOperationException("Replay requires scene car");
            // All disabling is confined to this explicit replay flag; no normal driving path changes.
            foreach(var mode in FindObjectsByType<TrainingMode>(FindObjectsSortMode.None))mode.enabled=false;
            view.agent.enabled=false;view.agent.distances.enabled=false;view.agent.vehicle.enabled=false;
            view.agent.episode.keyboard.enabled=false;view.agent.episode.enabled=false;view.agent.episode.automaticStartReset=false;
            foreach(var behaviour in view.agent.vehicle.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
            view.agent.vehicle.Body.isKinematic=true;
            foreach(var collider in view.agent.vehicle.GetComponentsInChildren<Collider>())collider.enabled=false;
            view.StartCoroutine(view.Prepare());
        }
        IEnumerator Prepare()
        {
            // Other presentation install adds the shared formula meshes before cloning.
            yield return null;
            if(manifest.runs==null||manifest.runs.Length!=4||!manifest.originalSpawn||manifest.parameters==null||manifest.terminalHoldSeconds<0)throw new InvalidDataException("Replay requires four original-start recorded runs and geometry parameters");
            var track=agent.episode.track;var generator=track.GetComponent<ProceduralTrack>()??track.gameObject.AddComponent<ProceduralTrack>();
            generator.Configure(track,agent.episode,1,manifest.seed,manifest.parameters,0,1);
            if(generator.Record.geometrySha256!=manifest.geometrySha256)throw new InvalidDataException("Replay geometry hash mismatch");
            var wheels=agent.vehicle.GetComponent<WheelVisuals>();
            if(wheels==null||wheels.steeringPivots==null||wheels.steeringPivots.Length<4)throw new InvalidDataException("Replay requires formula wheel pivots");
            wheelbase=agent.vehicle.wheelbase;frontTrackWidth=wheels.frontTrackWidth;
            float maximum=0;Pose? first=null;
            for(int i=0;i<manifest.runs.Length;i++)
            {
                var run=manifest.runs[i];var poses=ReadPoses(Path.Combine(directory,run.poses),manifest.seed,manifest.geometrySha256);
                if(first.HasValue&&(Vector3.Distance(first.Value.position,poses[0].position)>.00001f||Quaternion.Angle(first.Value.rotation,poses[0].rotation)>.001f))throw new InvalidDataException("Replay starts differ");
                first=poses[0];var last=poses[poses.Length-1];
                if(last.terminal!=run.terminalReason||last.interrupted!=run.interrupted)throw new InvalidDataException("Replay terminal metadata mismatch");maximum=Mathf.Max(maximum,last.time);
                var root=new GameObject("Recorded checkpoint "+run.steps).transform;
                CloneRenderers(agent.vehicle.transform,root,Colors[i]);
                ghosts.Add(new Ghost {run=run,poses=poses,root=root,color=Colors[i],leftFront=FindClone(agent.vehicle.transform,wheels.steeringPivots[2],root),rightFront=FindClone(agent.vehicle.transform,wheels.steeringPivots[3],root)});
            }
            if(Mathf.Abs(maximum-manifest.durationSeconds)>.001f)throw new InvalidDataException("Replay duration must equal latest recorded terminal");
            foreach(var renderer in agent.vehicle.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            agent.episode.GetComponent<RaceHud>().enabled=false;agent.episode.chaseCamera.enabled=false;
            var camera=agent.episode.chaseCamera.GetComponent<Camera>();camera.enabled=true;
            var overview=camera.GetComponent<PortfolioOverview>()??camera.gameObject.AddComponent<PortfolioOverview>();overview.track=track;overview.output=output;
            if(output!=null){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"replay-manifest.json"),JsonUtility.ToJson(manifest,true));timeline=new StreamWriter(Path.Combine(output,"frames.csv"));timeline.WriteLine("frame,realtime_seconds,replay_seconds,final_hold");}
            started=Time.realtimeSinceStartup;clock=0;ApplyPoses();ready=true;StartCoroutine(Capture());
        }
        static Pose[] ReadPoses(string path,int seed,string geometry)
        {
            var lines=File.ReadAllLines(path);var poses=new List<Pose>();int sequence=-1;string task=null;
            for(int i=1;i<lines.Length;i++)
            {
                var c=lines[i].Split(',');if(c.Length!=17)throw new InvalidDataException("Unexpected pose schema");
                float F(int n){float x=float.Parse(c[n],CultureInfo.InvariantCulture);if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidDataException("Nonfinite pose");return x;}
                int tick=int.Parse(c[0],CultureInfo.InvariantCulture),seq=int.Parse(c[12],CultureInfo.InvariantCulture);
                if(int.Parse(c[11],CultureInfo.InvariantCulture)!=seed||c[13]!=geometry||(sequence>=0&&seq!=sequence))throw new InvalidDataException("Pose task mismatch");sequence=seq;
                if(task!=null&&task!=c[14])throw new InvalidDataException("Pose task hash differs");task=c[14];
                var pose=new Pose {tick=tick,time=F(1),steering=F(9),position=new Vector3(F(2),F(3),F(4)),rotation=new Quaternion(F(5),F(6),F(7),F(8)),terminal=c[15],interrupted=bool.Parse(c[16])};
                if(poses.Count==0&&(tick!=0||pose.time!=0))throw new InvalidDataException("Missing recorded tick zero");
                if(poses.Count>0&&(tick!=poses[poses.Count-1].tick+1||pose.time<=poses[poses.Count-1].time||poses[poses.Count-1].terminal!="None"))throw new InvalidDataException("Pose ticks must be contiguous until terminal");
                poses.Add(pose);
            }
            if(poses.Count<2||poses[poses.Count-1].terminal=="None")throw new InvalidDataException("Missing recorded terminal");return poses.ToArray();
        }
        static Transform FindClone(Transform originalRoot,Transform original,Transform cloneRoot)
        {
            var indices=new List<int>();
            for(var node=original;node!=originalRoot;node=node.parent)
            {
                if(node==null)throw new InvalidDataException("Wheel pivot must belong to source car");
                indices.Add(node.GetSiblingIndex());
            }
            var result=cloneRoot;for(int i=indices.Count-1;i>=0;i--)result=result.GetChild(indices[i]);return result;
        }
        float FrontWheelAngle(float angle,bool rightWheel)
        {
            // Same renderer-only Ackermann calculation as WheelVisuals; actual recorded steering input.
            if(Mathf.Abs(angle)<.01f)return 0;
            float radius=wheelbase/Mathf.Tan(Mathf.Abs(angle)*Mathf.Deg2Rad);
            bool inside=rightWheel==(angle>0);
            return Mathf.Sign(angle)*Mathf.Atan2(wheelbase,Mathf.Max(.1f,radius+(inside?-1:1)*frontTrackWidth/2))*Mathf.Rad2Deg;
        }
        void CloneRenderers(Transform source,Transform target,Color tint)
        {
            foreach(Transform child in source)
            {
                var copy=new GameObject(child.name).transform;copy.SetParent(target,false);copy.localPosition=child.localPosition;copy.localRotation=child.localRotation;copy.localScale=child.localScale;
                var filter=child.GetComponent<MeshFilter>();var renderer=child.GetComponent<MeshRenderer>();
                if(filter!=null&&renderer!=null&&renderer.enabled&&child.gameObject.activeInHierarchy)
                {
                    copy.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var visual=copy.gameObject.AddComponent<MeshRenderer>();
                    var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++){slots[i]=new Material(slots[i]);materials.Add(slots[i]);var baseColor=slots[i].HasProperty("_BaseColor")?slots[i].GetColor("_BaseColor"):Color.white;if(baseColor.r>baseColor.g*1.2f)slots[i].SetColor("_BaseColor",tint);}
                    visual.sharedMaterials=slots;visual.shadowCastingMode=renderer.shadowCastingMode;
                }
                CloneRenderers(child,copy,tint);
            }
        }
        void Update(){if(!ready||(output!=null&&frame==0))return;clock=Mathf.Min(Time.realtimeSinceStartup-started,manifest.durationSeconds+manifest.terminalHoldSeconds);ApplyPoses();}
        void ApplyPoses()
        {
            foreach(var ghost in ghosts)
            {
                var p=ghost.poses;while(ghost.cursor+1<p.Length&&p[ghost.cursor+1].time<=clock)ghost.cursor++;
                var a=p[ghost.cursor];var b=p[Mathf.Min(ghost.cursor+1,p.Length-1)];float blend=b.time>a.time?Mathf.Clamp01((clock-a.time)/(b.time-a.time)):0;
                ghost.root.SetPositionAndRotation(Vector3.Lerp(a.position,b.position,blend),Quaternion.Slerp(a.rotation,b.rotation,blend));
                float steering=Mathf.Lerp(a.steering,b.steering,blend);
                ghost.leftFront.localRotation=Quaternion.Euler(0,FrontWheelAngle(steering,false),0);
                ghost.rightFront.localRotation=Quaternion.Euler(0,FrontWheelAngle(steering,true),0);
            }
        }
        IEnumerator Capture()
        {
            while(output!=null)
            {
                yield return new WaitForEndOfFrame();if(Time.realtimeSinceStartup<nextCapture)continue;nextCapture=Time.realtimeSinceStartup+.05f;
                var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,$"frame-{frame:00000}.png"),image.EncodeToPNG());Destroy(image);
                timeline.WriteLine(FormattableString.Invariant($"{frame},{Time.realtimeSinceStartup:R},{clock:R},{clock>manifest.durationSeconds}"));timeline.Flush();if(frame==0)started=Time.realtimeSinceStartup;frame++;
                if(clock>=manifest.durationSeconds+manifest.terminalHoldSeconds){Debug.Log("RACING_REPLAY_COMPLETE");Application.Quit(0);yield break;}
            }
        }
        void OnGUI()
        {
            if(!ready)return;if(style==null)style=new GUIStyle(GUI.skin.label){fontSize=15};
            var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*(Screen.width/960f));
            GUI.color=new Color(.035f,.06f,.085f,.95f);GUI.DrawTexture(new Rect(10,10,940,78),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(24,14,920,24),"REPLAY / independent recorded runs replayed together",style);
            GUI.Label(new Rect(24,36,920,24),$"Fixed-policy initialization: preserved trunk / mean   |   {Mathf.Min(clock,manifest.durationSeconds):0.0} simulated seconds   |   1x",style);
            GUI.Label(new Rect(24,58,920,24),"Same compact route and original start / terminal cars hold their recorded final pose",style);
            for(int i=0;i<ghosts.Count;i++)
            {
                var ghost=ghosts[i];var last=ghost.poses[ghost.poses.Length-1];bool ended=clock>=last.time;
                GUI.color=ghost.color;GUI.Label(new Rect(20+i*235,Screen.height/(Screen.width/960f)-36,235,30),$"{ghost.run.steps:N0} steps / "+(ended?(last.terminal=="Finish"?"FINISH":"FAIL: "+last.terminal):"running"),style);
                var camera=agent.episode.chaseCamera.GetComponent<Camera>();var screen=camera.WorldToScreenPoint(ghost.root.position);
                bool overlapping=false;for(int j=0;j<ghosts.Count;j++)if(i!=j&&Vector3.Distance(ghost.root.position,ghosts[j].root.position)<12)overlapping=true;
                if(!overlapping)GUI.Label(new Rect(screen.x/(Screen.width/960f)+8,(Screen.height-screen.y)/(Screen.width/960f),160,22),ghost.run.steps.ToString("N0")+(ended?(last.terminal=="Finish"?" / FINISH":" / FAIL"):""),style);
            }
            GUI.color=Color.white;GUI.matrix=old;
        }
        void OnDestroy(){timeline?.Dispose();foreach(var material in materials)Destroy(material);}
    }
}
