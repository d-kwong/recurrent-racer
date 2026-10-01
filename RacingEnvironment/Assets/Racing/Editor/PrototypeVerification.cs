using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Racing.Editor
{
    // Small batch smoke check, no test framework or training dependency.
    [InitializeOnLoad]
    public static class PrototypeVerification
    {
        const string Active = "Racing.Verifying";
        static RaceEpisode episode;
        static int phase;
        static float phaseStart, lastFixedTime = -1;
        static PrototypeVerification() { EditorApplication.update += Tick; }
        [MenuItem("Racing/Verify Prototype in Play Mode")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Racing/Scenes/ManualRacer.unity");
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("PASS: " + message);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || Time.fixedTime < 0.1f || Time.fixedTime == lastFixedTime) return;
            lastFixedTime = Time.fixedTime;
            try
            {
                if (episode == null)
                {
                    episode = UnityEngine.Object.FindFirstObjectByType<RaceEpisode>();
                    Require(episode != null && episode.track != null && episode.chaseCamera != null, "Scene references are wired");
                    episode.keyboard.enabled = false;
                    episode.ResetRun(); phaseStart = Time.fixedTime;
                    Require(episode.vehicle.EffectiveSteeringLimit(24) < episode.vehicle.EffectiveSteeringLimit(5), "Steering sensitivity decreases with speed");
                    Require(episode.chaseCamera.DesiredFieldOfView(24) > episode.chaseCamera.DesiredFieldOfView(0), "Speed increases camera FOV");
                    Require(episode.vehicle.GetComponent<WheelVisuals>().tires.Length == 4, "Four animated tires are wired");
                    episode.vehicle.SetCommands(0,1,0);
                }
                var vehicle = episode.vehicle;
                float elapsed = Time.fixedTime - phaseStart;
                if (phase == 0 && elapsed > 2)
                {
                    Require(vehicle.SpeedKmh > 35 && !vehicle.Crashed, "Acceleration advances along straight without crash");
                    Require(Mathf.Abs(vehicle.transform.position.y-episode.track.spawn.position.y) < 0.001f, "Movement remains planar");
                    vehicle.SetCommands(0,0,1); phase = 1; phaseStart = Time.fixedTime;
                }
                else if (phase == 1 && elapsed > 2)
                {
                    Require(vehicle.SpeedKmh < 0.1f, "Braking stops without reverse");
                    vehicle.SetCommands(1,1,0);
                    episode.ResetRun();
                    Require(vehicle.Body.linearVelocity == Vector3.zero && vehicle.Body.angularVelocity == Vector3.zero, "Reset clears velocities");
                    Require(Vector3.Distance(vehicle.Body.position,episode.track.spawn.position) < 0.001f && Quaternion.Angle(vehicle.Body.rotation,episode.track.spawn.rotation) < 0.001f, "Reset restores spawn pose");
                    Require(episode.NextCheckpoint == 1 && episode.LapTimes.Count == 0 && episode.CurrentLapTime == 0, "Reset clears lap and checkpoint state");
                    Vector3 expectedCamera = vehicle.transform.position + vehicle.transform.forward * episode.chaseCamera.lookAhead
                        - vehicle.transform.forward * (episode.chaseCamera.distance * Mathf.Cos(episode.chaseCamera.downwardAngle*Mathf.Deg2Rad))
                        + Vector3.up * (episode.chaseCamera.distance * Mathf.Sin(episode.chaseCamera.downwardAngle*Mathf.Deg2Rad));
                    Require(Vector3.Distance(episode.chaseCamera.transform.position,expectedCamera) < 0.001f, "Camera snaps to reset pose");
                    Require(episode.keyboard.WaitingForRelease, "Reset suppresses held keyboard input until release");
                    vehicle.SetCommands(1,0,0); phase = 4; phaseStart = Time.fixedTime;
                }
                else if (phase == 4 && elapsed > 0.25f)
                {
                    Require(vehicle.SteeringAngle > 1 && vehicle.SteeringAngle < vehicle.steeringLimit * 0.65f, "Steering ramps toward held input rather than snapping");
                    var wheels = vehicle.GetComponent<WheelVisuals>();
                    Require(wheels.FrontWheelAngle(true) > wheels.FrontWheelAngle(false), "Inside front tire uses larger Ackermann angle");
                    Require(wheels.steeringPivots[3].localEulerAngles.y > 1, "Front tire transforms animate with steering");
                    vehicle.SetCommands(0,0,0);
                    vehicle.Body.linearVelocity = vehicle.transform.TransformDirection(new Vector3(6,0,12));
                    phase = 5; phaseStart = Time.fixedTime;
                }
                else if (phase == 5 && elapsed > 0.12f)
                {
                    var marks = vehicle.GetComponent<DriftMarks>();
                    Require(marks.IsDrifting && marks.SegmentCount > 0, "Sideways slip starts real tire-mark mesh segments");
                    episode.ResetRun();
                    Require(marks.SegmentCount == 0 && !marks.IsDrifting, "Reset clears tire marks and drift state");
                    Require(Mathf.Abs(vehicle.SteeringAngle) < 0.001f, "Reset centers steering and tire animation");
                    Require(Mathf.Abs(episode.chaseCamera.GetComponent<Camera>().fieldOfView - episode.chaseCamera.fieldOfView) < 0.001f, "Reset clears speed FOV boost");
                    vehicle.ResetVehicle(episode.track.spawn.position,Quaternion.identity);
                    vehicle.SetCommands(0,1,0); phase = 2; phaseStart = Time.fixedTime;
                }
                else if (phase == 2)
                {
                    if (vehicle.Crashed)
                    {
                        Require(vehicle.Body.isKinematic && vehicle.SpeedKmh == 0, "Boundary collision stops and ends run");
                        Require(vehicle.transform.position.z < episode.track.spawn.position.z+episode.track.roadWidth/2+episode.track.curbWidth+0.2f, "Collision prevents passing through curb boundary");
                        episode.ResetRun(); Require(!vehicle.Crashed && !vehicle.Body.isKinematic, "Reset recovers after crash");
                        episode.PassCheckpoint(0); Require(episode.LapTimes.Count == 0, "Finish cannot award lap without ordered checkpoints");
                        phase = 3; phaseStart = Time.fixedTime;
                    }
                    else if (elapsed > 5) throw new InvalidOperationException("No boundary collision detected");
                }
                else if (phase == 3)
                {
                    if (vehicle.Crashed) throw new InvalidOperationException("Scripted lap driver crashed");
                    if (episode.LapTimes.Count > 0)
                    {
                        Require(episode.LapTimes[0] > 20, "Vehicle completes a physical lap through ordered gates");
                        Debug.Log("RACING_VERIFICATION_SUCCESS"); Finish(0); return;
                    }
                    if (elapsed > 120) throw new InvalidOperationException("Lap driver timed out");
                    // Pure pursuit solely for smoke testing. Never saved into the playable scene.
                    var points = episode.track.centerline; int nearest = 0; float best = float.MaxValue;
                    for (int i = 0; i < points.Length; i++)
                    {
                        float distance = (points[i]-vehicle.transform.position).sqrMagnitude;
                        if (distance < best) { best = distance; nearest = i; }
                    }
                    int target = nearest; float ahead = 0;
                    do { int next = (target+1)%points.Length; ahead += Vector3.Distance(points[target],points[next]); target = next; } while (ahead < 9);
                    Vector3 local = vehicle.transform.InverseTransformPoint(points[target]);
                    float desiredAngle = Mathf.Atan2(2*vehicle.wheelbase*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
                    float speed = vehicle.Body.linearVelocity.magnitude;
                    vehicle.SetCommands(desiredAngle/vehicle.EffectiveSteeringLimit(speed),speed < 12 ? 0.7f : 0,speed > 13 ? 0.2f : 0);
                }
            }
            catch (Exception exception) { Debug.LogError("RACING_VERIFICATION_FAILED: " + exception); Finish(1); }
        }
        static void Finish(int result)
        {
            SessionState.SetBool(Active,false);
            if (Application.isBatchMode) EditorApplication.Exit(result);
            else EditorApplication.ExitPlaymode();
        }
    }
}
