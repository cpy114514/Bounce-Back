using System.Collections;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BallBody : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public GeometryLevel Level { get; private set; }
        public bool Released { get; private set; }
        public bool InTransition { get; private set; }
        public float TransferAvailableAt { get; private set; }
        public float Speed => Body && Released && !InTransition ? Body.linearVelocity.magnitude : 0f;
        public event System.Action<float> SpeedChanged;
        public event System.Action<Collision> Collided;
        internal Vector3 IncomingVelocity { get; private set; }

        private Coroutine transfer;
        private CollisionDetectionMode rollingCollisionMode;
        private bool collisionSuppressed;
        private bool previousCollisionState;
        private float reportedSpeed;

        internal void Initialize(GeometryLevel level, float mass)
        {
            Level = level;
            Body = GetComponent<Rigidbody>();
            Body.mass = Mathf.Max(0.01f, mass);
            Body.isKinematic = true;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rollingCollisionMode = GetComponentInChildren<SphereCollider>()
                ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.ContinuousSpeculative;
            Body.maxAngularVelocity = 30f;
        }

        internal void Hold(Vector3 point, Quaternion rotation)
        {
            if (transfer != null)
                StopCoroutine(transfer);
            transfer = null;
            RestoreCollisions();
            InTransition = false;
            Released = false;
            TransferAvailableAt = Time.time + 0.1f;
            StopMotion();
            SetKinematic();
            Body.interpolation = RigidbodyInterpolation.None;
            Body.position = point;
            Body.rotation = rotation;
        }

        internal void Release(Vector3 velocity)
        {
            if (Released)
                return;
            Released = true;
            Body.isKinematic = false;
            Body.collisionDetectionMode = rollingCollisionMode;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.linearVelocity = velocity;
        }

        internal float SweepTravel(Vector3 destination)
        {
            Vector3 travel = destination - Body.position;
            float distance = travel.magnitude;
            if (distance <= 0.0001f)
                return 1f;
            Vector3 direction = travel / distance;
            float clearDistance = distance;
            foreach (var hit in Body.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore))
            {
                // A surface parallel to the stroke supports the ball without blocking it.
                if (Vector3.Dot(hit.normal, direction) < -0.01f)
                    clearDistance = Mathf.Min(clearDistance, Mathf.Max(0f, hit.distance - 0.001f));
            }
            return clearDistance / distance;
        }

        internal bool Compress(Transform surface, Vector3 outward, float duration, float depth, Vector3 rebound,
            System.Action released)
        {
            if (!Released || InTransition)
                return false;
            InTransition = true;
            previousCollisionState = Body.detectCollisions;
            collisionSuppressed = true;
            Body.detectCollisions = false;
            Vector3 spin = Body.angularVelocity;
            StopMotion();
            SetKinematic();
            transfer = StartCoroutine(CompressAndRelease(surface, outward, duration, depth, rebound, spin, released));
            return true;
        }

        private IEnumerator CompressAndRelease(Transform surface, Vector3 outward, float duration, float depth,
            Vector3 rebound, Vector3 spin, System.Action released)
        {
            Vector3 start = surface.InverseTransformPoint(Body.position);
            var physics = new WaitForFixedUpdate();
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.fixedDeltaTime)
            {
                if (!surface)
                    break;
                float fraction = Mathf.Clamp01(elapsed / duration);
                Body.MovePosition(surface.TransformPoint(start) - outward * (depth * Mathf.Sin(Mathf.PI * fraction)));
                yield return physics;
            }
            if (surface)
                Body.position = surface.TransformPoint(start) + outward * 0.04f;
            Body.isKinematic = false;
            Body.collisionDetectionMode = rollingCollisionMode;
            RestoreCollisions();
            Body.linearVelocity = rebound;
            Body.angularVelocity = spin;
            InTransition = false;
            transfer = null;
            released?.Invoke();
        }

        private void RestoreCollisions()
        {
            if (!collisionSuppressed)
                return;
            Body.detectCollisions = previousCollisionState;
            collisionSuppressed = false;
        }

        private void FixedUpdate()
        {
            if (Body && Released && !InTransition)
                IncomingVelocity = Body.linearVelocity + (Body.useGravity ? Physics.gravity * Time.fixedDeltaTime : Vector3.zero);
            if (SpeedChanged != null)
            {
                float speed = Speed;
                if (Mathf.Abs(speed - reportedSpeed) >= 0.1f || (speed == 0f && reportedSpeed > 0f))
                {
                    reportedSpeed = speed;
                    SpeedChanged.Invoke(speed);
                }
            }
        }

        public bool Transfer(Vector3 destination, Vector3 velocity, Vector3 spin, float duration, float cooldown)
        {
            if (!Released || InTransition || Time.time < TransferAvailableAt)
                return false;
            InTransition = true;
            StopMotion();
            SetKinematic();
            transfer = StartCoroutine(Cross(destination, velocity, spin, Mathf.Max(0.02f, duration), cooldown));
            return true;
        }

        private IEnumerator Cross(Vector3 destination, Vector3 velocity, Vector3 spin, float duration, float cooldown)
        {
            Vector3 start = Body.position;
            float elapsed = 0f;
            var physics = new WaitForFixedUpdate();
            while (elapsed < duration)
            {
                yield return physics;
                elapsed += Time.fixedDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / duration);
                Body.MovePosition(Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, fraction)));
            }
            Body.position = destination;
            Body.isKinematic = false;
            Body.collisionDetectionMode = rollingCollisionMode;
            Body.linearVelocity = velocity;
            Body.angularVelocity = spin;
            TransferAvailableAt = Time.time + Mathf.Max(0.05f, cooldown);
            InTransition = false;
            transfer = null;
        }

        private void SetKinematic()
        {
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.isKinematic = true;
        }

        private void StopMotion()
        {
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Released || InTransition)
                return;
            Collided?.Invoke(collision);
            if (Level && Level.audioDirector)
                Level.audioDirector.Play(Cue.Bounce, Mathf.Clamp01(collision.relativeVelocity.magnitude / 5f));
        }
    }
}
