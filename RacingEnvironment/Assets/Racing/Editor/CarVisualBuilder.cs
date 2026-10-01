using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Racing.Editor
{
    public static class CarVisualBuilder
    {
        const string Root = "Assets/Racing/";
        public static void Build(GameObject car, PrototypeSettings settings, Material red, Material white, Material black)
        {
            var previous = car.transform.Find("Visuals"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            EditorUtility.SetDirty(settings);
            var vehicle = car.GetComponent<ArcadeVehicle>();
            var animation = car.GetComponent<WheelVisuals>(); if (animation == null) animation = car.AddComponent<WheelVisuals>();
            animation.vehicle = vehicle; animation.steeringPivots = new Transform[4]; animation.tires = new Transform[4];
            float w = settings.carWidth, h = settings.carHeight, l = settings.carLength;
            float wheelCenter = w/2 + settings.wheelGap + settings.wheelWidth/2;
            float wheelY = settings.wheelVerticalOffset;
            animation.wheelRadius = settings.wheelDiameter/2; animation.frontTrackWidth = wheelCenter*2;
            var visuals = new GameObject("Visuals").transform; visuals.SetParent(car.transform,false);
            // Section = z, half width, lower y, upper y. Flat faces keep the low-poly style.
            Vector4[] sections = {
                new Vector4(-l/2,w*0.36f,-h*0.32f,h*0.06f),
                new Vector4(-l*0.22f,w/2,-h/2,h*0.16f),
                new Vector4(l*0.2f,w*0.46f,-h*0.38f,h*0.16f),
                new Vector4(l/2,w*settings.noseWidthRatio/2,-h*0.23f,-h*0.05f)
            };
            Loft("Tapered body",visuals,sections,red);
            Vector4[] stripe = (Vector4[])sections.Clone();
            for (int i=0;i<stripe.Length;i++) { stripe[i].y = w*0.04f; stripe[i].z = stripe[i].w+0.006f; stripe[i].w += 0.015f; }
            Loft("White center stripe",visuals,stripe,white);
            var cockpit = Material("Cockpit",new Color(0.055f,0.08f,0.11f));
            Loft("Cockpit bulge",visuals,new [] {
                new Vector4(-l*0.25f+settings.cockpitLongitudinalOffset,w*0.24f,h*0.16f,h*0.18f),
                new Vector4(-l*0.13f+settings.cockpitLongitudinalOffset,w*0.21f,h*0.16f,h*settings.cockpitHeightRatio),
                new Vector4(l*0.06f+settings.cockpitLongitudinalOffset,w*0.20f,h*0.16f,h*settings.cockpitHeightRatio),
                new Vector4(l*0.23f+settings.cockpitLongitudinalOffset,w*0.24f,h*0.16f,h*0.18f)
            },cockpit);
            var tireMesh = TireMesh();
            for (int axle = 0; axle < 2; axle++)
            {
                int end = axle == 0 ? -1 : 1;
                var shaft = Part("Axle",visuals,PrimitiveType.Cylinder,new Vector3(0,wheelY,end*l*0.34f),new Vector3(0.09f,wheelCenter,0.09f),white);
                shaft.localRotation = Quaternion.Euler(0,0,90);
                for (int sideIndex=0;sideIndex<2;sideIndex++)
                {
                    int side = sideIndex == 0 ? -1 : 1; int index= axle*2+sideIndex;
                    var pivot = new GameObject((axle==0 ? "Rear" : "Front") + (side<0 ? " left steering pivot" : " right steering pivot")).transform;
                    pivot.SetParent(visuals,false); pivot.localPosition = new Vector3(side*wheelCenter,wheelY,end*l*0.34f);
                    animation.steeringPivots[index] = pivot;
                    var tire = new GameObject("Beveled tire"); tire.transform.SetParent(pivot,false);
                    tire.transform.localScale = new Vector3(settings.wheelDiameter,settings.wheelWidth/2,settings.wheelDiameter);
                    tire.AddComponent<MeshFilter>().sharedMesh = tireMesh; tire.AddComponent<MeshRenderer>().sharedMaterial = black;
                    animation.tires[index] = tire.transform;
                    animation.tires[index].localRotation = Quaternion.Euler(0,0,90);
                    Vector3 bodyMount = new Vector3(side*w*0.46f,h*0.1f,end*l*0.34f);
                    Vector3 axleMount = new Vector3(side*(wheelCenter-settings.wheelWidth/2),wheelY,end*l*0.34f);
                    Vector3 supportDirection = bodyMount-axleMount;
                    var support = Part("Angled support",visuals,PrimitiveType.Cube,(bodyMount+axleMount)/2,new Vector3(0.08f,supportDirection.magnitude,0.08f),white);
                    support.localRotation = Quaternion.FromToRotation(Vector3.up,supportDirection);
                }
            }
            var marks = car.GetComponent<DriftMarks>(); if (marks == null) marks = car.AddComponent<DriftMarks>();
            marks.vehicle = vehicle; marks.rearTires = new [] {animation.steeringPivots[0],animation.steeringPivots[1]};
            marks.material = Material("Tire marks",new Color(0.055f,0.055f,0.055f),true);
        }
        static Mesh TireMesh()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            const int sides = 32;
            float[] heights = {-1,-0.72f,0.72f,1}; float[] radii = {0.42f,0.5f,0.5f,0.42f};
            for (int side=0;side<sides;side++)
            {
                float a = side*2*Mathf.PI/sides, b=(side+1)*2*Mathf.PI/sides;
                Vector3 radialA = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 radialB = new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                for (int ring=0;ring<3;ring++)
                    Quad(vertices,triangles,radialA*radii[ring]+Vector3.up*heights[ring],radialB*radii[ring]+Vector3.up*heights[ring],
                        radialB*radii[ring+1]+Vector3.up*heights[ring+1],radialA*radii[ring+1]+Vector3.up*heights[ring+1],radialA+radialB);
                Quad(vertices,triangles,Vector3.down,radialA*radii[0]+Vector3.down,radialB*radii[0]+Vector3.down,Vector3.down,Vector3.down);
                Quad(vertices,triangles,Vector3.up,radialA*radii[3]+Vector3.up,radialB*radii[3]+Vector3.up,Vector3.up,Vector3.up);
            }
            const string path=Root+"Settings/Sleek tire.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh==null) {mesh=new Mesh {name="Beveled tire"};AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static Material Material(string name, Color color, bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",0.1f); EditorUtility.SetDirty(m); return m;
        }
        static Transform Part(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type); obj.name=name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position; obj.transform.localScale=scale; obj.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); return obj.transform;
        }
        static void Loft(string name,Transform parent,Vector4[] sections,Material material)
        {
            var rings=new Vector3[sections.Length][];
            for(int i=0;i<sections.Length;i++)
            {
                var s=sections[i]; rings[i]=new [] {new Vector3(-s.y,s.z,s.x),new Vector3(-s.y,s.w,s.x),new Vector3(s.y,s.w,s.x),new Vector3(s.y,s.z,s.x)};
            }
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            // Each side has a known outward direction; duplicate vertices for faceted normals.
            Vector3[] directions={Vector3.left,Vector3.up,Vector3.right,Vector3.down};
            for(int i=0;i<rings.Length-1;i++) for(int face=0;face<4;face++)
                Quad(vertices,triangles,rings[i][face],rings[i][(face+1)%4],rings[i+1][(face+1)%4],rings[i+1][face],directions[face]);
            Quad(vertices,triangles,rings[0][0],rings[0][1],rings[0][2],rings[0][3],Vector3.back);
            int last=rings.Length-1; Quad(vertices,triangles,rings[last][0],rings[last][1],rings[last][2],rings[last][3],Vector3.forward);
            string path=Root+"Settings/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh==null) {mesh=new Mesh {name=name};AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
        {
            int n=vertices.Count;vertices.AddRange(new [] {a,b,c,d});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)>0) triangles.AddRange(new [] {n,n+1,n+2,n,n+2,n+3});
            else triangles.AddRange(new [] {n,n+2,n+1,n,n+3,n+2});
        }
    }
}
