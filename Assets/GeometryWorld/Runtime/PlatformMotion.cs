using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class PlatformMotion : MonoBehaviour
    {
        public bool automatic = true;
        public bool pingPong = true;
        public Vector3 localTravel;
        public Vector3 localEulerTravel;
        [Min(0.1f)] public float duration = 2.5f;
        public AdaptivePuzzleAudio audioDirector;
        public PuzzleSequence onMovementCompleted;

        private Rigidbody body;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float elapsed;
        private bool outward = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
        }

        private void FixedUpdate()
        {
            if (!automatic)
                return;
            elapsed += Time.fixedDeltaTime;
            float fraction = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            float t = Mathf.SmoothStep(0f, 1f, outward ? fraction : 1f - fraction);
            SetPose(transform, body, initialPosition + localTravel * t, initialRotation * Quaternion.Euler(localEulerTravel * t));
            if (audioDirector)
                audioDirector.ReportMovement(Mathf.Sin(fraction * Mathf.PI) * 0.75f);
            if (fraction < 1f)
                return;
            NotifyMovementCompleted();
            elapsed = 0f;
            if (pingPong)
                outward = !outward;
            else
                automatic = false;
        }

        public void NotifyMovementCompleted()
        {
            if (onMovementCompleted)
                onMovementCompleted.Run();
        }

        internal static void SetPose(Transform target, Rigidbody body, Vector3 localPosition, Quaternion localRotation)
        {
            if (!body || !body.isKinematic)
            {
                target.SetLocalPositionAndRotation(localPosition, localRotation);
                return;
            }
            Transform parent = target.parent;
            body.Move(parent ? parent.TransformPoint(localPosition) : localPosition, parent ? parent.rotation * localRotation : localRotation);
        }
    }
}
