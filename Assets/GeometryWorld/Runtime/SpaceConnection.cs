using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum ConnectionActivation { Manual, CameraAligned }
    public enum ConnectionVelocity { PreserveWorld, AlignToExit, ExitForward }

    public sealed class SpaceConnection : MonoBehaviour
    {
        public SpatialAnchor source;
        public SpatialAnchor destination;
        public Camera alignmentCamera;
        public ConnectionActivation activation;
        public ConnectionVelocity velocityMode = ConnectionVelocity.AlignToExit;
        public bool enabledByLogic = true;
        public bool bidirectional;
        [Range(0.002f, 0.2f)] public float viewportTolerance = 0.035f;
        [Min(0.02f)] public float travelTime = 0.18f;
        [Min(0f)] public float exitClearance = 0.35f;
        [Min(0f)] public float minimumExitSpeed = 1f;
        public PuzzleSequence onActivated;
        public PuzzleSequence onCrossed;

        private bool active;

        private void OnEnable()
        {
            if (source)
                source.Register(this);
            if (destination && bidirectional)
                destination.Register(this);
        }

        private void OnDisable()
        {
            active = false;
            if (source)
                source.Unregister(this);
            if (destination)
                destination.Unregister(this);
        }

        private void Update()
        {
            bool ready = IsReady();
            if (ready && !active && onActivated)
                onActivated.Run();
            active = ready;
        }

        public bool IsAligned()
        {
            if (!alignmentCamera || !source || !destination)
                return false;
            Vector3 a = alignmentCamera.WorldToViewportPoint(source.transform.position);
            Vector3 b = alignmentCamera.WorldToViewportPoint(destination.transform.position);
            if (!InFrame(a) || !InFrame(b))
                return false;
            var difference = new Vector2((a.x - b.x) * alignmentCamera.aspect, a.y - b.y);
            return difference.sqrMagnitude <= viewportTolerance * viewportTolerance;
        }

        private static bool InFrame(Vector3 point) =>
            point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;

        public bool IsReady() => isActiveAndEnabled && enabledByLogic && source && destination && source != destination &&
            source.isActiveAndEnabled && destination.isActiveAndEnabled &&
            (activation == ConnectionActivation.Manual || IsAligned());

        internal bool TryTraverse(SpatialAnchor entry, BallBody ball)
        {
            if (!IsReady() || !ball.Released || (entry != source && (!bidirectional || entry != destination)))
                return false;
            Transform entrance = entry.transform;
            Transform exit = (entry == source ? destination : source).transform;
            Vector3 velocity = ball.Body.linearVelocity;
            Vector3 spin = ball.Body.angularVelocity;
            if (velocityMode == ConnectionVelocity.AlignToExit)
            {
                Quaternion turn = exit.rotation * Quaternion.Inverse(entrance.rotation);
                velocity = turn * velocity;
                spin = turn * spin;
            }
            else if (velocityMode == ConnectionVelocity.ExitForward)
            {
                velocity = exit.forward * Mathf.Max(velocity.magnitude, minimumExitSpeed);
            }
            if (velocity.magnitude < minimumExitSpeed)
                velocity = exit.forward * minimumExitSpeed;

            if (!ball.Transfer(exit.position + exit.forward * exitClearance, velocity, spin, travelTime, 0.4f))
                return false;
            if (ball.Level && ball.Level.audioDirector)
                ball.Level.audioDirector.Play(Cue.Connection);
            if (onCrossed)
                onCrossed.Run();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!source || !destination)
                return;
            Gizmos.color = IsReady() ? new Color(0.4f, 1f, 0.75f) : new Color(1f, 0.65f, 0.35f);
            Gizmos.DrawLine(source.transform.position, destination.transform.position);
            Gizmos.DrawWireSphere(source.transform.position, 0.32f);
            Gizmos.DrawWireSphere(destination.transform.position, 0.32f);
        }
    }
}
