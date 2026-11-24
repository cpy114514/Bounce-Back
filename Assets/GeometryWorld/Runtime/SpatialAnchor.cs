using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SpatialAnchor : MonoBehaviour
    {
        private readonly List<SpaceConnection> connections = new();

        private void Reset() => GetComponent<SphereCollider>().isTrigger = true;

        internal void Register(SpaceConnection connection)
        {
            if (!connections.Contains(connection))
                connections.Add(connection);
        }

        internal void Unregister(SpaceConnection connection) => connections.Remove(connection);

        private void OnTriggerStay(Collider other)
        {
            if (!other.attachedRigidbody || !other.attachedRigidbody.TryGetComponent(out BallBody ball) || ball.InTransition)
                return;
            foreach (var connection in connections)
                if (connection.TryTraverse(this, ball))
                    break;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.58f, 0.9f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.7f);
        }
    }
}
