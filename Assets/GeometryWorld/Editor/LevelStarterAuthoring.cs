using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class LevelStarterAuthoring
    {
        internal const string PrefabPath = GeometryAssets.Prefabs + "/Starter/Gadgets/Spawn.prefab";

        [MenuItem("Tools/Set Up Ball and Piston")]
        private static void SelectFromMenu()
        {
            var level = SceneConstruction.Level;
            if (!level)
            {
                EditorUtility.DisplayDialog("Ball and Piston", "Open a Geometry World level first.", "OK");
                return;
            }
            Select(level);
        }

        internal static void Select(GeometryLevel level) => Selection.activeGameObject = Ensure(level, true).gameObject;

        internal static BallSpawn Ensure(GeometryLevel level, bool undo)
        {
            var spawn = level.spawn;
            if (!spawn || !spawn.transform.IsChildOf(level.transform))
            {
                spawn = level.GetComponentInChildren<BallSpawn>(true);
                if (!spawn)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                    var root = prefab && prefab.GetComponent<PistonLauncher>()
                        ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.transform)
                        : LevelAuthoring.NewObject("Ball and Piston", level.transform, level.gameObject.scene, level.transform.position, false);
                    root.transform.localPosition = Vector3.up * (LevelAuthoring.SpawnDiameter * 0.5f);
                    root.transform.localRotation = Quaternion.identity;
                    spawn = root.GetComponent<BallSpawn>();
                    if (!spawn)
                    {
                        spawn = root.AddComponent<BallSpawn>();
                        spawn.material = Material("Ivory", 0);
                    }
                    if (undo)
                        Undo.RegisterCreatedObjectUndo(root, "Create Ball and Piston");
                }
                if (undo)
                    Undo.RecordObject(level, "Set Ball and Piston");
                level.spawn = spawn;
                PrefabUtility.RecordPrefabInstancePropertyModifications(level);
            }

            if (level.requireStarterPiston)
                AddPiston(spawn, undo);
            return spawn;
        }

        internal static GameObject Move(GeometryLevel level, Transform parent, Vector3 position, bool undo)
        {
            var spawn = Ensure(level, undo);
            Transform start = spawn.transform;
            if (start.parent != parent)
            {
                if (undo)
                    Undo.SetTransformParent(start, parent, "Move Ball and Piston");
                else
                    start.SetParent(parent, true);
            }
            if (undo)
                Undo.RecordObject(start, "Move Ball and Piston");
            start.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(start);
            return spawn.gameObject;
        }

        internal static void AddPiston(BallSpawn spawn, bool undo)
        {
            var piston = spawn.GetComponent<PistonLauncher>();
            if (piston && piston.HasVisuals)
            {
                PistonArtwork.Apply(piston, undo);
                return;
            }
            if (!piston)
                piston = undo ? Undo.AddComponent<PistonLauncher>(spawn.gameObject) : spawn.gameObject.AddComponent<PistonLauncher>();
            Transform housing = piston.housing;
            Transform ram = piston.head;
            if (!housing || housing.parent != spawn.transform)
                housing = CreatePart(spawn.transform, "Piston Housing", Vector3.zero,
                    new Vector3(0.7f, 0.56f, PistonLauncher.HousingDepth), Material("Sky", 4), true, undo);
            if (!ram || ram.parent != spawn.transform)
            {
                ram = new GameObject("Piston Ram").transform;
                ram.SetParent(spawn.transform, false);
                CreatePart(ram, "Push Plate", Vector3.zero, new Vector3(0.46f, 0.46f, PistonLauncher.HeadDepth),
                    Material("Apricot", 3), false, false);
                CreatePart(ram, "Rod", Vector3.back * 0.32f, new Vector3(0.18f, 0.18f, 0.64f),
                    Material("Ivory", 0), false, false);
                if (undo)
                    Undo.RegisterCreatedObjectUndo(ram.gameObject, "Create Piston Ram");
            }
            if (undo)
                Undo.RecordObjects(new Object[] { piston, ram, housing }, "Align Piston");
            piston.head = ram;
            piston.housing = housing;
            piston.AlignVisuals();
            PrefabUtility.RecordPrefabInstancePropertyModifications(piston);
            PistonArtwork.Apply(piston, undo);
        }

        internal static int CountStarts(GeometryLevel level)
        {
            return level.gameObject.scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren<BallSpawn>(true).Length);
        }

        private static Material Material(string name, int palette) =>
            AssetDatabase.LoadAssetAtPath<Material>(GeometryAssets.Prefabs + $"/Starter/Materials/{name}.mat")
            ?? GeometryAssets.Preset(palette);

        private static Transform CreatePart(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool clickable, bool undo)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!clickable)
                Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            if (undo)
                Undo.RegisterCreatedObjectUndo(part, "Create " + name);
            return part.transform;
        }
    }
}
