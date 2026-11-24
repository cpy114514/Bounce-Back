// Editor-only, isolated-project asset build and import validation for K-02.
// This file is deliberately kept under ArtSource, not the runtime Assets assembly.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class K02TrimValidation
{
    private const string Root = "Assets/Scenes/Archive/BounceBack/Art/K-02";

    [Serializable]
    private class Surface
    {
        public float r, g, b, metallic, roughness, coat;
    }

    [Serializable]
    private class Palette
    {
        public Surface white_stone, cobalt_blue, brass;
    }

    [Serializable]
    private class AssetResult
    {
        public string name;
        public float[] dimensions;
        public float[] boundsMin;
        public float[] boundsMax;
        public int vertices;
        public int triangles;
        public int meshObjects;
        public int materialSlots;
        public int uvLayers;
        public bool mountingPivotPassed;
        public bool visualOnlyPassed;
    }

    [Serializable]
    private class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope;
        public string error;
        public List<AssetResult> assets = new List<AssetResult>();
    }

    private sealed class Spec
    {
        public string Name;
        public Surface Surface;
        public Vector3 Expected;
        public Material Material;
        public string FbxPath => $"{Root}/FBX/{Name}.fbx";
        public string PrefabPath => $"{Root}/Prefabs/{Name}.prefab";
    }

    public static void BuildAndValidate()
    {
        string output = GetOutputDirectory();
        Report report = new Report
        {
            unityVersion = Application.unityVersion,
            scope = "Isolated import/render validation only; no formal scene, physics, wall, angle or travel changes."
        };
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string palettePath = Path.GetFullPath(Path.Combine(Application.dataPath, "Scenes/BounceBack/Art/K-02/Documentation/K02_Palette.json"));
            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(palettePath));
            EnsureAssetFolder($"{Root}/Materials");
            EnsureAssetFolder($"{Root}/Prefabs");
            Spec[] specs =
            {
                new Spec { Name = "K02_White_Stone_Guide_Seat", Surface = palette.white_stone, Expected = new Vector3(0.340f, 0.560f, 0.120f) },
                new Spec { Name = "K02_Cobalt_Ceramic_Slider_Facing", Surface = palette.cobalt_blue, Expected = new Vector3(0.260f, 0.380f, 0.057f) },
                new Spec { Name = "K02_Brass_Axle_Cap", Surface = palette.brass, Expected = new Vector3(0.200f, 0.200f, 0.086f) }
            };
            foreach (Spec spec in specs)
            {
                ConfigureImporter(spec.FbxPath);
                spec.Material = CreateMaterial(spec.Name, spec.Surface);
            }
            AssetDatabase.SaveAssets();
            foreach (Spec spec in specs)
                report.assets.Add(BuildPrefabAndValidate(spec));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (Environment.GetCommandLineArgs().Contains("--k02-no-render"))
                Debug.Log("K02_RENDER_SKIPPED: headless import/Prefab validation only; Blender previews remain available.");
            else
                RenderAssembly(specs, Path.Combine(output, "Preview", "K02_Assembled_Example_Unity.png"));
            report.passed = report.assets.Count == 3 && report.assets.All(asset => asset.mountingPivotPassed && asset.visualOnlyPassed);
            if (!report.passed)
                throw new InvalidOperationException("One or more K-02 asset checks failed.");
            Debug.Log("K02_UNITY_VALIDATION_PASSED: three independent meshes, mounting pivots, dimensions, URP materials and collider-free Prefabs.");
            File.WriteAllText(Path.Combine(output, "Documentation", "K02_Unity_Validation.json"), JsonUtility.ToJson(report, true));
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            report.passed = false;
            report.error = exception.ToString();
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "Documentation", "K02_Unity_Validation.json"), JsonUtility.ToJson(report, true));
            Debug.LogError("K02_UNITY_VALIDATION_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    private static string GetOutputDirectory()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--k02-output");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("Missing --k02-output <absolute directory>.");
        string output = args[index + 1];
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "Preview"));
        Directory.CreateDirectory(Path.Combine(output, "Documentation"));
        return output;
    }

    private static void EnsureAssetFolder(string path)
    {
        Directory.CreateDirectory(Path.GetFullPath(path));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new FileNotFoundException("FBX did not import", path);
        importer.globalScale = 1f;
        importer.useFileUnits = true;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        // Unity 6.3 no longer exposes the legacy addColliders property.
        // The FBX has no collision nodes and the generated Prefab is checked for zero Colliders below.
        importer.isReadable = true;
        importer.generateSecondaryUV = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial(string name, Surface surface)
    {
        string path = $"{Root}/Materials/{name}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP/Lit shader is unavailable in the isolated project.");
        Material material = new Material(shader) { name = name, enableInstancing = true };
        Color color = new Color(surface.r, surface.g, surface.b, 1f);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", surface.metallic);
        material.SetFloat("_Smoothness", 1f - surface.roughness);
        material.SetFloat("_ClearCoatMask", surface.coat);
        material.SetFloat("_ClearCoatSmoothness", 0.82f);
        if (surface.coat > 0f)
            material.EnableKeyword("_CLEARCOAT");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static AssetResult BuildPrefabAndValidate(Spec spec)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
        if (model == null)
            throw new InvalidOperationException("No imported model root: " + spec.FbxPath);
        GameObject root = new GameObject(spec.Name);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        instance.name = spec.Name + "_Imported";
        instance.transform.localPosition = Vector3.zero;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != 1)
            throw new InvalidOperationException($"{spec.Name}: expected exactly one MeshRenderer, got {renderers.Length}");
        renderers[0].sharedMaterials = Enumerable.Repeat(spec.Material, Math.Max(1, renderers[0].sharedMaterials.Length)).ToArray();
        renderers[0].shadowCastingMode = ShadowCastingMode.On;
        renderers[0].receiveShadows = true;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath) != null)
            AssetDatabase.DeleteAsset(spec.PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath);
        AssetResult result = Validate(root, spec);
        UnityEngine.Object.DestroyImmediate(root);
        return result;
    }

    private static AssetResult Validate(GameObject root, Spec spec)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 1 || filters[0].sharedMesh == null)
            throw new InvalidOperationException($"{spec.Name}: expected one imported mesh.");
        Mesh mesh = filters[0].sharedMesh;
        Bounds bounds = LocalBounds(root.transform, filters);
        Vector3 size = bounds.size;
        bool dimensions = Vector3.Distance(size, spec.Expected) < 0.0001f && Mathf.Max(size.x, size.y, size.z) < 0.600001f;
        bool mountingPlane = Mathf.Abs(bounds.min.z) < 0.0001f;
        bool centered = Mathf.Abs(bounds.center.x) < 0.0001f && Mathf.Abs(bounds.center.y) < 0.0001f;
        bool identityRoot = root.transform.localPosition == Vector3.zero &&
                            root.transform.localRotation == Quaternion.identity && root.transform.localScale == Vector3.one;
        bool visualOnly = root.GetComponentsInChildren<Collider>(true).Length == 0 &&
                          root.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
                          root.GetComponentsInChildren<Animator>(true).Length == 0 &&
                          root.GetComponentsInChildren<Animation>(true).Length == 0 &&
                          root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
        bool validMesh = mesh.subMeshCount == 1 && mesh.uv.Length == mesh.vertexCount && mesh.normals.Length == mesh.vertexCount &&
                         mesh.vertices.All(v => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z));
        if (!dimensions || !mountingPlane || !centered || !identityRoot || !visualOnly || !validMesh)
            throw new InvalidOperationException($"{spec.Name}: dimensions={size}, min={bounds.min}, center={bounds.center}, " +
                                                $"identityRoot={identityRoot}, visualOnly={visualOnly}, validMesh={validMesh}");
        return new AssetResult
        {
            name = spec.Name,
            dimensions = ToArray(size),
            boundsMin = ToArray(bounds.min),
            boundsMax = ToArray(bounds.max),
            vertices = mesh.vertexCount,
            triangles = mesh.triangles.Length / 3,
            meshObjects = filters.Length,
            materialSlots = mesh.subMeshCount,
            uvLayers = mesh.uv4.Length > 0 ? 4 : mesh.uv3.Length > 0 ? 3 : mesh.uv2.Length > 0 ? 2 : 1,
            mountingPivotPassed = dimensions && mountingPlane && centered && identityRoot,
            visualOnlyPassed = visualOnly && validMesh
        };
    }

    private static Bounds LocalBounds(Transform root, MeshFilter[] filters)
    {
        bool initialized = false;
        Bounds result = new Bounds();
        foreach (MeshFilter filter in filters)
        {
            Bounds b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                    (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!initialized)
                {
                    result = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static void RenderAssembly(Spec[] specs, string output)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject[] instances =
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(specs[0].PrefabPath)) as GameObject,
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(specs[1].PrefabPath)) as GameObject,
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(specs[2].PrefabPath)) as GameObject
        };
        instances[0].transform.position = Vector3.zero;
        instances[1].transform.position = new Vector3(0, 0, 0.120f);
        instances[2].transform.position = new Vector3(0, 0, 0.177f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.61f, 0.68f);
        RenderSettings.fog = false;
        CreateLight("Key", new Quaternion(), 1.35f, new Color(1f, 0.93f, 0.83f), new Vector3(32, -28, -18));
        CreateLight("Fill", new Quaternion(), 0.45f, new Color(0.74f, 0.85f, 1f), new Vector3(-20, 145, 25));
        GameObject cameraObject = new GameObject("Validation Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.46f, 0.59f, 0.65f);
        camera.orthographic = true;
        camera.orthographicSize = 0.37f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        camera.transform.position = new Vector3(0.58f, 0.45f, 1.15f);
        camera.transform.LookAt(new Vector3(0, 0, 0.055f));
        RenderTexture target = new RenderTexture(1200, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(1200, 1200, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0);
        image.Apply();
        File.WriteAllBytes(output, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        foreach (GameObject instance in instances)
            UnityEngine.Object.DestroyImmediate(instance);
    }

    private static void CreateLight(string name, Quaternion rotation, float intensity, Color color, Vector3 euler)
    {
        GameObject obj = new GameObject(name);
        Light light = obj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        obj.transform.rotation = Quaternion.Euler(euler);
    }

    private static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
