using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BReleaseTowerImport
{
    public const string Folder = "Assets/Scenes/Archive/BounceBack/Art/B-ReleaseTower";
    public static readonly Vector3 Anchor = new(-5.64f, -0.22f, -3.65f);
    private static readonly string[] Modules = { "BReleaseTower_Body", "BReleaseTower_Platform", "BReleaseTower_BellTower" };
    private static readonly Dictionary<string, Color> Palette = new()
    {
        ["MV_Ivory"] = new(0.95f, 0.88f, 0.77f),
        ["MV_IvoryShade"] = new(0.86f, 0.76f, 0.70f),
        ["MV_Rose"] = new(0.74f, 0.45f, 0.47f),
        ["MV_RoseDeep"] = new(0.55f, 0.30f, 0.38f),
        ["MV_Lilac"] = new(0.50f, 0.45f, 0.66f),
        ["MV_Apricot"] = new(0.90f, 0.58f, 0.37f),
        ["MV_DomeBlue"] = new(0.30f, 0.47f, 0.80f),
        ["MV_Mint"] = new(0.36f, 0.70f, 0.55f),
        ["MV_Water"] = new(0.45f, 0.80f, 0.84f),
        ["MV_Gold"] = new(0.96f, 0.70f, 0.32f),
    };

    private static void CreateMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP/Lit not found");
        foreach (var entry in Palette)
        {
            string path = $"{Folder}/Materials/{entry.Key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = entry.Value;
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }

    private static void RemapMaterials(GameObject instance)
    {
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            var materials = renderer.sharedMaterials.Select(original =>
            {
                string name = original ? original.name.Replace(" (Instance)", "") : "";
                var remap = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/Materials/{name}.mat");
                if (!remap) throw new InvalidOperationException($"No MV material remap for '{name}' on {renderer.name}");
                return remap;
            }).ToArray();
            renderer.sharedMaterials = materials;
        }
    }

    private static GameObject WrapModel(string name, Scene scene)
    {
        string path = $"{Folder}/FBX/{name}.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer) throw new FileNotFoundException("Missing FBX importer", path);
        importer.globalScale = 1f;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var wrapper = new GameObject(name);
        SceneManager.MoveGameObjectToScene(wrapper, scene);
        var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        imported.name = name + "_Imported";
        imported.transform.SetParent(wrapper.transform, false);
        imported.transform.localPosition = Vector3.zero;
        // Deliberately preserve imported axis rotation and file-unit scale.
        RemapMaterials(wrapper);
        return wrapper;
    }

    private static Bounds WorldBounds(MeshFilter filter)
    {
        var points = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
        var result = new Bounds(points[0], Vector3.zero);
        foreach (var point in points) result.Encapsulate(point);
        return result;
    }

    private static float BoundsError(Bounds actual, Vector3 center, Vector3 size)
    {
        var expected = new Bounds(center, size);
        return Mathf.Max(Mathf.Abs(actual.min.x - expected.min.x), Mathf.Abs(actual.min.y - expected.min.y),
            Mathf.Abs(actual.min.z - expected.min.z), Mathf.Abs(actual.max.x - expected.max.x),
            Mathf.Abs(actual.max.y - expected.max.y), Mathf.Abs(actual.max.z - expected.max.z));
    }

    private static string InspectCombined(GameObject instance)
    {
        instance.transform.position = Anchor;
        var filters = instance.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray();
        var core = filters.Single(f => f.sharedMesh.name.StartsWith("Body_Collision_Aligned_Core"));
        var slab = filters.Single(f => f.sharedMesh.name.StartsWith("Launch_Coping_Aligned"));
        var bell = filters.Single(f => f.sharedMesh.name.StartsWith("Bell_Pavilion_Dome_And_Flag"));
        var rails = filters.Single(f => f.sharedMesh.name.StartsWith("Platform_Broken_Railings"));
        float coreError = BoundsError(WorldBounds(core), new Vector3(-5.64f, 2.27f, -3.65f), new Vector3(1.77f, 4.02f, 1.79f));
        float slabError = BoundsError(WorldBounds(slab), new Vector3(-5.91f, 4.27f, -3.65f), new Vector3(2.83f, 0.36f, 1.92f));
        if (coreError > 0.0001f || slabError > 0.0001f)
            throw new InvalidOperationException($"Alignment failure: core={coreError}, slab={slabError}; actualCore={WorldBounds(core)}; actualSlab={WorldBounds(slab)}");
        var bellBounds = WorldBounds(bell);
        if (Mathf.Abs(bellBounds.max.y - 8.14f) > 0.0001f || bellBounds.min.x < -7.2801f || bellBounds.max.x > -6.5399f ||
            bellBounds.min.z < -4.5801f || bellBounds.max.z > -4.0799f)
            throw new InvalidOperationException("Bell footprint/top failure: " + bellBounds);
        var railBounds = WorldBounds(rails);
        if (railBounds.max.y - 4.45f > 0.3501f)
            throw new InvalidOperationException("Rail height exceeds 0.35 m");
        if (instance.GetComponentsInChildren<Collider>(true).Length != 0 || instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("Delivery prefab contains a collider or script");
        var lines = new List<string>
        {
            "B-ReleaseTower Unity asset validation",
            "Prefab outer wrapper: local position/rotation zero; scale one. FBX conversion remains internal.",
            $"Tower core bounds error: {coreError:F6} m",
            $"Launch Coping bounds error: {slabError:F6} m",
            $"Rail maximum above Y=4.45: {railBounds.max.y - 4.45f:F6} m",
            $"Bell world bounds: min={bellBounds.min}; max={bellBounds.max}",
            $"Colliders: 0; scripts: 0; external textures: 0",
        };
        int total = 0;
        foreach (string module in Modules)
        {
            string prefix = module.Replace("BReleaseTower_", "");
            var moduleFilters = filters.Where(f => f.transform.IsChildOf(instance.transform.Find(module))).ToArray();
            int triangles = moduleFilters.Sum(f => f.sharedMesh.triangles.Length / 3);
            total += triangles;
            if (triangles > 15000) throw new InvalidOperationException(module + " exceeds 15k triangles");
            foreach (var filter in moduleFilters)
            {
                if (filter.sharedMesh.subMeshCount > 4) throw new InvalidOperationException(filter.name + " exceeds four material slots");
                lines.Add($"{module}/{filter.sharedMesh.name}: triangles={filter.sharedMesh.triangles.Length / 3}; slots={filter.sharedMesh.subMeshCount}; materials={string.Join(",", filter.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name))}");
            }
            lines.Add($"{module}: module triangles={triangles}");
        }
        lines.Add($"Total delivery triangles: {total}");
        lines.Add("RESULT=PASS");
        return string.Join("\n", lines);
    }

    public static string Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        var scene = EditorSceneManager.NewPreviewScene();
        string report;
        try
        {
            foreach (string module in Modules)
            {
                var wrapper = WrapModel(module, scene);
                PrefabUtility.SaveAsPrefabAsset(wrapper, $"{Folder}/Prefabs/{module}.prefab");
                Object.DestroyImmediate(wrapper);
            }
            var combined = new GameObject("BReleaseTower");
            SceneManager.MoveGameObjectToScene(combined, scene);
            foreach (string module in Modules)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Prefabs/{module}.prefab");
                var child = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                child.name = module;
                child.transform.SetParent(combined.transform, false);
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;
            }
            report = InspectCombined(combined);
            // The reusable wrapper is origin-based; callers explicitly place it at Anchor.
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(combined, $"{Folder}/Prefabs/BReleaseTower.prefab");
            Object.DestroyImmediate(combined);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
        File.WriteAllText($"{Folder}/Documentation/BReleaseTower_Unity_Assets.txt", report + "\n");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("B_RELEASE_TOWER_ASSETS_PASS\n" + report);
        return report;
    }
}
