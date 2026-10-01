using UnityEngine;
namespace Racing
{
    // ML-Agents' Academy stepper runs at order 0: apply its commands before physics.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeVehicle : MonoBehaviour
    {
        [Min(0)] public float acceleration = 12f;
        [Min(0)] public float braking = 18f;
        [Min(1)] public float maximumSpeed = 48f;
        [Min(0)] public float drag = 0.15f;
        [Min(0)] public float lateralGrip = 3.5f;
        [Range(0, 45)] public float steeringLimit = 22f;
        [Min(0.1f)] public float wheelbase = 3.4f;
        [Min(0)] public float steeringResponse = 1.5f;
        [Tooltip("Steering angle is divided by 1 + speed (m/s) × this value.")]
        [Min(0)] public float speedSteeringReduction = 0.025f;
        public float SteeringAngle { get; private set; }
        public float EffectiveSteeringLimit(float speed) => steeringLimit / (1 + Mathf.Abs(speed) * speedSteeringReduction);
        public event System.Action ResetPerformed;
        public Rigidbody Body { get; private set; }
        public bool Crashed { get; private set; }
        public float SpeedKmh => Body == null ? 0 : Body.linearVelocity.magnitude * 3.6f;
        float throttle, brake, steering, currentSteering;
        public event System.Action Crash;
        void Awake() { Body = GetComponent<Rigidbody>(); }
        // All commands are normalized; independent of keyboard and future agent inputs.
        public void SetCommands(float steer, float accelerate, float brakingCommand)
        {
            steering = Mathf.Clamp(steer, -1, 1);
            throttle = Mathf.Clamp01(accelerate);
            brake = Mathf.Clamp01(brakingCommand);
        }
        void FixedUpdate()
        {
            if (Crashed) return;
            float dt = Time.fixedDeltaTime;
            Vector3 local = transform.InverseTransformDirection(Body.linearVelocity);
            local.z = Mathf.Clamp(local.z + (throttle * acceleration - brake * braking - drag * local.z) * dt, 0, maximumSpeed);
            local.x *= Mathf.Exp(-lateralGrip * dt);
            local.y = 0;
            Body.linearVelocity = transform.TransformDirection(local);
            currentSteering = Mathf.MoveTowards(currentSteering, steering, steeringResponse * dt);
            SteeringAngle = currentSteering * EffectiveSteeringLimit(local.z);
            // MoveTowards gives a trapezoidal ramp/hold/ramp profile for held keyboard commands.
            // Bicycle yaw approximation. Dynamic rigidbody translation still resolves collisions.
            float yaw = local.z / wheelbase * Mathf.Tan(SteeringAngle * Mathf.Deg2Rad);
            Body.angularVelocity = new Vector3(0, yaw, 0);
        }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponent<CrashBoundary>() != null) StopAsCrash();
        }
        public void StopAsCrash()
        {
            if (Crashed) return;
            Crashed = true; SetCommands(0, 0, 0);
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            Crash?.Invoke();
        }
        public void ResetVehicle(Vector3 position, Quaternion rotation)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            Body.isKinematic = false;
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position; Body.rotation = rotation;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
            currentSteering = 0; SteeringAngle = 0; SetCommands(0, 0, 0); Crashed = false;
            Physics.SyncTransforms();
            ResetPerformed?.Invoke();
        }
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>(); if (box == null) return;
            Gizmos.color = Color.cyan; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
