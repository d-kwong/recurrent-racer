using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
namespace Racing.Editor
{
    // Diagnostic only. Reflection injects test parameters into the installed ML-Agents
    // parameter channel; no alternate runtime interface, assets or scene changes are saved.
    [InitializeOnLoad]
    public static class ProceduralEpisodeVerification
    {
        const string Active="Racing.VerifyProceduralEpisodes";
        static readonly List<string> evidence=new List<string>();
        static ProceduralEpisodeVerification() { EditorApplication.update+=Tick; }
        public static void Run()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Start in Edit Mode");
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            SessionState.SetBool(Active,true); EditorApplication.EnterPlaymode();
        }
        static void Check(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
            evidence.Add("PASS: "+message); Debug.Log(evidence[evidence.Count-1]);
        }
        static void Parameter(string key,float value)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var parameters=Academy.Instance.EnvironmentParameters;
            var channel=parameters.GetType().GetField("m_Channel",flags).GetValue(parameters);
            var values=(Dictionary<string,Func<float>>)channel.GetType().GetField("m_Parameters",flags).GetValue(channel);
            values[key]=()=>value;
        }
        static bool Step(RacingAgent agent,float steer=0,float longitudinal=0)
        {
            int before=agent.CompletedEpisodes; Academy.Instance.EnvironmentStep();
            if(agent.CompletedEpisodes!=before) return true;
            agent.OnActionReceived(new ActionBuffers(new[] {steer,longitudinal},Array.Empty<int>()));
            agent.vehicle.SendMessage("FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
            return false;
        }
        static void Reset(RacingAgent agent) {agent.EndEpisode();}
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.fixedTime<.1f) return;
            SessionState.SetBool(Active,false); int code=0; RacingAgent agent=null;
            float maximum=120,noProgress=15; bool automatic=true;
            try
            {
                agent=UnityEngine.Object.FindFirstObjectByType<RacingAgent>(); maximum=agent.maximumEpisodeSeconds; noProgress=agent.noProgressSeconds;
                automatic=Academy.Instance.AutomaticSteppingEnabled; Academy.Instance.AutomaticSteppingEnabled=false; Physics.simulationMode=SimulationMode.Script;
                Parameter("racing_track_mode",1); Parameter("racing_track_seed",101); Parameter("racing_track_seed_count",0);
                Parameter("racing_track_spawn_mode",2); Parameter("racing_track_spawn_turn_0",3); Parameter("racing_track_spawn_approach",15);
                Reset(agent); var generator=agent.episode.track.GetComponent<ProceduralTrack>(); var record=generator.Record;
                Check(record.spawnTurnIndex==3 && Mathf.Abs(record.turnEntryDistances[3]-record.spawnDistance-15)<.001f,"explicit midroute turn3/15m pose");
                Check(record.actualSpawnPosition==agent.vehicle.Body.position && Vector3.Dot(record.spawnForward,agent.episode.track.spawn.forward)>.9999f,"exact physics spawn metadata");
                Check(agent.progress.ValidatedGates==0 && agent.progress.TravelMetres==0 && agent.episode.NextCheckpoint==record.activeGateIndices[0],"midroute reset credits no skipped gates/distance");
                Step(agent);
                // Diagnostic poses test signed reward potential on the approach straight, not a policy.
                foreach(float delta in new[] {.5f,1f,1.5f,2f,1.5f,1f,.5f,0f})
                {
                    Vector3 position=ProceduralTrack.At(record,record.spawnDistance+delta,out var direction); position.y=record.actualSpawnPosition.y;
                    agent.vehicle.ResetVehicle(position,Quaternion.LookRotation(direction)); Check(!Step(agent),"small diagnostic progress remains valid");
                    float expected=agent.progressRewardPerMetre*delta+agent.timeRewardPerSecond*agent.ElapsedSeconds;
                    Check(Mathf.Abs(agent.GetCumulativeReward()-expected)<.00005f,"signed progress reward + time per physics interval");
                }
                Check(agent.progress.ValidatedGates==0,"retracing cannot validate skipped gates");
                Reset(agent); for(int i=0;i<8;i++) Step(agent,.5f,.5f); Reset(agent);
                Check(agent.vehicle.Body.linearVelocity==Vector3.zero && agent.vehicle.Body.angularVelocity==Vector3.zero && agent.vehicle.SteeringAngle==0 && agent.LastActions==Vector2.zero && agent.progress.TravelMetres==0,"reset clears velocity/steering/held action/progress");
                Step(agent); agent.vehicle.SendMessage("FixedUpdate");
                Check(agent.vehicle.Body.linearVelocity==Vector3.zero && agent.vehicle.SteeringAngle==0,"held controls remain cleared after reset");
                agent.vehicle.StopAsCrash(); Step(agent);
                Check(agent.LastEndReason==RacingEndReason.Crash && !agent.LastEndInterrupted,"crash terminates and resets");
                Reset(agent); Step(agent); int before=agent.CompletedEpisodes;
                for(int i=0;i<760 && agent.CompletedEpisodes==before;i++) Step(agent);
                Check(agent.LastEndReason==RacingEndReason.NoProgress && !agent.LastEndInterrupted && Mathf.Abs(agent.LastEpisodeReward-agent.timeRewardPerSecond*15)<.00005f,"idle noProgress750 intervals is terminal with time reward");
                agent.maximumEpisodeSeconds=.12f; agent.noProgressSeconds=100; Reset(agent); before=agent.CompletedEpisodes;
                for(int i=0;i<12 && agent.CompletedEpisodes==before;i++) Step(agent);
                Check(agent.LastEndReason==RacingEndReason.Timeout && agent.LastEndInterrupted && Mathf.Abs(agent.LastEpisodeReward-agent.timeRewardPerSecond*.12f)<.00005f,"timeout6 intervals is interrupted with exact time reward");
                agent.maximumEpisodeSeconds=maximum; agent.noProgressSeconds=noProgress; Reset(agent);
                DriveSuffix(agent);
                Check(agent.LastEndReason==RacingEndReason.Finish && !agent.LastEndInterrupted,"scripted physical suffix reaches ordered finish before cap");
                Reset(agent); before=agent.CompletedEpisodes; agent.episode.PassCheckpoint(0);
                Check(agent.CompletedEpisodes==before && agent.progress.ValidatedGates==0,"premature finish on fresh midroute start rejected");
                evidence.Add("PROCEDURAL_EPISODE_VERIFICATION_SUCCESS; scripted diagnostic, not learned-policy evidence.");
            }
            catch(Exception error) {code=1;evidence.Add("PROCEDURAL_EPISODE_VERIFICATION_FAILED: "+error);Debug.LogException(error);}
            finally
            {
                if(agent!=null) {agent.maximumEpisodeSeconds=maximum;agent.noProgressSeconds=noProgress;}
                if(Academy.IsInitialized) Academy.Instance.AutomaticSteppingEnabled=automatic;
                Physics.simulationMode=SimulationMode.FixedUpdate;
                string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--racing-episode-output");
                string output=index>=0 ? args[index+1] : "../runs/procedural/episodes.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))); File.WriteAllLines(output,evidence);
                if(Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
            }
        }
        static void DriveSuffix(RacingAgent agent)
        {
            var record=agent.episode.track.GetComponent<ProceduralTrack>().Record; var vehicle=agent.vehicle;
            int before=agent.CompletedEpisodes;
            for(int tick=0;tick<6000 && agent.CompletedEpisodes==before;tick++)
            {
                float s=agent.progress.Project(vehicle.Body.position,out _);
                Vector3 target=ProceduralTrack.At(record,Mathf.Min(s+9,record.length),out _);
                Vector3 local=vehicle.transform.InverseTransformPoint(target);
                float speed=vehicle.Body.linearVelocity.magnitude;
                float angle=Mathf.Atan2(2*vehicle.wheelbase*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
                float steer=angle/vehicle.EffectiveSteeringLimit(speed), throttle=speed<8 ? .5f : speed>8.5f ? -.2f : 0;
                float seconds=agent.PhysicsTicks*Time.fixedDeltaTime;
                bool ended=Step(agent,steer,throttle);
                float reward=ended ? agent.LastEpisodeReward : agent.GetCumulativeReward();
                float credited=ended ? s-record.spawnDistance : agent.progress.RewardPosition;
                float expected=agent.progressRewardPerMetre*credited+agent.timeRewardPerSecond*seconds+(ended && agent.LastEndReason==RacingEndReason.Finish ? agent.finishReward : 0);
                if(Mathf.Abs(reward-expected)>.00015f) throw new InvalidOperationException("Physical suffix reward mismatch at tick="+tick);
                if(ended) {Check(agent.LastEndReason==RacingEndReason.Finish,"physical suffix reward includes finish bonus exactly once");break;}
            }
        }
    }
}
