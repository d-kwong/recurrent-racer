using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
namespace Racing
{
    public enum RacingEndReason { None, Crash, OffTrack, InvalidProgress, Finish, NoProgress, Timeout }
    [System.Serializable]
    public sealed class ProceduralEpisodeRecord
    {
        public int seed, physicsTicks, sequenceIndex, spawnSeed, spawnTurnIndex;
        public string geometrySha256, taskSha256, reason;
        public bool interrupted;
        public DrivingQualityRecord quality;
        public float simulatedSeconds, travelMetres, routeLength, finishDistance, spawnDistance, spawnApproach;
    }
    public sealed class RacingAgent : Agent
    {
        public const int ObservationCount = RoadDistanceSensor.RayCount+4;
        public ArcadeVehicle vehicle;
        public RaceEpisode episode;
        public RoadDistanceSensor distances;
        public RouteProgress progress;
        [Range(1,20)] public int decisionPeriod = 5;
        public float progressRewardPerMetre = 0.01f;
        public float timeRewardPerSecond = -0.005f;
        public float finishReward = 1;
        // Progress-only failure objective: termination forfeits future reward, no extra penalty.
        public float failureReward = 0;
        public float maximumEpisodeSeconds = 120;
        public float noProgressSeconds = 15;
        public float progressWindowMetres = 0.5f;
        public float lateralVelocityScale = 20;
        public float yawRateScale = 4;
        public RacingEndReason LastEndReason { get; private set; }
        public bool LastEndInterrupted { get; private set; }
        public float LastEpisodeReward { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int PhysicsTicks { get; private set; }
        public Vector2 LastActions { get; private set; }
        float previousRewardPosition, bestProgress, episodeReward;
        int stagnantTicks;
        bool crashPending, finishPending;
        DrivingQuality quality;
        int trackSequenceId=-1, trackSequenceIndex;
        readonly float[] observationBuffer=new float[ObservationCount];
        public override void Initialize()
        {
            Academy.Instance.AgentPreStep+=BeforeAcademyStep;
            vehicle.Crash+=OnCrash; episode.CheckpointPassed+=OnGate;
            episode.CheckpointValidator=progress.ValidateGate;
        }
        protected override void OnDisable()
        {
            if(Academy.IsInitialized) Academy.Instance.AgentPreStep-=BeforeAcademyStep;
            if(vehicle!=null) vehicle.Crash-=OnCrash;
            if(episode!=null) { episode.CheckpointPassed-=OnGate; episode.CheckpointValidator=null; }
            base.OnDisable();
        }
        public override void OnEpisodeBegin()
        {
            ConfigureTrack();
            episode.ResetRun(); ApplyCurriculumSpawn(); progress.ResetProgress(vehicle.Body.position);
            previousRewardPosition=0; bestProgress=0; stagnantTicks=0; episodeReward=0;
            ElapsedSeconds=0; PhysicsTicks=0; LastActions=Vector2.zero;
            crashPending=false; finishPending=false;
            quality=null;
            if(Academy.Instance.EnvironmentParameters.GetWithDefault("racing_quality_telemetry",0)==1)
            {
                float entry=-1,exit=-1;
                var generated=episode.track.GetComponent<ProceduralTrack>();
                if(generated!=null && generated.Record!=null)
                {
                    var record=generated.Record;
                    for(int i=0;i<record.turnExitDistances.Length;i++) if(record.turnExitDistances[i]>record.spawnDistance)
                    { entry=record.turnEntryDistances[i]-record.spawnDistance;exit=record.turnExitDistances[i]-record.spawnDistance;break; }
                }
                quality=new DrivingQuality(entry,exit);
            }
        }
        public void ConfigureTrack()
        {
            var parameters=Academy.Instance.EnvironmentParameters;
            // Accepted showcase default; Python always supplies its explicit track mode.
            int fallbackMode=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--racing-fixed")>=0 ? 0 : 1;
            int mode=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_mode",fallbackMode),0,1,"mode");
            var generator=episode.track.GetComponent<ProceduralTrack>();
            if(mode==0 && generator==null) return; // Exact fixed-track bypass.
            if(generator==null) generator=episode.track.gameObject.AddComponent<ProceduralTrack>();
            int sequenceId=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_sequence_id",0),0,16777215,"sequence id");
            if(sequenceId!=trackSequenceId) { trackSequenceId=sequenceId; trackSequenceIndex=0; }
            int seedCount=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_seed_count",0),0,64,"seed count");
            int index=trackSequenceIndex++;
            float requested=seedCount==0 ? parameters.GetWithDefault("racing_track_seed",1009) :
                parameters.GetWithDefault("racing_track_seed_"+(index%seedCount),101);
            int seed=ProceduralTrack.Integer(requested,0,16777215,"seed");
            generator.Configure(episode.track,episode,mode,seed,ProceduralTrack.ReadParameters(),index);
            if(mode==1)
            {
                int spawnMode=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_spawn_mode",0),0,2,"spawn mode");
                int spawnSeed=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_spawn_seed",7),0,16777215,"spawn seed");
                int turn=ProceduralTrack.Integer(parameters.GetWithDefault("racing_track_spawn_turn_"+(seedCount==0 ? 0 : index%seedCount),-1),-1,11,"spawn turn");
                generator.SetSpawn(episode,spawnMode,spawnSeed,index,
                    parameters.GetWithDefault("racing_track_spawn_min",10),parameters.GetWithDefault("racing_track_spawn_max",20),
                    parameters.GetWithDefault("racing_track_original_fraction",.25f),turn,parameters.GetWithDefault("racing_track_spawn_approach",15));
                Debug.Log("RACING_TRACK: "+JsonUtility.ToJson(generator.Record));
            }
        }
        // Optional Python environment parameters. Default zero preserves original gameplay.
        // Only the episode pose changes; the finish line, gates and physics stay fixed.
        void ApplyCurriculumSpawn()
        {
            if(!episode.track.closedLoop) return;
            var parameters=Academy.Instance.EnvironmentParameters;
            float minimum=parameters.GetWithDefault("racing_spawn_min",0);
            float maximum=parameters.GetWithDefault("racing_spawn_max",0);
            float originalFraction=parameters.GetWithDefault("racing_original_fraction",0);
            if(maximum<=0 || Random.value<originalFraction) return;
            Vector3 corner=episode.track.centerline[0], original=episode.track.spawn.position;
            corner.y=original.y;
            Vector3 direction=episode.track.spawn.forward;
            float straight=Vector3.Dot(corner-original,direction);
            if(minimum<5 || maximum<minimum || maximum>straight ||
                Vector3.Distance(corner-original,direction*straight)>0.01f)
                throw new System.InvalidOperationException("Invalid first-turn spawn curriculum geometry/range");
            float approach=Random.Range(minimum,maximum);
            Vector3 position=corner-direction*approach;
            vehicle.ResetVehicle(position,episode.track.spawn.rotation);
            episode.chaseCamera.Snap();
            Debug.Log("RACING_CURRICULUM_SPAWN: approach="+approach+", position="+position);
        }
        void OnCrash() { crashPending=true; }
        void OnGate(int index) { progress.AcceptGate(index); if(index==0) finishPending=true; }
        void Reward(float value) { AddReward(value); episodeReward+=value; }
        void BeforeAcademyStep(int academyStep)
        {
            if(!isActiveAndEnabled) return;
            if(PhysicsTicks>0) EvaluatePhysicsStep();
            if(PhysicsTicks%decisionPeriod==0) RequestDecision(); else RequestAction();
            PhysicsTicks++;
        }
        // Called before ML-Agents sends state, so completed physics intervals contribute to that decision.
        public void EvaluatePhysicsStep()
        {
            float dt=Time.fixedDeltaTime; ElapsedSeconds=PhysicsTicks*dt;
            bool valid=progress.Advance(vehicle.Body.position,vehicle.maximumSpeed*dt*1.5f+0.25f);
            if(quality!=null)
            {
                Vector3 local=Quaternion.Inverse(vehicle.Body.rotation)*vehicle.Body.linearVelocity;
                quality.Sample(dt,ElapsedSeconds,progress.LateralDistance,episode.track.roadWidth/2,local.z,vehicle.Body.linearVelocity.magnitude,progress.TravelMetres);
            }
            float position=progress.RewardPosition;
            Reward((position-previousRewardPosition)*progressRewardPerMetre+timeRewardPerSecond*dt);
            previousRewardPosition=position;
            if(progress.TravelMetres>=bestProgress+progressWindowMetres) { bestProgress=progress.TravelMetres; stagnantTicks=0; }
            else stagnantTicks++;
            if(crashPending || vehicle.Crashed) Complete(RacingEndReason.Crash,false,failureReward);
            else if(!valid) Complete(RacingEndReason.InvalidProgress,false,failureReward);
            else if(progress.LateralDistance>episode.track.roadWidth/2+episode.track.curbWidth+0.1f) Complete(RacingEndReason.OffTrack,false,failureReward);
            else if(finishPending) Complete(RacingEndReason.Finish,false,finishReward);
            else if(stagnantTicks*dt+0.0001f>=noProgressSeconds) Complete(RacingEndReason.NoProgress,false,failureReward);
            else if(ElapsedSeconds+0.0001f>=maximumEpisodeSeconds) Complete(RacingEndReason.Timeout,true,0);
        }
        void Complete(RacingEndReason reason,bool interrupted,float bonus)
        {
            var generated=episode.track.GetComponent<ProceduralTrack>();
            if(generated!=null && generated.Record!=null)
                Debug.Log("RACING_EPISODE: " + JsonUtility.ToJson(new ProceduralEpisodeRecord {
                    seed=generated.Record.seed, sequenceIndex=Mathf.Max(0,trackSequenceIndex-1), geometrySha256=generated.Record.geometrySha256, taskSha256=generated.Record.taskSha256,
                    spawnSeed=generated.Record.spawnSeed, spawnTurnIndex=generated.Record.spawnTurnIndex,
                    spawnDistance=generated.Record.spawnDistance, spawnApproach=generated.Record.spawnApproach,
                    reason=reason.ToString(), interrupted=interrupted, physicsTicks=PhysicsTicks,
                    simulatedSeconds=ElapsedSeconds, travelMetres=progress.TravelMetres,
                    routeLength=progress.RouteLength, finishDistance=generated.Record.taskDistance,
                    quality=quality==null ? null : quality.Complete(reason==RacingEndReason.Finish,progress.TravelMetres) }));
            else if(quality!=null)
                Debug.Log("RACING_QUALITY_EPISODE: "+JsonUtility.ToJson(new ProceduralEpisodeRecord {
                    reason=reason.ToString(),interrupted=interrupted,physicsTicks=PhysicsTicks,simulatedSeconds=ElapsedSeconds,
                    travelMetres=progress.TravelMetres,routeLength=progress.RouteLength,
                    quality=quality.Complete(reason==RacingEndReason.Finish,progress.TravelMetres) }));
            Reward(bonus); LastEndReason=reason; LastEndInterrupted=interrupted; LastEpisodeReward=episodeReward;
            if(interrupted) EpisodeInterrupted(); else EndEpisode();
        }
        public void FillObservations(float[] output)
        {
            distances.Fill(output);
            Vector3 local=Quaternion.Inverse(vehicle.Body.rotation)*vehicle.Body.linearVelocity;
            output[11]=Mathf.Clamp(local.z/vehicle.maximumSpeed,-1,1);
            output[12]=Mathf.Clamp(local.x/lateralVelocityScale,-1,1);
            output[13]=Mathf.Clamp(vehicle.Body.angularVelocity.y/yawRateScale,-1,1);
            output[14]=Mathf.Clamp(vehicle.SteeringAngle/Mathf.Max(0.1f,vehicle.steeringLimit),-1,1);
        }
        public override void CollectObservations(VectorSensor sensor)
        {
            if(sensor==null || vehicle==null || vehicle.Body==null) return;
            FillObservations(observationBuffer);
            for(int i=0;i<observationBuffer.Length;i++) sensor.AddObservation(observationBuffer[i]);
        }
        public override void OnActionReceived(ActionBuffers actions)
        {
            if(!isActiveAndEnabled) return;
            float steer=FiniteClamp(actions.ContinuousActions[0]), longitudinal=FiniteClamp(actions.ContinuousActions[1]);
            LastActions=new Vector2(steer,longitudinal);
            vehicle.SetCommands(steer,Mathf.Max(0,longitudinal),Mathf.Max(0,-longitudinal));
        }
        static float FiniteClamp(float value) => float.IsNaN(value)||float.IsInfinity(value) ? 0 : Mathf.Clamp(value,-1,1);
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Without a Python connection, training mode idles; manual input belongs to KeyboardDriver only.
            var continuous=actionsOut.ContinuousActions; continuous[0]=0; continuous[1]=0;
        }
    }
}
