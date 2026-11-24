using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class BallCollisionEvent : MonoBehaviour
    {
        [Min(0f)] public float minimumImpact;
        public PuzzleSequence onCollision;

        private void OnCollisionEnter(Collision collision)
        {
            if (onCollision && collision.rigidbody && collision.rigidbody.TryGetComponent<BallBody>(out _) &&
                collision.relativeVelocity.magnitude >= minimumImpact)
                onCollision.Run();
        }
    }
}
