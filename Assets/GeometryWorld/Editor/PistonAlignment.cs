using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class PistonAlignment
    {
        internal static bool TryAlign(PistonLauncher piston, GeometryObject block, Ray ray)
        {
            if (!block || !block.gameplayCollider || !block.gameplayCollider.enabled ||
                !block.gameplayCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity) ||
                !ConstructionDimensions.TryBounds(block.gameObject, out Bounds bounds, includeLocked: true) ||
                !TryPose(piston, block.transform, bounds, hit.point, hit.normal, out Pose pose))
                return false;

            Transform starter = piston.transform;
            Undo.RecordObject(starter, "Snap Piston to Block");
            starter.SetPositionAndRotation(pose.position, pose.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(starter);
            return true;
        }

        internal static bool TryPose(PistonLauncher piston, Transform block, Bounds bounds,
            Vector3 surface, Vector3 normal, out Pose pose)
        {
            pose = default;
            if (Vector3.Dot(normal, block.up) < 0.97f)
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(block.forward, normal).normalized;
            Vector3 right = Vector3.ProjectOnPlane(block.right, normal).normalized;
            Vector3 heading = piston.transform.forward;
            float along = Vector3.Dot(heading, forward);
            float across = Vector3.Dot(heading, right);
            bool useRight = Mathf.Abs(across) > Mathf.Abs(along);
            Vector3 direction = useRight ? right * Mathf.Sign(across) : forward * Mathf.Sign(along);
            Quaternion rotation = Quaternion.LookRotation(direction, normal);

            Transform starter = piston.transform;
            Transform parent = starter.parent;
            Vector3 localStroke = Vector3.Scale(-piston.BallRestOffset, starter.localScale);
            Vector3 stroke = parent ? parent.TransformVector(Quaternion.Inverse(parent.rotation) * rotation * localStroke)
                : rotation * localStroke;

            int axis = useRight ? 0 : 2;
            int lateralAxis = useRight ? 2 : 0;
            Vector3 rest = block.InverseTransformPoint(surface);
            rest[lateralAxis] = bounds.center[lateralAxis];
            float localStrokeDistance = block.InverseTransformVector(stroke)[axis];
            const float edgeMargin = 0.02f;
            float min = bounds.min[axis] + edgeMargin - Mathf.Min(0f, localStrokeDistance);
            float max = bounds.max[axis] - edgeMargin - Mathf.Max(0f, localStrokeDistance);
            if (min <= max)
                rest[axis] = Mathf.Clamp(rest[axis], min, max);

            Vector3 top = block.TransformPoint(rest);
            top += Vector3.Project(surface - top, normal);
            float radius = piston.GetComponent<BallSpawn>().diameter * 0.5f;
            pose = new Pose(top + normal * (radius + 0.001f) + stroke, rotation);
            return true;
        }
    }
}
