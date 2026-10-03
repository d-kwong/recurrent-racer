using System.Collections.Generic;
using UnityEngine;
namespace Racing
{
    public sealed class RaceEpisode : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        public KeyboardDriver keyboard;
        public RaceTrack track;
        public ChaseCamera chaseCamera;
        public bool automaticStartReset = true;
        public event System.Action<int> CheckpointPassed;
        public System.Func<int,bool> CheckpointValidator;
        public List<float> LapTimes { get; } = new List<float>();
        public float CurrentLapTime { get; private set; }
        public int NextCheckpoint { get; private set; } = 1;
        public int LapNumber => LapTimes.Count + 1;
        void Start() { if (automaticStartReset) ResetRun(); }
        void FixedUpdate() { if (!vehicle.Crashed) CurrentLapTime += Time.fixedDeltaTime; }
        public void PassCheckpoint(int index)
        {
            if (vehicle.Crashed || index != NextCheckpoint || (CheckpointValidator != null && !CheckpointValidator(index))) return;
            if (index == 0) { LapTimes.Add(CurrentLapTime); CurrentLapTime = 0; }
            NextCheckpoint = (index + 1) % track.checkpointCount;
            CheckpointPassed?.Invoke(index);
        }
        // UI-independent reset entry point for a future environment wrapper.
        public void ResetRun()
        {
            vehicle.ResetVehicle(track.spawn.position, track.spawn.rotation);
            CurrentLapTime = 0; LapTimes.Clear(); NextCheckpoint = track.firstCheckpoint;
            keyboard.SuppressUntilReleased(); chaseCamera.Snap();
        }
    }
}
