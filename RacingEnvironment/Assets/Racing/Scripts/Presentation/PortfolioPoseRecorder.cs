using System;
using System.Globalization;
using System.IO;
using UnityEngine;
namespace Racing
{
    // Passive physics poses for offline collider-free checkpoint replay.
    public sealed class PortfolioPoseRecorder : MonoBehaviour
    {
        RacingAgent agent; StreamWriter writer,observations; int observationIndex;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"--portfolio-poses");
            if(i<0) return;
            if(i+1>=args.Length) throw new ArgumentException("Pose output path required");
            var agent=FindFirstObjectByType<RacingAgent>(); if(agent==null) throw new InvalidOperationException("Pose recorder requires agent");
            string path=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(Path.GetDirectoryName(path));
            var recorder=agent.gameObject.AddComponent<PortfolioPoseRecorder>();recorder.agent=agent;
            recorder.writer=new StreamWriter(path);
            recorder.writer.WriteLine("tick,simulated_seconds,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,rotation_w,steering_degrees,speed_metres_per_second,seed,sequence_index,geometry_sha256,task_sha256,terminal_reason,interrupted");
            agent.PoseSampled+=recorder.Record;
            recorder.observations=new StreamWriter(path+".observations.csv");
            recorder.observations.Write("sample_index,physics_ticks,episode_seconds,seed,geometry_sha256,task_sha256");
            for(int column=0;column<15;column++) recorder.observations.Write(",observation_"+column);
            recorder.observations.WriteLine();
            agent.ObservationSampled+=recorder.RecordObservation;
        }
        void RecordObservation(int tick,float seconds,float[] values)
        {
            var generator=agent.episode.track.GetComponent<ProceduralTrack>(); var record=generator==null ? null : generator.Record;
            observations.Write((observationIndex++).ToString()+","+tick+","+F(seconds)+","+(record==null?-1:record.seed)+","+(record==null?"":record.geometrySha256)+","+(record==null?"":record.taskSha256));
            foreach(float value in values) observations.Write(","+F(value));
            observations.WriteLine();
        }
        static string F(float value) { return value.ToString("R",CultureInfo.InvariantCulture); }
        void Record(RacingPoseSample sample)
        {
            writer.WriteLine(string.Join(",",new[]{sample.tick.ToString(),F(sample.simulatedSeconds),F(sample.position.x),F(sample.position.y),F(sample.position.z),F(sample.rotation.x),F(sample.rotation.y),F(sample.rotation.z),F(sample.rotation.w),F(sample.steeringDegrees),F(sample.speedMetresPerSecond),sample.seed.ToString(),sample.sequenceIndex.ToString(),sample.geometrySha256,sample.taskSha256,sample.terminalReason,sample.interrupted?"true":"false"}));
            if(sample.terminalReason!="None") writer.Flush();
        }
        void OnDestroy() { if(agent!=null) agent.PoseSampled-=Record; if(agent!=null) agent.ObservationSampled-=RecordObservation; if(writer!=null) writer.Dispose(); if(observations!=null) observations.Dispose(); }
    }
}
