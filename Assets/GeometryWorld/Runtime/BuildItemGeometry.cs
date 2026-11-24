using UnityEngine;

namespace BounceBack.GeometryWorld
{
    internal readonly struct BuildItemGeometry
    {
        internal readonly Bounds Bounds;
        internal readonly Vector3 Scale;
        internal readonly Mesh Mesh;
        internal readonly Matrix4x4 MeshToRoot;

        internal BuildItemGeometry(GameObject prefab, Vector3 scale)
        {
            Scale = Vector3.Scale(prefab ? prefab.transform.localScale : Vector3.one, scale);
            Bounds = new Bounds(Vector3.zero, Vector3.one);
            MeshToRoot = Matrix4x4.identity;
            Mesh = null;
            if (!prefab)
                return;
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                Mesh = filter.sharedMesh;
                MeshToRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds bounds = new(MeshToRoot.MultiplyPoint3x4(Mesh.bounds.min), Vector3.zero);
                for (int i = 1; i < 8; i++)
                {
                    Vector3 p = new((i & 1) == 0 ? Mesh.bounds.min.x : Mesh.bounds.max.x,
                        (i & 2) == 0 ? Mesh.bounds.min.y : Mesh.bounds.max.y,
                        (i & 4) == 0 ? Mesh.bounds.min.z : Mesh.bounds.max.z);
                    bounds.Encapsulate(MeshToRoot.MultiplyPoint3x4(p));
                }
                Bounds = bounds;
            }
            else if (prefab.TryGetComponent(out BoxCollider box))
                Bounds = new Bounds(box.center, box.size);
            else if (prefab.TryGetComponent(out SphereCollider sphere))
                Bounds = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
        }

        internal Vector3 Size => Vector3.Scale(Bounds.size, Scale);
        internal Vector3 CenterOffset(Quaternion rotation) => rotation * Vector3.Scale(Bounds.center, Scale);

        internal Pose RestOnFace(Pose cubePose, Vector3 normal, int quarterTurns)
        {
            Quaternion rotation = cubePose.rotation * Quaternion.Euler(0f, quarterTurns * 90f, 0f);
            Vector3 along = Quaternion.Inverse(rotation) * normal;
            Vector3 projected = new(Mathf.Abs(along.x), Mathf.Abs(along.y), Mathf.Abs(along.z));
            float reach = Vector3.Dot(projected, Size) * 0.5f;
            return new Pose(cubePose.position + normal * (reach - 0.5f) - CenterOffset(rotation), rotation);
        }
    }
}
