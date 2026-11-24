using UnityEngine;

namespace BounceBack.GeometryWorld
{
    internal sealed class WorldRoute
    {
        internal readonly Vector3[] points;
        internal readonly float[] distance;
        internal float Length => distance[^1];

        internal WorldRoute(Transform[] waypoints)
        {
            points = new Vector3[waypoints.Length];
            distance = new float[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                points[i] = waypoints[i].position;
                if (i > 0)
                    distance[i] = distance[i - 1] + Vector3.Distance(points[i], points[i - 1]);
            }
        }

        internal int Segment(float along, bool forward = true)
        {
            for (int i = 1; i < distance.Length - 1; i++)
                if (forward ? along < distance[i] - 0.001f : along <= distance[i] + 0.001f)
                    return i - 1;
            return distance.Length - 2;
        }

        internal Vector3 At(float along)
        {
            int i = Segment(along);
            return Vector3.Lerp(points[i], points[i + 1], Mathf.InverseLerp(distance[i], distance[i + 1], along));
        }

        internal float AtWorldPosition(Vector3 position)
        {
            float best = float.PositiveInfinity;
            float along = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                float fraction = Mathf.Clamp01(Vector3.Dot(position - points[i], delta) / Mathf.Max(delta.sqrMagnitude, 0.0001f));
                float distanceSquared = (points[i] + fraction * delta - position).sqrMagnitude;
                if (distanceSquared >= best)
                    continue;
                best = distanceSquared;
                along = Mathf.Lerp(distance[i], distance[i + 1], fraction);
            }
            return along;
        }

        internal float Pick(Camera view, Vector2 cursor, float radius, float current, int gap)
        {
            float best = radius * radius;
            float along = current;
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (i == gap)
                    continue;
                Vector2 a = view.WorldToScreenPoint(points[i]);
                Vector2 b = view.WorldToScreenPoint(points[i + 1]);
                Vector2 delta = b - a;
                float fraction = Mathf.Clamp01(Vector2.Dot(cursor - a, delta) / Mathf.Max(delta.sqrMagnitude, 0.0001f));
                float target = Mathf.Lerp(distance[i], distance[i + 1], fraction);
                float score = (a + fraction * delta - cursor).sqrMagnitude + Mathf.Abs(target - current) * 0.01f;
                if (score >= best)
                    continue;
                best = score;
                along = target;
            }
            return along;
        }

        internal float KeyboardTarget(float along, Vector3 direction)
        {
            int next = Segment(along);
            int last = Segment(along, false);
            float forward = along >= Length - 0.001f ? -1f :
                Vector3.Dot(direction, Vector3.ProjectOnPlane(points[next + 1] - points[next], Vector3.up).normalized);
            float backward = along <= 0.001f ? -1f :
                Vector3.Dot(direction, Vector3.ProjectOnPlane(points[last] - points[last + 1], Vector3.up).normalized);
            if (Mathf.Max(forward, backward) < 0.12f)
                return along;
            return forward >= backward ? distance[next + 1] : distance[last];
        }
    }
}
