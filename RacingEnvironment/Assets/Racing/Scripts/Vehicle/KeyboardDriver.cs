using UnityEngine;
using UnityEngine.InputSystem;
namespace Racing
{
    public sealed class KeyboardDriver : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        public RaceEpisode episode;
        bool waitForRelease = true;
        public bool WaitingForRelease => waitForRelease;
        public void SuppressUntilReleased() { waitForRelease = true; vehicle.SetCommands(0, 0, 0); }
        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) { vehicle.SetCommands(0, 0, 0); return; }
            if (keys.rKey.wasPressedThisFrame) { episode.ResetRun(); return; }
            bool held = keys.wKey.isPressed || keys.sKey.isPressed || keys.aKey.isPressed || keys.dKey.isPressed;
            if (waitForRelease) { if (!held && !keys.rKey.isPressed) waitForRelease = false; vehicle.SetCommands(0, 0, 0); return; }
            vehicle.SetCommands((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                keys.wKey.isPressed ? 1 : 0, keys.sKey.isPressed ? 1 : 0);
        }
        void OnApplicationFocus(bool focused) { if (enabled && !focused) SuppressUntilReleased(); }
        void OnDisable() { if (vehicle != null) SuppressUntilReleased(); }
    }
}
