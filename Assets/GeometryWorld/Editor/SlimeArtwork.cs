using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class SlimeArtwork
    {
        private const string Materials = GeometryAssets.Prefabs + "/Starter/Materials";

        internal static Material Gel => GeometryAssets.CreateMaterial("Slime Gel", new Color32(85, 190, 116, 255),
            0f, 0.88f, 0.78f, reuse: true, directory: Materials);

        internal static void Tint(GeometryObject geometry, bool undo)
        {
            if (!geometry.surfaceRenderer)
                return;
            var renderer = geometry.surfaceRenderer;
            var gel = Gel;
            var materials = renderer.sharedMaterials;
            if (undo)
                Undo.RecordObject(renderer, "Apply Slime Material");
            renderer.sharedMaterials = Enumerable.Repeat(gel, Mathf.Max(1, materials.Length)).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
}
