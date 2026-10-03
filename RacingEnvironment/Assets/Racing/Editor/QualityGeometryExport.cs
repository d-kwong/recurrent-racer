using System;
using System.IO;
using UnityEngine;
namespace Racing.Editor
{
    public static class QualityGeometryExport
    {
        [Serializable] sealed class Records {public TrackRecord[] tracks;}
        public static void Run()
        {
            var tracks=new TrackRecord[8];
            for(int i=0;i<tracks.Length;i++) tracks[i]=ProceduralTrack.Generate(20000+i,new TrackParameters());
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--racing-quality-geometry-output");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Explicit geometry output required");
            string output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,JsonUtility.ToJson(new Records {tracks=tracks},true));
            Debug.Log("QUALITY_GEOMETRY_EXPORT_SUCCESS: "+output);
        }
    }
}
