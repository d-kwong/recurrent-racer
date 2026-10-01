using UnityEngine;
namespace Racing
{
    public sealed class WheelVisuals : MonoBehaviour
    {
        public ArcadeVehicle vehicle;
        // Order: rear left, rear right, front left, front right. Pivots provide steering; cylinders provide spin.
        public Transform[] steeringPivots;
        public Transform[] tires;
        public float wheelRadius = 0.75f;
        public float frontTrackWidth = 3.5f;
        float spin;
        void OnEnable() { if (vehicle != null) vehicle.ResetPerformed += ResetVisuals; }
        void OnDisable() { if (vehicle != null) vehicle.ResetPerformed -= ResetVisuals; }
        public float FrontWheelAngle(bool rightWheel)
        {
            float angle = vehicle.SteeringAngle;
            if (Mathf.Abs(angle) < 0.01f) return 0;
            // Ackermann steering forms the steering trapezoid: inside front tire turns more.
            float radius = vehicle.wheelbase / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
            bool inside = rightWheel == (angle > 0);
            float wheelAngle = Mathf.Atan2(vehicle.wheelbase, Mathf.Max(0.1f, radius + (inside ? -1 : 1) * frontTrackWidth / 2)) * Mathf.Rad2Deg;
            return Mathf.Sign(angle) * wheelAngle;
        }
        void LateUpdate()
        {
            spin = (spin + Vector3.Dot(vehicle.Body.linearVelocity, transform.forward) / Mathf.Max(0.01f, wheelRadius) * Mathf.Rad2Deg * Time.deltaTime) % 360;
            Apply();
        }
        void Apply()
        {
            for (int i = 0; i < tires.Length; i++)
            {
                steeringPivots[i].localRotation = Quaternion.Euler(0, i < 2 ? 0 : FrontWheelAngle(i == 3), 0);
                tires[i].localRotation = Quaternion.AngleAxis(spin, Vector3.right) * Quaternion.Euler(0, 0, 90);
            }
        }
        public void ResetVisuals() { spin = 0; Apply(); }
    }
}
