using System;
using System.IO;
using UnityEngine;
namespace Racing.Editor
{
    public static class CompactGeometryExport
    {
        [Serializable] sealed class Records { public TrackRecord[] tracks; public int auditedSeeds; public bool bothDirections; public float auditWallSeconds,defaultSeedWallSeconds; public TrackRecord defaultSeed; }
        static void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException("Compact geometry: "+message); }
        public static void Run()
        {
            var watch=System.Diagnostics.Stopwatch.StartNew(); var tracks=new TrackRecord[8]; bool left=false,right=false;
            for(int i=0;i<40;i++)
            {
                int seed=i<8?50000+i:40000+i-8;
                var record=ProceduralTrack.Generate(seed,new TrackParameters(),layout:1);
                var repeat=ProceduralTrack.Generate(seed,new TrackParameters(),layout:1);
                Check(record.geometrySha256==repeat.geometrySha256,"determinism seed="+seed);
                Check(record.generatorVersion=="compact-circuit-v1","version");
                Check(ProceduralTrack.IsValid(record.centerline,record.cumulativeMetres,15),"clearance");
                var bounds=new Bounds(record.leftBoundary[0],Vector3.zero);
                foreach(var p in record.leftBoundary) bounds.Encapsulate(p);
                foreach(var p in record.rightBoundary) bounds.Encapsulate(p);
                float small=Mathf.Min(bounds.size.x,bounds.size.z),large=Mathf.Max(bounds.size.x,bounds.size.z);
                Check(large/small<=1.60001f&&large/record.length<=.40001f,"compact bounds");
                Check(record.spawnDistance==4&&record.finishDistance==record.length-6,"open cap clearance");
                foreach(float radius in record.turnRadii) Check(radius>=22,"minimum radius");
                left|=record.turnAngles[2]<0;right|=record.turnAngles[2]>0;
                if(i<8) tracks[i]=record;
            }
            Check(left&&right,"both directions");
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--racing-compact-geometry-output");
            if(index<0||index+1>=args.Length) throw new ArgumentException("Explicit geometry output required");
            string output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
            float auditSeconds=(float)watch.Elapsed.TotalSeconds;watch.Restart();
            var defaultRecord=ProceduralTrack.Generate(1009,new TrackParameters(),layout:1);
            File.WriteAllText(output,JsonUtility.ToJson(new Records{tracks=tracks,auditedSeeds=40,bothDirections=true,auditWallSeconds=auditSeconds,defaultSeedWallSeconds=(float)watch.Elapsed.TotalSeconds,defaultSeed=defaultRecord},true));
            Debug.Log("COMPACT_GEOMETRY_EXPORT_SUCCESS: "+output);
        }
    }
}
