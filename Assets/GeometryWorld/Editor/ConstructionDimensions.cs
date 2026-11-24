using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class ConstructionDimensions
    {
        private const float MinimumAxis = 0.00001f;

        internal static bool TryMeasure(GameObject subject, out Vector3 dimensions)
        {
            dimensions = Vector3.zero;
            if (!TryBounds(subject, out Bounds bounds))
                return false;
            Vector3 scale = subject.transform.localScale;
            dimensions = Vector3.Scale(BaseSize(subject.transform, bounds), new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            return true;
        }

        internal static bool Resize(GameObject subject, Vector3 size)
        {
            if (!TryBounds(subject, out Bounds bounds))
                return false;
            Transform target = subject.transform;
            Vector3 basis = BaseSize(target, bounds);
            Vector3 oldScale = target.localScale;
            Vector3 scale = oldScale;
            for (int axis = 0; axis < 3; axis++)
                if (basis[axis] > MinimumAxis && float.IsFinite(size[axis]) && size[axis] > MinimumAxis)
                    scale[axis] = Mathf.Sign(oldScale[axis]) * size[axis] / basis[axis];
            if (scale == oldScale)
                return false;

            var basePoint = bounds.center;
            basePoint.y = oldScale.y < 0f ? bounds.max.y : bounds.min.y;
            Vector3 anchor = target.TransformPoint(basePoint);
            Undo.RecordObject(target, "Resize Geometry");
            target.localScale = scale;
            target.position += anchor - target.TransformPoint(basePoint);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            SceneView.RepaintAll();
            return true;
        }

        private static Vector3 BaseSize(Transform target, Bounds bounds)
        {
            var parent = target.parent ? target.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 axes = parent * Matrix4x4.Rotate(target.localRotation);
            return new Vector3(axes.MultiplyVector(Vector3.right).magnitude * bounds.size.x,
                axes.MultiplyVector(Vector3.up).magnitude * bounds.size.y,
                axes.MultiplyVector(Vector3.forward).magnitude * bounds.size.z);
        }

        internal static bool TryBounds(GameObject subject, out Bounds bounds, bool includeLocked = false)
        {
            bounds = default;
            if (!subject)
                return false;
            if (subject.TryGetComponent(out GeometryObject geometry) && geometry.surface && geometry.surface.sharedMesh)
            {
                if (geometry.editLocked && !includeLocked)
                    return false;
                Bounds mesh = geometry.surface.sharedMesh.bounds;
                Matrix4x4 local = subject.transform.worldToLocalMatrix * geometry.surface.transform.localToWorldMatrix;
                bounds = new Bounds(local.MultiplyPoint3x4(mesh.min), Vector3.zero);
                for (int i = 1; i < 8; i++)
                {
                    var point = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    bounds.Encapsulate(local.MultiplyPoint3x4(point));
                }
                return true;
            }
            if (subject.GetComponent<PuzzleTrigger>() && subject.TryGetComponent(out BoxCollider box))
            {
                bounds = new Bounds(box.center, box.size);
                return true;
            }
            return false;
        }
    }
}
