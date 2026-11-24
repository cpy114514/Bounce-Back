using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class PassiveReflector : MonoBehaviour
    {
        [Range(0f, 1f)] public float restitution = 0.87f;
        [Min(0f)] public float contactPause;
        public event Action<BallBody, Vector3, Vector3> Reflected;

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.rigidbody || !collision.rigidbody.TryGetComponent(out BallBody ball) ||
                !ball.Released || ball.InTransition || collision.contactCount == 0)
                return;
            var outward = -collision.GetContact(0).normal;
            var incoming = ball.IncomingVelocity;
            float normalSpeed = Vector3.Dot(incoming, outward);
            if (normalSpeed >= -0.1f)
                return;
            var outgoing = incoming - (1f + restitution) * normalSpeed * outward;
            if (contactPause > 0f)
            {
                ball.Compress(transform, outward, contactPause, 0f, outgoing, () => Notify(ball, incoming, outgoing));
                return;
            }
            ball.Body.linearVelocity = outgoing;
            Notify(ball, incoming, outgoing);
        }

        private void Notify(BallBody ball, Vector3 incoming, Vector3 outgoing)
        {
            Reflected?.Invoke(ball, incoming, outgoing);
            if (ball.Level.audioDirector)
                ball.Level.audioDirector.Play(Cue.Rotation);
        }
    }
}
