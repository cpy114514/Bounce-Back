using System.Collections;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BallSpawn))]
    public sealed class PistonLauncher : MonoBehaviour
    {
        public const float HeadDepth = 0.18f;
        public const float HousingDepth = 0.62f;
        private const float RodSeatDepth = 0.04f;

        public Transform head;
        public Transform housing;
        public Transform rod;
        [Min(0.02f), Tooltip("Distance the piston moves the ball before releasing it.")]
        public float stroke = 0.48f;
        [Min(0.02f)] public float pushTime = 0.14f;
        [Min(0.02f)] public float returnTime = 0.24f;

        public bool IsPushing => cycle != null;
        public bool HasVisuals => head && housing && head.parent == transform && housing.parent == transform;
        public Vector3 BallRestOffset => Vector3.back * Stroke;
        private Coroutine cycle;

        private Vector3 ReleaseHeadPosition =>
            Vector3.back * (GetComponent<BallSpawn>().diameter * 0.5f + HeadDepth * 0.5f);

        private float Stroke => Mathf.Max(0.02f, stroke);

        public void AlignVisuals()
        {
            if (!HasVisuals)
                return;
            Vector3 rest = ReleaseHeadPosition + BallRestOffset;
            housing.localPosition = rest + Vector3.back * (HousingDepth * 0.5f);
            SetHeadPosition(rest);
        }

        private void SetHeadPosition(Vector3 position)
        {
            head.localPosition = position;
            if (!rod || rod.parent != head)
                return;

            // The rod mesh runs from -1 to 0 on local Z. Its rear end stays inside the fixed guide sleeve.
            float seat = housing.localPosition.z + RodSeatDepth;
            Vector3 scale = rod.localScale;
            scale.z = Mathf.Max(0.01f, position.z + rod.localPosition.z - seat);
            rod.localScale = scale;
        }

        internal void Push(GeometryLevel level)
        {
            if (!IsPushing && isActiveAndEnabled && HasVisuals)
                cycle = StartCoroutine(Animate(level));
        }

        internal void ResetStroke()
        {
            if (IsPushing)
                StopCoroutine(cycle);
            cycle = null;
            AlignVisuals();
        }

        private IEnumerator Animate(GeometryLevel level)
        {
            AlignVisuals();
            Vector3 rest = head.localPosition;
            Vector3 release = ReleaseHeadPosition;
            BallBody ball = level.Ball;
            ball.Body.position = transform.TransformPoint(BallRestOffset);
            yield return Slide(rest, release, pushTime, false, ball);
            level.LaunchFromPiston(this);
            yield return Slide(head.localPosition, rest, returnTime, true);
            cycle = null;
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float duration, bool ease, BallBody ball = null)
        {
            duration = Mathf.Max(Time.fixedDeltaTime, duration);
            var wait = new WaitForFixedUpdate();
            for (float elapsed = 0f; elapsed < duration;)
            {
                yield return wait;
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(from, to, ease ? Mathf.SmoothStep(0f, 1f, t) : t);
                float travel = 1f;
                // The authored spawn is the ball's center at the end of the forward stroke.
                if (ball && !ball.Released)
                {
                    Vector3 destination = transform.TransformPoint(position - to);
                    travel = ball.SweepTravel(destination);
                    ball.Body.position = Vector3.Lerp(ball.Body.position, destination, travel);
                    position = Vector3.Lerp(head.localPosition, position, travel);
                }
                SetHeadPosition(position);
                if (travel < 1f)
                    yield break;
            }
        }

        private void OnDisable() => ResetStroke();

        private void OnDrawGizmosSelected()
        {
            Vector3 release = ReleaseHeadPosition;
            Gizmos.color = new Color(0.95f, 0.66f, 0.4f);
            Gizmos.DrawLine(transform.TransformPoint(release + BallRestOffset), transform.TransformPoint(release));
        }
    }
}
