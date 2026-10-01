using UnityEngine;
namespace Racing
{
    public enum RacingDriverMode { Manual, Agent }
    [DefaultExecutionOrder(-1000)]
    public sealed class TrainingMode : MonoBehaviour
    {
        public RacingDriverMode mode = RacingDriverMode.Agent;
        public RacingAgent agent;
        public KeyboardDriver keyboard;
        public RaceEpisode episode;
        public bool renderDiagnostics = true;
        RacingDriverMode appliedMode;
        void Awake()
        {
            foreach(string arg in System.Environment.GetCommandLineArgs())
            {
                if(arg=="--racing-manual") mode=RacingDriverMode.Manual;
                if(arg=="--racing-no-render") renderDiagnostics=false;
            }
            Physics.IgnoreLayerCollision(8,9,true);
            Apply();
        }
        void Update() { if(appliedMode!=mode) Apply(); }
        System.Collections.IEnumerator Start()
        {
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--racing-smoke-test")<0) yield break;
            // Optional player diagnostic; no trainer or alternate communication protocol.
            var observations=new float[RacingAgent.ObservationCount];
            int initialEpisodes=agent.CompletedEpisodes;
            for(int tick=0;tick<1100;tick++)
            {
                yield return new WaitForFixedUpdate();
                agent.FillObservations(observations);
                foreach(float value in observations)
                    if(float.IsNaN(value)||float.IsInfinity(value)) { Debug.LogError("RACING_PLAYER_SMOKE_FAILED: nonfinite observation");Application.Quit(1);yield break; }
                if(agent.CompletedEpisodes>initialEpisodes)
                {
                    bool valid=mode==RacingDriverMode.Agent&&agent.LastEndReason==RacingEndReason.NoProgress&&!agent.LastEndInterrupted&&agent.vehicle.Body.linearVelocity==Vector3.zero;
                    Debug.Log((valid ? "RACING_PLAYER_SMOKE_SUCCESS" : "RACING_PLAYER_SMOKE_FAILED")+": observation=15, actions=2, reason="+agent.LastEndReason+", reward="+agent.LastEpisodeReward+", graphics="+SystemInfo.graphicsDeviceType+", Python connection not tested");
                    Application.Quit(valid ? 0 : 1);yield break;
                }
            }
            Debug.LogError("RACING_PLAYER_SMOKE_FAILED: episode did not end");Application.Quit(1);
        }
        public void SetMode(RacingDriverMode value) { mode=value; if(Application.isPlaying) Apply(); }
        void Apply()
        {
            // Disable the old input writer before enabling the new one.
            agent.enabled=false; keyboard.enabled=false;
            appliedMode=mode;
            if(mode==RacingDriverMode.Agent) agent.enabled=true;
            else { episode.ResetRun(); keyboard.enabled=true; }
            var camera=episode.chaseCamera;
            camera.enabled=renderDiagnostics; camera.GetComponent<Camera>().enabled=renderDiagnostics;
            episode.GetComponent<RaceHud>().enabled=renderDiagnostics;
            var wheels=episode.vehicle.GetComponent<WheelVisuals>(); if(wheels!=null) wheels.enabled=renderDiagnostics;
            var marks=episode.vehicle.GetComponent<DriftMarks>(); if(marks!=null) marks.enabled=renderDiagnostics;
        }
    }
}
