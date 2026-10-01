using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
namespace Racing.Editor
{
    public sealed class TrainingKeyboardVerification : MonoBehaviour {}
    [InitializeOnLoad]
    public static class TrainingVerification
    {
        const string Active="Racing.VerifyTraining";
        static readonly List<string> evidence=new List<string>();
        static TrainingVerification() { EditorApplication.update+=Tick; }
        [MenuItem("Racing/Verify Training Integration")]
        public static void Run()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Start verification from Edit Mode.");
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            SessionState.SetBool(Active,true);EditorApplication.EnterPlaymode();
        }
        static void Require(bool condition,string description)
        {
            if(!condition) throw new InvalidOperationException(description);
            evidence.Add("PASS: "+description); Debug.Log(evidence[evidence.Count-1]);
        }
        static bool Step(RacingAgent agent,float steer,float longitudinal)
        {
            int before=agent.CompletedEpisodes;
            Academy.Instance.EnvironmentStep();
            if(agent.CompletedEpisodes!=before) return true;
            agent.OnActionReceived(new ActionBuffers(new [] {steer,longitudinal},Array.Empty<int>()));
            agent.vehicle.SendMessage("FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);
            agent.vehicle.GetComponent<DriftMarks>().SendMessage("FixedUpdate");
            var values=new float[RacingAgent.ObservationCount];agent.FillObservations(values);
            foreach(float value in values)
                if(float.IsNaN(value)||float.IsInfinity(value)||Mathf.Abs(value)>1) throw new InvalidOperationException("Invalid observation during physics stepping");
            return false;
        }
        static void Reset(RacingAgent agent) { agent.EndEpisode(); }
        static void Tick()
        {
            if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||Time.fixedTime<0.1f) return;
            SessionState.SetBool(Active,false);
            try
            {
                var mode=UnityEngine.Object.FindFirstObjectByType<TrainingMode>();var agent=mode.agent;var vehicle=agent.vehicle;
                Academy.Instance.AutomaticSteppingEnabled=false; Physics.simulationMode=SimulationMode.Script;
                Reset(agent);
                var behavior=agent.GetComponent<BehaviorParameters>();
                Require(agent.episode.track!=null&&agent.progress.track==agent.episode.track&&agent.distances!=null,"Training references wired");
                Require(behavior.BrainParameters.VectorObservationSize==15&&behavior.BrainParameters.NumStackedVectorObservations==1,"One 15-float unstacked vector configured");
                Require(behavior.BrainParameters.ActionSpec.NumContinuousActions==2&&behavior.BrainParameters.ActionSpec.NumDiscreteActions==0,"Two continuous actions and no discrete branches");
                Require(behavior.FullyQualifiedBehaviorName=="RecurrentRacer?team=0"&&behavior.Model==null,"Behavior name/team and no-model setup");
                Require(!mode.keyboard.enabled&&agent.enabled,"Agent mode excludes keyboard");
                Require(Mathf.Abs(Time.fixedDeltaTime-0.02f)<0.00001f&&agent.decisionPeriod==5&&agent.MaxStep==0,"50 Hz physics, 10 Hz decisions, explicit episode limits");
                var observation=new float[15];agent.FillObservations(observation);
                foreach(float value in observation) Require(!float.IsNaN(value)&&!float.IsInfinity(value),"Finite observation value");
                Require(Mathf.Abs(observation[0]-4.5f/40)<0.001f&&Mathf.Abs(observation[10]-4.5f/40)<0.001f,"Side rays see grey edges at 4.5 m, excluding curb and car");
                Require(observation[5]==1,"Forward no-hit ray returns one");
                Require(Physics.GetIgnoreLayerCollision(8,9),"Sensor edges do not physically block curb driving");
                Step(agent,1,1);
                Require(vehicle.Body.linearVelocity.magnitude>0&&vehicle.SteeringAngle>0,"Agent actions reach vehicle acceleration and filtered steering");
                agent.OnActionReceived(new ActionBuffers(new [] {float.NaN,float.PositiveInfinity},Array.Empty<int>()));
                Require(agent.LastActions==Vector2.zero,"Nonfinite actions sanitized");
                Reset(agent);for(int i=0;i<100;i++) Step(agent,0,1);
                Require(vehicle.SpeedKmh>35&&agent.GetCumulativeReward()>0,"Forward motion earns signed progress reward");
                Require(!agent.progress.ValidateGate(0)&&!agent.progress.ValidateGate(2),"Out-of-order finish/checkpoint rejected");
                Reset(agent);agent.progress.Advance(agent.episode.track.spawn.position+agent.episode.track.spawn.forward,2);
                float forward=agent.progress.TravelMetres;
                agent.progress.Advance(agent.episode.track.spawn.position,2);
                Require(forward>0.9f&&Mathf.Abs(agent.progress.TravelMetres)<0.001f,"Route progress is signed and return cancels forward movement");
                Reset(agent);vehicle.Body.linearVelocity=vehicle.transform.TransformDirection(new Vector3(6,0,12));
                for(int i=0;i<8;i++) { vehicle.GetComponent<DriftMarks>().SendMessage("FixedUpdate");vehicle.SendMessage("FixedUpdate");Physics.Simulate(0.02f); }
                Require(vehicle.GetComponent<DriftMarks>().SegmentCount>0,"Drift state and marks exist before reset");
                Reset(agent);
                Require(vehicle.Body.linearVelocity==Vector3.zero&&vehicle.Body.angularVelocity==Vector3.zero&&vehicle.SteeringAngle==0&&agent.progress.TravelMetres==0&&agent.LastActions==Vector2.zero,"Episode reset clears motion, steering, progress and held action");
                Require(vehicle.GetComponent<DriftMarks>().SegmentCount==0&&agent.episode.NextCheckpoint==1&&agent.episode.LapTimes.Count==0,"Episode reset clears visual history and ordered lap state");
                Step(agent,0,0);vehicle.StopAsCrash();Step(agent,0,0);
                Require(agent.LastEndReason==RacingEndReason.Crash&&!agent.LastEndInterrupted&&Mathf.Abs(agent.LastEpisodeReward-agent.failureReward)<0.02f,"Crash is terminal with failure reward");
                Require(!vehicle.Crashed&&vehicle.Body.linearVelocity==Vector3.zero,"Terminal crash automatically resets episode");
                Reset(agent);Step(agent,0,0);
                vehicle.ResetVehicle(agent.episode.track.spawn.position+Vector3.forward*8,agent.episode.track.spawn.rotation);Step(agent,0,0);
                Require(agent.LastEndReason==RacingEndReason.OffTrack,"Analytic off-track safety termination");
                Reset(agent);Step(agent,0,0);
                vehicle.ResetVehicle(agent.episode.track.spawn.position+agent.episode.track.spawn.forward*20,agent.episode.track.spawn.rotation);Step(agent,0,0);
                Require(agent.LastEndReason==RacingEndReason.InvalidProgress,"Implausible route jump terminates instead of awarding progress");
                Reset(agent);int idleStart=agent.CompletedEpisodes;
                for(int i=0;i<760&&agent.CompletedEpisodes==idleStart;i++) Step(agent,0,0);
                Require(agent.LastEndReason==RacingEndReason.NoProgress&&!agent.LastEndInterrupted&&Mathf.Abs(agent.LastEpisodeReward-(agent.failureReward+agent.timeRewardPerSecond*15f))<0.00002f,"No-progress is terminal after 750 idle intervals with configured failure/time reward");
                agent.maximumEpisodeSeconds=0.12f;agent.noProgressSeconds=100;Reset(agent);
                int timeoutStart=agent.CompletedEpisodes;
                for(int i=0;i<12&&agent.CompletedEpisodes==timeoutStart;i++) Step(agent,0,0);
                Require(agent.LastEndReason==RacingEndReason.Timeout&&agent.LastEndInterrupted,"Time limit calls EpisodeInterrupted");
                agent.maximumEpisodeSeconds=120;agent.noProgressSeconds=15;Reset(agent);
                DriveLap(agent);
                Require(agent.LastEndReason==RacingEndReason.Finish&&!agent.LastEndInterrupted&&agent.LastEpisodeReward>4,"Physical full lap validates gates, wraparound, terminal finish and rewards");
                int finished=agent.CompletedEpisodes;agent.episode.PassCheckpoint(0);
                Require(agent.CompletedEpisodes==finished&&agent.episode.NextCheckpoint==1,"Duplicate finish cannot reward a new episode");
                // A closer spawn changes remaining gate distances, never gate order.
                foreach(float approach in new [] {10f,15f,20f})
                {
                    Reset(agent);
                    Vector3 pose=agent.episode.track.centerline[0]; pose.y=agent.episode.track.spawn.position.y;
                    pose-=agent.episode.track.spawn.forward*approach;
                    vehicle.ResetVehicle(pose,agent.episode.track.spawn.rotation);
                    agent.progress.ResetProgress(pose);
                    Require(agent.progress.TravelMetres==0 && agent.progress.RewardPosition==0,
                        "Curriculum spawn begins with zero travel and reward potential at "+approach+"m");
                    Step(agent,0,1);
                    Require(agent.LastEndReason!=RacingEndReason.InvalidProgress && agent.progress.TravelMetres<0.1f,
                        "Curriculum first interval has no artificial jump credit");
                    // Drive from this pose through all gates, including the fixed finish line.
                    DriveLap(agent);
                    Require(agent.LastEndReason==RacingEndReason.Finish && !agent.LastEndInterrupted,
                        "Curriculum physical lap validates ordered gates and reduced finish distance at "+approach+"m");
                }
                Reset(agent);
                mode.SetMode(RacingDriverMode.Manual);
                Require(mode.keyboard.enabled&&!agent.enabled,"Manual switch disables Agent and enables keyboard");
                mode.gameObject.AddComponent<TrainingKeyboardVerification>().StartCoroutine(VerifyKeyboard(mode));
            }
            catch(Exception exception) {evidence.Add("TRAINING_VERIFICATION_FAILED: "+exception);Debug.LogException(exception);Finish(1);}
            finally {Physics.simulationMode=SimulationMode.FixedUpdate;}
        }
        static void DriveLap(RacingAgent agent)
        {
            var vehicle=agent.vehicle;
            int lapStart=agent.CompletedEpisodes;
                for(int step=0;step<6000&&agent.CompletedEpisodes==lapStart;step++)
                {
                    var points=agent.episode.track.centerline;int nearest=0;float best=float.MaxValue;
                    for(int i=0;i<points.Length;i++) {float d=(points[i]-vehicle.transform.position).sqrMagnitude;if(d<best){best=d;nearest=i;}}
                    int target=nearest;float ahead=0;
                    do {int next=(target+1)%points.Length;ahead+=Vector3.Distance(points[target],points[next]);target=next;}while(ahead<9);
                    var local=vehicle.transform.InverseTransformPoint(points[target]);float speed=vehicle.Body.linearVelocity.magnitude;
                    float angle=Mathf.Atan2(2*vehicle.wheelbase*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
                    Step(agent,angle/vehicle.EffectiveSteeringLimit(speed),speed<12 ? 0.7f : speed>13 ? -0.2f : 0);
                }
        }
        static System.Collections.IEnumerator VerifyKeyboard(TrainingMode mode)
        {
            // Drive actual PlayerLoop frames: button edge events cannot be tested by repeatedly
            // calling InputSystem.Update from a single EditorApplication.update callback.
            var oldRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
            var oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D));
            yield return null;
            yield return new WaitForFixedUpdate();
            bool moving=mode.episode.vehicle.Body.linearVelocity.magnitude>0&&mode.episode.vehicle.SteeringAngle>0;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R,Key.W));
            yield return null;
            try
            {
                Require(moving,"Actual keyboard adapter accepts W/D in manual mode");
                Require(mode.episode.vehicle.Body.linearVelocity==Vector3.zero&&mode.keyboard.WaitingForRelease,"R resets and suppresses held W in manual mode");
                mode.SetMode(RacingDriverMode.Agent);
                Require(!mode.keyboard.enabled&&mode.agent.enabled,"Agent mode can be restored without concurrent input");
                Physics.simulationMode=SimulationMode.Script;
                Step(mode.agent,0,1);
                Require(mode.episode.vehicle.Body.linearVelocity.magnitude>0,"Re-enabled Agent lifecycle still applies actions");
                evidence.Add("TRAINING_VERIFICATION_SUCCESS; Python communication not exercised.");Finish(0);
            }
            catch(Exception exception) {evidence.Add("TRAINING_VERIFICATION_FAILED: "+exception);Debug.LogException(exception);Finish(1);}
            finally {InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=oldRouting;InputSystem.settings.backgroundBehavior=oldBackground;Physics.simulationMode=SimulationMode.FixedUpdate;}
        }
        static void Finish(int code)
        {
            Directory.CreateDirectory("../docs");File.WriteAllLines("../docs/unity-rl-verification.txt",evidence);
            if(Application.isBatchMode) EditorApplication.Exit(code);else EditorApplication.ExitPlaymode();
        }
    }
}
