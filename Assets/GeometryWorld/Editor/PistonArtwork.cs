using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class PistonArtwork
    {
        internal const string Folder = GeometryAssets.Prefabs + "/Starter/Art/Piston";
        private const string Materials = GeometryAssets.Prefabs + "/Starter/Materials";
        private const string RodMesh = Folder + "/Fluted Rod.asset";
        private static readonly string[] Palette = { "Ivory", "Apricot", "Rose", "Mint", "Lilac" };

        internal static bool Apply(PistonLauncher piston, bool undo)
        {
            if (!piston.HasVisuals)
                return false;
            var parts = new[]
            {
                (part: piston.housing, name: "Arch Housing"),
                (part: piston.head.Find("Push Plate"), name: "Octagonal Push Plate"),
                (part: piston.head.Find("Rod"), name: "Fluted Rod")
            };
            var materials = Palette.Select(name => AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat")).ToArray();
            if (materials.Any(material => !material))
                return false;

            bool changed = false;
            foreach (var (part, name) in parts)
            {
                if (!part || !part.TryGetComponent(out MeshFilter filter) || !part.TryGetComponent(out MeshRenderer renderer))
                    continue;
                var current = filter.sharedMesh;
                var replacement = AssetDatabase.LoadAssetAtPath<Mesh>($"{Folder}/{name}.asset");
                if (!replacement || !current || current == replacement || current.name != "Cube" || EditorUtility.IsPersistent(current) &&
                    AssetDatabase.GetAssetPath(current).StartsWith("Assets/", StringComparison.Ordinal))
                    continue;

                const string edit = "Update Piston Artwork";
                if (undo)
                    Undo.RecordObjects(new Object[] { part, filter, renderer }, edit);
                part.localScale = Vector3.one;
                filter.sharedMesh = replacement;
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

                if (part.TryGetComponent(out BoxCollider hitbox))
                {
                    if (undo)
                        Undo.RecordObject(hitbox, edit);
                    hitbox.center = replacement.bounds.center;
                    hitbox.size = replacement.bounds.size;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(hitbox);
                }
                changed = true;
            }
            return ConnectRod(piston, parts[2].part, undo) || changed;
        }

        private static bool ConnectRod(PistonLauncher piston, Transform rod, bool undo)
        {
            if (!rod || piston.rod == rod || !rod.TryGetComponent(out MeshFilter filter) ||
                !filter.sharedMesh || AssetDatabase.GetAssetPath(filter.sharedMesh) != RodMesh)
                return false;
            if (undo)
                Undo.RecordObjects(new Object[] { piston, rod, piston.head, piston.housing }, "Connect Piston Rod");
            piston.rod = rod;
            rod.localPosition = Vector3.zero;
            piston.AlignVisuals();
            PrefabUtility.RecordPrefabInstancePropertyModifications(piston);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rod);
            PrefabUtility.RecordPrefabInstancePropertyModifications(piston.head);
            PrefabUtility.RecordPrefabInstancePropertyModifications(piston.housing);
            return true;
        }
    }
}
