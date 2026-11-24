using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class BouncePad : MonoBehaviour
    {
        [Min(0.1f)] public float speed = 4f;
        public Vector3 localDirection = Vector3.up;
        public bool preserveTangent = true;
        public PuzzleSequence onBounce;
        private float nextBounce;

        private void OnCollisionEnter(Collision other)
        {
            Vector3 direction = Direction;
            if (other.contactCount > 0 && Vector3.Dot(other.GetContact(0).normal, direction) > -0.1f)
                return;
            Bounce(other.rigidbody, direction);
        }

        private void OnTriggerEnter(Collider other) => Bounce(other.attachedRigidbody, Direction);

        private Vector3 Direction => localDirection.sqrMagnitude > 0.0001f
            ? transform.TransformDirection(localDirection.normalized) : transform.up;

        private void Bounce(Rigidbody body, Vector3 direction)
        {
            if (Time.time < nextBounce || !body || !body.TryGetComponent(out BallBody ball) ||
                !ball.Released || ball.InTransition)
                return;

            Vector3 tangent = preserveTangent ? Vector3.ProjectOnPlane(ball.Body.linearVelocity, direction) : Vector3.zero;
            ball.Body.linearVelocity = tangent + direction * speed;
            nextBounce = Time.time + 0.12f;
            if (ball.Level && ball.Level.audioDirector)
                ball.Level.audioDirector.Play(Cue.Bounce);
            if (onBounce)
                onBounce.Run();
        }
    }
}
