using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal readonly struct ConstructionPlane
    {
        internal readonly Vector3 Origin;
        internal readonly Quaternion Rotation;
        internal Vector3 Normal => Rotation * Vector3.up;

        internal ConstructionPlane(Vector3 origin, Vector3 normal)
        {
            Origin = origin;
            Rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }

        internal bool Intersect(Ray ray, out Vector3 point)
        {
            bool hit = new Plane(Normal, Origin).Raycast(ray, out float distance);
            point = hit ? ray.GetPoint(distance) : default;
            return hit;
        }

        internal Vector3 Snap(Vector3 point, Vector3 step)
        {
            var local = Quaternion.Inverse(Rotation) * (point - Origin);
            var snapped = SceneConstruction.Round(local, new Vector3(step.x, 0f, step.z));
            return Origin + Rotation * snapped;
        }

        internal void Draw(Vector3 gridStep)
        {
            float width = Mathf.Clamp(gridStep.x, 0.25f, 2f);
            float depth = Mathf.Clamp(gridStep.z, 0.25f, 2f);
            const int lines = 5;
            using var scope = new Handles.DrawingScope(Matrix4x4.TRS(Origin + Normal * 0.004f, Rotation, Vector3.one));
            for (int index = -lines; index <= lines; index++)
            {
                Handles.color = new Color(0.43f, 0.94f, 0.80f, index == 0 ? 0.82f : 0.30f);
                float x = index * width;
                float z = index * depth;
                Handles.DrawLine(new Vector3(x, 0f, -lines * depth), new Vector3(x, 0f, lines * depth));
                Handles.DrawLine(new Vector3(-lines * width, 0f, z), new Vector3(lines * width, 0f, z));
            }
        }
    }
}
