using UnityEngine;
namespace Racing
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public ArcadeVehicle vehicle;
        [Range(0, 25)] public float speedFovBoost = 10;
        [Min(0)] public float fovStartSpeed = 5;
        [Min(0.1f)] public float fovFullSpeed = 48;
        [Min(0.1f)] public float fovResponse = 5;
        Camera lens;
        public float DesiredFieldOfView(float speed) => fieldOfView + speedFovBoost * Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(fovStartSpeed, Mathf.Max(fovStartSpeed + 0.1f, fovFullSpeed), speed));
        void Awake() { lens = GetComponent<Camera>(); }
        [Range(10, 70)] public float downwardAngle = 20;
        [Range(10, 80)] public float fieldOfView = 35;
        [Min(0.1f)] public float distance = 18f;
        [Min(0.1f)] public float smoothing = 12;
        [Range(0, 5f)] public float lookAhead = 4f;
        Vector3 DesiredPosition => target.position + target.forward * lookAhead - target.forward * (distance * Mathf.Cos(downwardAngle * Mathf.Deg2Rad)) + Vector3.up * (distance * Mathf.Sin(downwardAngle * Mathf.Deg2Rad));
        Quaternion DesiredRotation(Vector3 position) => Quaternion.LookRotation(target.position + target.forward * lookAhead - position, Vector3.up);
        public void Snap() { if (target == null) return; transform.position = DesiredPosition; transform.rotation = DesiredRotation(transform.position); if (lens == null) lens = GetComponent<Camera>(); lens.fieldOfView = fieldOfView; }
        void LateUpdate()
        {
            if (target == null) return;
            if (lens == null) lens = GetComponent<Camera>();
            float speed = vehicle == null || vehicle.Body == null ? 0 : vehicle.Body.linearVelocity.magnitude;
            lens.fieldOfView = Mathf.Lerp(lens.fieldOfView, DesiredFieldOfView(speed), 1 - Mathf.Exp(-fovResponse * Time.deltaTime));
            float t = 1 - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, DesiredPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, DesiredRotation(transform.position), t);
        }
    }
}
