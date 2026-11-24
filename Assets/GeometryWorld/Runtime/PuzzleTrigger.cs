using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum TriggerRole { EventZone, Goal, Checkpoint, Reset }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class PuzzleTrigger : MonoBehaviour
    {
        public TriggerRole role;
        public Transform checkpoint;
        public PuzzleSequence onEnter;
        public PuzzleSequence onExit;
        public PuzzleSequence onGoal;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.attachedRigidbody || !other.attachedRigidbody.TryGetComponent(out BallBody ball) ||
                !ball.Released || ball.InTransition || !ball.Level)
                return;

            switch (role)
            {
                case TriggerRole.Goal when !ball.Level.Completed && ball.Level.goal == this:
                    ball.Level.ReachGoal();
                    if (onGoal)
                        onGoal.Run();
                    break;
                case TriggerRole.Checkpoint:
                    ball.Level.SetCheckpoint(checkpoint ? checkpoint : transform);
                    break;
                case TriggerRole.Reset:
                    ball.Level.FailBall();
                    break;
            }
            if (onEnter)
                onEnter.Run();
        }

        private void OnTriggerExit(Collider other)
        {
            if (onExit && other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<BallBody>(out _))
                onExit.Run();
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (!box)
                return;
            Matrix4x4 matrix = Gizmos.matrix;
            Color color = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = role == TriggerRole.Goal ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = matrix;
            Gizmos.color = color;
        }
    }
}
