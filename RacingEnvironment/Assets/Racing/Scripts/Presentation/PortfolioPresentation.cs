using System;
using System.Collections;
using System.Collections.Generic;
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
        GUIStyle titleStyle, detailStyle, metricStyle;
        static readonly Color PanelColor=new Color(.035f,.06f,.085f,.94f);
        static readonly Color AccentColor=new Color(.2f,.85f,.92f);
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
            var agent=FindFirstObjectByType<RacingAgent>();if(agent==null) return;
            AddCarDetails(agent.transform);
            var backdrop=new GameObject("Procedural backdrop presentation").AddComponent<ProceduralBackdrop>();
            backdrop.track=agent.episode.track;
            if(Array.IndexOf(args,"--portfolio-replay")>=0)
            {
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(light.type==LightType.Directional) { light.shadows=LightShadows.Soft;light.shadowStrength=.7f; }
                return;
            }
            string cameraMode=Arg(args,"--portfolio-camera")??"chase";
            if(cameraMode!="chase"&&cameraMode!="overview") throw new ArgumentException("Unknown portfolio camera: "+cameraMode);
            if(cameraMode=="overview")
            {
                var overview=agent.episode.chaseCamera.gameObject.AddComponent<PortfolioOverview>();
                overview.track=agent.episode.track;overview.output=Arg(args,"--portfolio-capture");
                agent.episode.chaseCamera.enabled=false;
            }
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional) { light.shadows=LightShadows.Soft;light.shadowStrength=.7f; }
            bool rays=Array.IndexOf(args,"--portfolio-rays")>=0;
            string output=Arg(args,"--portfolio-capture"),label=Arg(args,"--portfolio-label");
            if(!rays&&output==null&&label==null) return;
            var view=new GameObject("Portfolio presentation (opt-in)").AddComponent<PortfolioPresentation>();
            view.agent=agent;view.sensor=agent.distances;view.rays=rays;view.output=output;view.label=label??"SAC policy";
            var chase=agent.episode.chaseCamera;
            chase.downwardAngle=rays ? 65 : 35;chase.distance=rays ? 48 : 22;
            chase.fieldOfView=rays ? 65 : 48;chase.lookAhead=rays ? 10 : 4;
            chase.speedFovBoost=0;if(cameraMode=="chase") chase.Snap();
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
            // The capture runner owns duration: retain the complete route and terminal hold.
            bool started=false;
            while(output!=null)
            {
                yield return new WaitForEndOfFrame();
                if(!started) started=agent.ElapsedSeconds>=.1f;
                if(!started||Time.realtimeSinceStartup<nextCapture) continue;
                nextCapture=Time.realtimeSinceStartup+.05f;
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(output,$"frame-{frames:00000}.png"),image.EncodeToPNG());Destroy(image);
                timeline.WriteLine(FormattableString.Invariant($"{frames},{Time.realtimeSinceStartup:R},{agent.PhysicsTicks},{agent.ElapsedSeconds:R},{agent.vehicle.Body.linearVelocity.magnitude:R}"));timeline.Flush();frames++;
            }
        }
        void OnGUI()
        {
            if(titleStyle==null)
            {
                titleStyle=new GUIStyle(GUI.skin.label) {fontSize=20,fontStyle=FontStyle.Bold,clipping=TextClipping.Clip};
                titleStyle.normal.textColor=new Color(.94f,.97f,.98f);
                detailStyle=new GUIStyle(GUI.skin.label) {fontSize=14};
                detailStyle.normal.textColor=new Color(.66f,.78f,.84f);
                metricStyle=new GUIStyle(detailStyle) {fontSize=17};
                metricStyle.normal.textColor=new Color(.94f,.97f,.98f);
            }
            // A reference canvas keeps labels readable in both video and smaller previews.
            float scale=Screen.width/960f;
            float width=Screen.width/scale,height=Screen.height/scale;
            var previousMatrix=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            DrawPanel(new Rect(12,10,width-24,38),PanelColor);
            DrawPanel(new Rect(12,10,3,38),AccentColor);
            string heading="RECURRENT RACER  /  "+label;
            titleStyle.fontSize=20;
            while(titleStyle.fontSize>12&&titleStyle.CalcSize(new GUIContent(heading)).x>width-52) titleStyle.fontSize--;
            GUI.Label(new Rect(26,14,width-52,28),heading,titleStyle);
            DrawPanel(new Rect(12,height-46,424,36),PanelColor);
            DrawPanel(new Rect(12,height-46,3,36),AccentColor);
            GUI.Label(new Rect(26,height-42,390,28),$"{agent.vehicle.SpeedKmh:0} km/h   |   {agent.ElapsedSeconds:0.0} s   |   {agent.LastActions.x:+0.00;-0.00;0.00} steer",metricStyle);
            if(rays) GUI.Label(new Rect(452,height-40,width-466,26),"11 rays / 40 m  |  cyan hit / amber clear",detailStyle);
            GUI.matrix=previousMatrix;
        }
        static void DrawPanel(Rect rect,Color color)
        {
            var previousColor=GUI.color;GUI.color=color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previousColor;
        }
        // Render-only additions: no primitives, colliders or changes to existing car children.
        static void AddCarDetails(Transform car)
        {
            var visuals=car.Find("Visuals");if(visuals==null||visuals.Find("Arcade aero details")!=null) return;
            var body=visuals.Find("Tapered body");var stripe=visuals.Find("White center stripe");
            if(body==null||stripe==null) return;
            var bounds=body.GetComponent<MeshFilter>().sharedMesh.bounds;
            float w=bounds.size.x,l=bounds.size.z;
            var red=body.GetComponent<MeshRenderer>().sharedMaterial;
            var white=stripe.GetComponent<MeshRenderer>().sharedMaterial;
            var root=new GameObject("Arcade aero details").transform;root.SetParent(visuals,false);
            // Replace bulky visual lofts with a slender formula-car fuselage and stepped nose.
            body.GetComponent<MeshRenderer>().enabled=false;stripe.GetComponent<MeshRenderer>().enabled=false;
            var cockpit=visuals.Find("Cockpit bulge");
            if(cockpit!=null) cockpit.GetComponent<MeshRenderer>().enabled=false;
            var dark=cockpit==null ? red : cockpit.GetComponent<MeshRenderer>().sharedMaterial;
            DetailMesh("Formula fuselage",root,Vector3.zero,new [] {
                new Vector4(-l*.45f,w*.18f,-w*.16f,w*.08f),
                new Vector4(-l*.20f,w*.27f,-w*.16f,w*.15f),
                new Vector4(l*.12f,w*.20f,-w*.12f,w*.13f),
                new Vector4(l*.37f,w*.10f,-w*.065f,w*.015f),
                new Vector4(l*.48f,w*.085f,-w*.05f,-w*.01f)
            },red);
            DetailMesh("Ivory nose stripe",root,Vector3.zero,new [] {
                new Vector4(l*.10f,w*.035f,w*.132f,w*.14f),
                new Vector4(l*.37f,w*.03f,w*.018f,w*.026f),
                new Vector4(l*.48f,w*.028f,-w*.006f,w*.002f)
            },white);
            DetailMesh("Faceted cockpit",root,Vector3.zero,new [] {
                new Vector4(-l*.24f,w*.16f,w*.12f,w*.19f),
                new Vector4(-l*.15f,w*.14f,w*.12f,w*.32f),
                new Vector4(-l*.02f,w*.12f,w*.12f,w*.30f),
                new Vector4(l*.09f,w*.10f,w*.12f,w*.15f)
            },dark);
            DetailMesh("Front wing",root,Vector3.zero,new [] {
                new Vector4(l*.39f,w*.64f,-w*.19f,-w*.155f),
                new Vector4(l*.48f,w*.60f,-w*.17f,-w*.135f)
            },white);
            // Angular sidepods give the open-wheel racer a broader, stepped silhouette.
            for(int side=-1;side<=1;side+=2)
            {
                DetailMesh("Faceted sidepod",root,new Vector3(side*w*.35f,0,0),new [] {
                    new Vector4(-l*.24f,w*.07f,-w*.13f,w*.025f),
                    new Vector4(-l*.15f,w*.16f,-w*.13f,w*.055f),
                    new Vector4(l*.07f,w*.16f,-w*.11f,w*.055f),
                    new Vector4(l*.17f,w*.08f,-w*.09f,w*.005f)
                },red);
                DetailMesh("Front wing endplate",root,new Vector3(side*w*.61f,0,0),new [] {
                    new Vector4(l*.38f,w*.025f,-w*.20f,-w*.06f),
                    new Vector4(l*.49f,w*.025f,-w*.20f,-w*.06f)
                },red);
                DetailMesh("Rear wing endplate",root,new Vector3(side*w*.49f,0,0),new [] {
                    new Vector4(-l*.46f,w*.025f,w*.20f,w*.39f),
                    new Vector4(-l*.34f,w*.025f,w*.20f,w*.39f)
                },red);
                DetailMesh("Rear wing support",root,new Vector3(side*w*.24f,0,0),new [] {
                    new Vector4(-l*.41f,w*.02f,w*.015f,w*.30f),
                    new Vector4(-l*.38f,w*.02f,w*.015f,w*.30f)
                },white);
            }
            DetailMesh("Low-poly rear wing",root,Vector3.zero,new [] {
                new Vector4(-l*.45f,w*.49f,w*.30f,w*.335f),
                new Vector4(-l*.35f,w*.49f,w*.28f,w*.315f)
            },white);
        }
        static void DetailMesh(string name,Transform parent,Vector3 position,Vector4[] sections,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var rings=new Vector3[sections.Length][];
            for(int i=0;i<sections.Length;i++)
            {
                var s=sections[i];rings[i]=new [] {new Vector3(-s.y,s.z,s.x),new Vector3(-s.y,s.w,s.x),new Vector3(s.y,s.w,s.x),new Vector3(s.y,s.z,s.x)};
            }
            Vector3[] outward={Vector3.left,Vector3.up,Vector3.right,Vector3.down};
            for(int i=0;i<rings.Length-1;i++) for(int face=0;face<4;face++)
                DetailQuad(vertices,triangles,rings[i][face],rings[i][(face+1)%4],rings[i+1][(face+1)%4],rings[i+1][face],outward[face]);
            DetailQuad(vertices,triangles,rings[0][0],rings[0][1],rings[0][2],rings[0][3],Vector3.back);
            int last=rings.Length-1;
            DetailQuad(vertices,triangles,rings[last][0],rings[last][1],rings[last][2],rings[last][3],Vector3.forward);
            var mesh=new Mesh {name=name,hideFlags=HideFlags.DontSave};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            obj.AddComponent<PresentationMeshLifetime>();
        }
        static void DetailQuad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
        {
            int n=vertices.Count;vertices.AddRange(new [] {a,b,c,d});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)>0) triangles.AddRange(new [] {n,n+1,n+2,n,n+2,n+3});
            else triangles.AddRange(new [] {n,n+2,n+1,n,n+3,n+2});
        }
        void OnDestroy()
        {
            if(sensor!=null) sensor.Sampled-=OnSampled;
            timeline?.Dispose();samples?.Dispose();
        }
    }
    // Release transient visual meshes on scene teardown; no simulation callbacks.
    public sealed class PortfolioOverview : MonoBehaviour
    {
        public RaceTrack track;
        public string output;
        Camera lens;
        string geometry;
        StreamWriter metadata;
        Vector3 fixedPosition;
        Quaternion fixedRotation;
        float fixedFarClip;
        // Pure perspective fit: all eight bounding-box corners have a 10% framing margin.
        public static float FitDistance(Bounds bounds,Quaternion rotation,float verticalFov,float aspect)
        {
            float tanY=Mathf.Tan(verticalFov*Mathf.Deg2Rad*.5f),tanX=tanY*Mathf.Max(.01f,aspect);
            float distance=1;
            foreach(var corner in Corners(bounds))
            {
                var local=Quaternion.Inverse(rotation)*(corner-bounds.center);
                distance=Mathf.Max(distance,1.1f*Mathf.Abs(local.x)/tanX-local.z,1.1f*Mathf.Abs(local.y)/tanY-local.z);
            }
            return distance;
        }
        public static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                yield return bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z));
        }
        void LateUpdate()
        {
            if(track==null||track.centerline==null||track.centerline.Length==0) return;
            if(lens==null) lens=GetComponent<Camera>();
            var generator=track.GetComponent<ProceduralTrack>();var record=generator==null ? null : generator.Record;
            string key=record==null ? "fixed" : record.geometrySha256;
            if(geometry==key)
            {
                // Episode reset may call ChaseCamera.Snap even while that component is disabled.
                transform.SetPositionAndRotation(fixedPosition,fixedRotation);
                lens.fieldOfView=48;lens.farClipPlane=fixedFarClip;
                return;
            }
            var bounds=new Bounds(track.centerline[0],Vector3.zero);
            foreach(var point in track.centerline) bounds.Encapsulate(point);
            if(record!=null)
            {
                foreach(var point in record.leftBoundary) bounds.Encapsulate(point);
                foreach(var point in record.rightBoundary) bounds.Encapsulate(point);
            }
            else bounds.Expand(new Vector3(track.roadWidth+track.curbWidth*2,0,track.roadWidth+track.curbWidth*2));
            // Fixed elevated observer; no reads from the vehicle and no follow motion.
            var rotation=Quaternion.Euler(62,0,0);lens.fieldOfView=48;
            float distance=FitDistance(bounds,rotation,lens.fieldOfView,lens.aspect);
            fixedPosition=bounds.center-rotation*Vector3.forward*distance;fixedRotation=rotation;
            transform.SetPositionAndRotation(fixedPosition,fixedRotation);
            lens.farClipPlane=Mathf.Max(1000,distance+bounds.size.magnitude+200);
            fixedFarClip=lens.farClipPlane;
            geometry=key;
            if(output!=null)
            {
                if(metadata==null)
                {
                    Directory.CreateDirectory(output);metadata=new StreamWriter(Path.Combine(output,"camera.csv"));
                    metadata.WriteLine("geometry,camera,x,y,z,pitch,yaw,vertical_fov,aspect,bounds_min_x,bounds_min_z,bounds_max_x,bounds_max_z,margin");
                }
                var p=transform.position;
                metadata.WriteLine(FormattableString.Invariant($"{key},overview,{p.x:R},{p.y:R},{p.z:R},62,0,{lens.fieldOfView:R},{lens.aspect:R},{bounds.min.x:R},{bounds.min.z:R},{bounds.max.x:R},{bounds.max.z:R},1.1"));metadata.Flush();
            }
        }
        void OnDestroy() { metadata?.Dispose(); }
    }
    // Release transient visual meshes on scene teardown; no simulation callbacks.
    sealed class PresentationMeshLifetime : MonoBehaviour
    {
        void OnDestroy() { Destroy(GetComponent<MeshFilter>().sharedMesh); }
    }
    // Python config arrives after scene load. Observe completed geometry in rendered frames only.
    sealed class ProceduralBackdrop : MonoBehaviour
    {
        public RaceTrack track;
        ProceduralTrack generator;
        Mesh mesh;
        MeshRenderer backdrop;
        string geometry;
        void LateUpdate()
        {
            if(generator==null) generator=track.GetComponent<ProceduralTrack>();
            var record=generator==null ? null : generator.Record;
            if(record==null) { if(backdrop!=null) backdrop.enabled=false;geometry=null;return; }
            if(geometry==record.geometrySha256) return;
            if(backdrop==null)
            {
                var ground=GameObject.Find("Ground");
                var renderer=ground==null ? null : ground.GetComponent<MeshRenderer>();
                if(renderer==null) return;
                mesh=new Mesh {name="Procedural visual ground",hideFlags=HideFlags.DontSave};
                gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                backdrop=gameObject.AddComponent<MeshRenderer>();backdrop.sharedMaterial=renderer.sharedMaterial;
                backdrop.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                // Below both road and fixed ground: no z-fighting or change to existing geometry.
                transform.position=Vector3.up*Mathf.Min(renderer.bounds.max.y-.05f,record.centerline[0].y-.05f);
            }
            var bounds=new Bounds(record.centerline[0],Vector3.zero);
            foreach(var point in record.leftBoundary) bounds.Encapsulate(point);
            foreach(var point in record.rightBoundary) bounds.Encapsulate(point);
            // Scale visual grass with the whole-route observer's view on larger routes.
            float margin=Mathf.Max(120,2*bounds.size.magnitude);
            float x0=bounds.min.x-margin,x1=bounds.max.x+margin,z0=bounds.min.z-margin,z1=bounds.max.z+margin;
            mesh.Clear();mesh.vertices=new [] {new Vector3(x0,0,z0),new Vector3(x0,0,z1),new Vector3(x1,0,z1),new Vector3(x1,0,z0)};
            mesh.triangles=new [] {0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
            backdrop.enabled=true;geometry=record.geometrySha256;
        }
        void OnDestroy() { if(mesh!=null) Destroy(mesh); }
    }
}
