using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class GeometryUnitScale
    {
        private const float Tolerance = 0.00001f;

        internal static bool CanNormalize(GeometryObject geometry)
        {
            if (!geometry || geometry.editLocked || !geometry.model || !geometry.model.mesh ||
                !geometry.surface || !geometry.surfaceRenderer)
                return false;
            Transform surface = geometry.surface.transform;
            Collider collider = geometry.gameplayCollider;
            return surface.parent == geometry.transform && surface.localPosition == Vector3.zero &&
                surface.localRotation == Quaternion.identity && surface.localScale == Vector3.one &&
                (!collider || collider.transform == geometry.transform && collider is (BoxCollider or MeshCollider)) &&
                (Basis(geometry.model.mesh.bounds.size) - Vector3.one).sqrMagnitude > Tolerance * Tolerance;
        }

        internal static void Normalize(GeometryObject geometry, bool undo = true, string folder = GeometryAssets.Models)
        {
            if (!CanNormalize(geometry))
                return;
            const string change = "Normalize Geometry Scale";
            MeshTopology topology = geometry.model.topology.Copy();
            Vector3 basis = Basis(geometry.model.mesh.bounds.size);
            var unit = new Vector3(1f / basis.x, 1f / basis.y, 1f / basis.z);
            for (int i = 0; i < topology.vertices.Count; i++)
                topology.vertices[i] = Vector3.Scale(topology.vertices[i], unit);
            var model = GeometryAssets.CreateModel(geometry.model.name + " Unit", topology, geometry.model.materials,
                geometry.model.sourceDrawing, folder);
            Transform target = geometry.transform;
            Collider collider = geometry.gameplayCollider;
            if (undo)
            {
                Undo.RecordObjects(new Object[] { geometry, target, geometry.surface, geometry.surfaceRenderer }, change);
                if (collider)
                    Undo.RecordObject(collider, change);
            }
            target.localScale = Vector3.Scale(target.localScale, basis);
            if (collider is BoxCollider box)
            {
                box.center = Vector3.Scale(box.center, unit);
                box.size = Vector3.Scale(box.size, unit);
            }
            geometry.model = model;
            GeometryAssets.Sync(geometry, false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(geometry);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(geometry.surface);
            PrefabUtility.RecordPrefabInstancePropertyModifications(geometry.surfaceRenderer);
            if (collider)
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private static Vector3 Basis(Vector3 size) => new(size.x > Tolerance ? size.x : 1f,
            size.y > Tolerance ? size.y : 1f, size.z > Tolerance ? size.z : 1f);
    }
}
