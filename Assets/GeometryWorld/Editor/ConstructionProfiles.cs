using System;
using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal sealed class ConstructionProfile
    {
        private const string Starter = GeometryAssets.Prefabs + "/Starter/";
        internal const string Metric = Starter + "Metric/";
        private readonly string path;

        internal string Name { get; }
        internal string Hint { get; }
        internal PuzzleItem? Gadget { get; }
        internal GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(Metric + path)
            ?? AssetDatabase.LoadAssetAtPath<GameObject>(Starter + path);
        internal Bounds Bounds => ConstructionPreview.From(Prefab).Bounds;

        internal ConstructionProfile(string name)
        {
            Name = name;
            Hint = $"Place a {name.ToLowerInvariant()}.";
            path = "Shapes/" + name + ".prefab";
        }

        internal ConstructionProfile(string name, PuzzleItem gadget, string hint)
        {
            Name = name;
            Hint = hint;
            Gadget = gadget;
            path = "Gadgets/" + ObjectNames.NicifyVariableName(gadget.ToString()) + ".prefab";
        }

        internal GameObject Place(GeometryLevel level, Transform parent, Vector3 position, Quaternion localRotation)
            => Place(level, parent, Prefab, position, localRotation, true);

        internal GameObject Place(GeometryLevel level, Transform parent, GameObject prefab, Vector3 position,
            Quaternion localRotation, bool undo)
        {
            if (!prefab)
                throw new InvalidOperationException($"Starter Prefab is missing: {path}");
            return LevelAuthoring.PlaceStarter(level, parent, prefab, Gadget, position, localRotation, undo);
        }
    }
}
