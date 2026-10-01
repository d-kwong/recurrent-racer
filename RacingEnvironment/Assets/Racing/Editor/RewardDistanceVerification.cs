using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;

namespace Racing.Editor
{
    // Diagnostic only: no changes to the reward function, player or scene assets.
    [InitializeOnLoad]
    public static class RewardDistanceVerification
    {
        const string Active="Racing.VerifyRewardDistance";
        static readonly List<string> evidence=new List<string>();
        static readonly string DirectoryPath="../runs/reward-distance-check";
        static RewardDistanceVerification() { EditorApplication.update+=Tick; }
        [MenuItem("Racing/Verify Turning Reward")]
        public static void Run()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Start from Edit Mode");
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            SessionState.SetBool(Active,true); EditorApplication.EnterPlaymode();
        }
        static void Require(bool value,string message)
        {
            if(!value) throw new InvalidOperationException(message);
            evidence.Add("PASS: "+message); Debug.Log(evidence[evidence.Count-1]);
        }
        static string N(float value) => value.ToString("R",CultureInfo.InvariantCulture);
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.fixedTime<.1f) return;
            SessionState.SetBool(Active,false); int code=0;
            try
            {
                var agent=UnityEngine.Object.FindFirstObjectByType<RacingAgent>();
                Academy.Instance.AutomaticSteppingEnabled=false; Physics.simulationMode=SimulationMode.Script;
                Geometry(agent);
                var straight=Probe(agent,false,"straight");
                var corner=Probe(agent,true,"corner-following");
                Require(corner.gates>=1,"Corner-following physically passes checkpoint 1");
                Require(corner.travel>straight.travel+30,"Valid turn gains substantially more route distance than straight-only driving");
                Require(corner.reward>straight.reward+.3f,"Current reward favors corner-following over straight-only driving");
                evidence.Add("SUMMARY: straight travel="+N(straight.travel)+", return="+N(straight.reward)+", seconds="+N(straight.seconds)+", ended="+straight.ended+", reason="+straight.reason);
                evidence.Add("SUMMARY: corner travel="+N(corner.travel)+", return="+N(corner.reward)+", seconds="+N(corner.seconds)+", gates="+corner.gates+", ended="+corner.ended);
                evidence.Add("REWARD_DISTANCE_VERIFICATION_SUCCESS; Unity diagnostic, not a learned policy.");
            }
            catch(Exception error) { code=1; evidence.Add("REWARD_DISTANCE_VERIFICATION_FAILED: "+error); Debug.LogException(error); }
            finally
            {
                Physics.simulationMode=SimulationMode.FixedUpdate;
                Directory.CreateDirectory(DirectoryPath); File.WriteAllLines(DirectoryPath+"/verification.txt",evidence);
                if(Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
            }
        }
        static void Geometry(RacingAgent agent)
        {
            agent.EndEpisode(); var progress=agent.progress; var points=agent.episode.track.centerline;
            progress.ResetProgress(points[0]); float expected=0, previous=0, reward=0;
            for(int i=0;i<32;i++)
            {
                expected+=Vector3.Distance(points[i],points[i+1]);
                for(int j=1;j<=4;j++)
                {
                    RequireAdvance(progress,Vector3.Lerp(points[i],points[i+1],j/4f));
                    reward+=(progress.RewardPosition-previous)*agent.progressRewardPerMetre;
                    previous=progress.RewardPosition;
                }
            }
            float chord=Vector3.Distance(points[0],points[32]);
            Require(Mathf.Abs(progress.TravelMetres-expected)<.002f,"First curved corner credits polyline arc distance, not endpoint chord");
            Require(expected>chord+2,"Corner arc distance exceeds straight-line displacement");
            Require(Mathf.Abs(reward-.01f*expected)<.00002f,"Corner earns +0.01 per metre of forward route progress");
            evidence.Add("GEOMETRY: quarter-turn arc="+N(expected)+", chord="+N(chord)+", progress-only reward="+N(reward));
            for(int i=32;i>0;i--) for(int j=1;j<=4;j++)
            {
                RequireAdvance(progress,Vector3.Lerp(points[i],points[i-1],j/4f));
                reward+=(progress.RewardPosition-previous)*agent.progressRewardPerMetre;
                previous=progress.RewardPosition;
            }
            Require(Mathf.Abs(progress.TravelMetres)<.002f&&Mathf.Abs(reward)<.00002f,
                "Retracing the corner removes progress credit; forward/backward motion cannot farm it");
            var spawn=agent.episode.track.spawn;
            progress.ResetProgress(spawn.position);
            RequireAdvance(progress,spawn.position+spawn.right);
            Require(Mathf.Abs(progress.TravelMetres)<.001f,"One metre sideways on straight earns zero route progress");
            agent.EndEpisode();
        }
        static void RequireAdvance(RouteProgress progress,Vector3 point)
        {
            if(!progress.Advance(point,1.69f)) throw new InvalidOperationException("Invalid geometry probe jump");
        }
        sealed class Result
        {
            public float travel,reward,seconds; public int gates; public bool ended; public RacingEndReason reason;
        }
        static Result Probe(RacingAgent agent,bool follow,string name)
        {
            agent.EndEpisode(); var vehicle=agent.vehicle; var track=agent.episode.track;
            var position=track.centerline[0]; position.y=track.spawn.position.y; position-=track.spawn.forward*15;
            vehicle.ResetVehicle(position,track.spawn.rotation); agent.progress.ResetProgress(position);
            Directory.CreateDirectory(DirectoryPath);
            var lines=new List<string> {"physics_tick,sim_seconds,route_travel_m,cumulative_reward,expected_reward,last_interval_steer_command,post_delivery_speed_mps,terminal"};
            int initial=agent.CompletedEpisodes; float previousS=agent.progress.Project(position,out _);
            float travel=0,reward=0,seconds=0; int gates=0; bool ended=false;
            float lastCommand=0;
            for(int tick=0;tick<=750;tick++)
            {
                // Measure completed physics interval before Academy can terminate/reset it.
                float s=agent.progress.Project(vehicle.Body.position,out _);
                travel+=Mathf.Repeat(s-previousS+agent.progress.RouteLength/2,agent.progress.RouteLength)-agent.progress.RouteLength/2;
                previousS=s; gates=agent.progress.ValidatedGates;
                Academy.Instance.EnvironmentStep();
                ended=agent.CompletedEpisodes!=initial;
                reward=ended ? agent.LastEpisodeReward : agent.GetCumulativeReward();
                seconds=tick*Time.fixedDeltaTime;
                float expected=.01f*travel-.005f*seconds;
                if(Mathf.Abs(reward-expected)>.0001f) throw new InvalidOperationException(name+" reward/distance mismatch at tick "+tick+": "+reward+" versus "+expected);
                lines.Add(tick+","+N(seconds)+","+N(travel)+","+N(reward)+","+N(expected)+","+N(lastCommand)+","+N(vehicle.Body.linearVelocity.magnitude)+","+ended);
                if(ended||tick==750) break;
                float speed=vehicle.Body.linearVelocity.magnitude, steer=0;
                if(follow)
                {
                    var points=track.centerline; int nearest=0; float best=float.MaxValue;
                    for(int i=0;i<points.Length;i++) {float d=(points[i]-vehicle.transform.position).sqrMagnitude;if(d<best){best=d;nearest=i;}}
                    int target=nearest; float ahead=0;
                    do {int next=(target+1)%points.Length;ahead+=Vector3.Distance(points[target],points[next]);target=next;} while(ahead<9);
                    var local=vehicle.transform.InverseTransformPoint(points[target]);
                    float angle=Mathf.Atan2(2*vehicle.wheelbase*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
                    steer=Mathf.Clamp(angle/vehicle.EffectiveSteeringLimit(speed),-1,1);
                }
                float longitudinal=speed<8 ? .3f : speed>8.5f ? -.2f : 0;
                lastCommand=steer;
                agent.OnActionReceived(new ActionBuffers(new [] {steer,longitudinal},Array.Empty<int>()));
                vehicle.SendMessage("FixedUpdate"); Physics.Simulate(Time.fixedDeltaTime);
            }
            File.WriteAllLines(DirectoryPath+"/"+name+".csv",lines);
            Require(true,name+" physical reward matches 0.01 × signed route metres − 0.005 × elapsed seconds each tick");
            return new Result {travel=travel,reward=reward,seconds=seconds,gates=gates,ended=ended,reason=ended ? agent.LastEndReason : RacingEndReason.None};
        }
    }
}
