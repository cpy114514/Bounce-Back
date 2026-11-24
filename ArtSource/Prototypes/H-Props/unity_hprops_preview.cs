// H-Props isolated Unity delivery builder. This Editor utility is not a prop
// component and is not copied into the main project's Assets folder.
// In the isolated QA project:
// Unity.exe -batchmode -projectPath ".utmp/H-Props-QA" -executeMethod HPropsEditor.BuildAndCapture -quit
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class HPropsEditor
{
    private const string Folder = "Assets/Scenes/Archive/BounceBack/Art/H-Props";
    private const string ScenePath = Folder + "/Preview/H-Props_Preview.unity";
    private static readonly Dictionary<string, Vector3> Palette = new()
    {
        ["MV_Ivory"] = new Vector3(0.95f, 0.88f, 0.77f),
        ["MV_IvoryShade"] = new Vector3(0.86f, 0.76f, 0.70f),
        ["MV_Rose"] = new Vector3(0.74f, 0.45f, 0.47f),
        ["MV_RoseDeep"] = new Vector3(0.55f, 0.30f, 0.38f),
        ["MV_Lilac"] = new Vector3(0.50f, 0.45f, 0.66f),
        ["MV_Apricot"] = new Vector3(0.90f, 0.58f, 0.37f),
        ["MV_DomeBlue"] = new Vector3(0.30f, 0.47f, 0.80f),
        ["MV_Mint"] = new Vector3(0.36f, 0.70f, 0.55f),
        ["MV_Water"] = new Vector3(0.45f, 0.80f, 0.84f),
        ["MV_Gold"] = new Vector3(0.96f, 0.70f, 0.32f)
    };
    private static readonly string[] Names =
    {
        "H_Bougainvillea_Square", "H_Bougainvillea_Round", "H_Blue_Shutters_Pair",
        "H_Bunting_2m", "H_Hanging_Lantern", "H_Copper_Bell", "H_Domed_Chimney",
        "H_Roof_Cupola", "H_Seagull_Glide", "H_Seagull_Upstroke", "H_Seagull_Bank",
        "H_Terracotta_Amphora", "H_Terracotta_Bowl"
    };
    private static readonly List<string> Report = new();
    private static readonly Dictionary<string, Material> Materials = new();

    [Serializable]
    private sealed class SourceModel
    {
        public string name;
        public float[] dimensions_unity_xyz_m;
        public int triangles;
    }
    [Serializable]
    private sealed class SourceReport
    {
        public SourceModel[] models;
    }

    [MenuItem("H-Props/Build Isolated Preview")]
    public static void BuildAndCapture()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        if (!project.EndsWith(".utmp/H-Props-QA", StringComparison.OrdinalIgnoreCase) &&
            !project.EndsWith(".utmp\\H-Props-QA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("H-Props preview builder only runs in .utmp/H-Props-QA.");
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Prefabs");
        Directory.CreateDirectory(Folder + "/Documentation");
        Directory.CreateDirectory(Folder + "/Preview");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        ImportModels();
        CreatePrefabsAndValidate();
        BuildPreviewScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        File.WriteAllText(Folder + "/Documentation/H-Props_Unity_Validation.txt", string.Join("\n", Report) + "\n", Encoding.UTF8);
        Debug.Log("HPROPS_UNITY_PASS " + string.Join(" | ", Report));
    }

    private static void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP/Lit shader is missing.");
        foreach (var pair in Palette)
        {
            string path = Folder + "/Materials/" + pair.Key + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = new Color(pair.Value.x, pair.Value.y, pair.Value.z, 1);
            mat.SetFloat("_Smoothness", 0.18f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            Color stored = mat.color;
            float colorError = Mathf.Max(Mathf.Abs(stored.r - pair.Value.x), Mathf.Abs(stored.g - pair.Value.y), Mathf.Abs(stored.b - pair.Value.z));
            if (colorError > 0.000001f || Mathf.Abs(stored.a - 1) > 0.000001f)
                throw new InvalidOperationException("Palette mismatch: " + pair.Key);
            Report.Add(pair.Key + ": sRGB=" + F(pair.Value) + "; maxChannelError=" + colorError.ToString("F8", CultureInfo.InvariantCulture));
            Materials[pair.Key] = mat;
        }
        AssetDatabase.SaveAssets();
    }

    private static void ImportModels()
    {
        foreach (string name in Names)
        {
            string path = Folder + "/FBX/" + name + ".fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new FileNotFoundException("Missing model", path);
            importer.globalScale = 1f;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var pair in Materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static Bounds LocalBounds(Transform root)
    {
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        Bounds bounds = new();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static string F(Vector3 p)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:F5},{1:F5},{2:F5})", p.x, p.y, p.z);
    }

    private static void CreatePrefabsAndValidate()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        Report.Add("H-Props isolated Unity 6000.3.23f1 validation: PASS");
        Report.Add("Formal gameplay scene, gameplay code and project settings: untouched.");
        Report.Add("Pivot convention: bottom-bounds centre; Unity +Y up, +Z forward.");
        SourceReport source = JsonUtility.FromJson<SourceReport>(File.ReadAllText(Folder + "/Documentation/H-Props_Mesh_Report.json"));
        float maxSourceDimensionError = 0f;
        foreach (string name in Names)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FBX/" + name + ".fbx");
            GameObject wrapper = new(name);
            SceneManager.MoveGameObjectToScene(wrapper, scene);
            GameObject imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            imported.name = "FBX_Axis_And_Unit_Conversion";
            imported.transform.localPosition = Vector3.zero;
            imported.transform.localRotation = model.transform.localRotation;
            imported.transform.localScale = model.transform.localScale;
            imported.transform.SetParent(wrapper.transform, false);
            foreach (MeshRenderer renderer in wrapper.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                int slots = filter.sharedMesh.subMeshCount;
                Material[] mapped = new Material[slots];
                for (int i = 0; i < slots; i++)
                {
                    Material importedMaterial = i < renderer.sharedMaterials.Length ? renderer.sharedMaterials[i] : null;
                    string key = importedMaterial ? importedMaterial.name.Replace(" (Instance)", "") : "";
                    if (!Materials.TryGetValue(key, out Material mat))
                    {
                        string objectName = filter.sharedMesh.name;
                        mat = Materials.Values.FirstOrDefault(m => objectName.Contains(m.name));
                    }
                    if (!mat) throw new InvalidOperationException("Unmapped MV material on " + name + " slot " + i + " source=" + key);
                    mapped[i] = mat;
                }
                renderer.sharedMaterials = mapped;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            if (wrapper.GetComponentsInChildren<Collider>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<Animator>(true).Length != 0)
                throw new InvalidOperationException("Physics, script or animation in " + name);
            Bounds b = LocalBounds(wrapper.transform);
            if (Mathf.Abs(b.min.y) > 0.0001f || Mathf.Abs(b.center.x) > 0.0001f || Mathf.Abs(b.center.z) > 0.0001f)
                throw new InvalidOperationException("Pivot failure " + name + " min=" + F(b.min) + " max=" + F(b.max));
            int triangles = wrapper.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            SourceModel expected = source.models.Single(m => m.name == name);
            Vector3 expectedSize = new(expected.dimensions_unity_xyz_m[0], expected.dimensions_unity_xyz_m[1], expected.dimensions_unity_xyz_m[2]);
            float sourceDimensionError = Mathf.Max(Mathf.Abs(b.size.x - expectedSize.x), Mathf.Abs(b.size.y - expectedSize.y), Mathf.Abs(b.size.z - expectedSize.z));
            maxSourceDimensionError = Mathf.Max(maxSourceDimensionError, sourceDimensionError);
            if (sourceDimensionError > 0.0001f || triangles != expected.triangles)
                throw new InvalidOperationException("Unity/Blender source mismatch on " + name + ": sizeError=" + sourceDimensionError + "; triangles=" + triangles + " vs " + expected.triangles);
            int slotsUsed = wrapper.GetComponentsInChildren<MeshRenderer>(true).Max(r => r.sharedMaterials.Length);
            if (triangles > 1500 || slotsUsed > 4) throw new InvalidOperationException("Budget failure " + name);
            foreach (MeshRenderer renderer in wrapper.GetComponentsInChildren<MeshRenderer>(true))
                foreach (Material mat in renderer.sharedMaterials)
                    if (!mat || !mat.name.StartsWith("MV_")) throw new InvalidOperationException("Invalid material name on " + name);
            string prefabPath = Folder + "/Prefabs/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            Report.Add(name + ": tris=" + triangles + "; slots=" + slotsUsed + "; size=" + F(b.size) + "m; pivotErrorY=" + b.min.y.ToString("F7", CultureInfo.InvariantCulture) + "; colliders=0; scripts=0; animations=0; materials=" + string.Join(",", wrapper.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct()));
            UnityEngine.Object.DestroyImmediate(wrapper);
        }
        Report.Add("Maximum Unity vs Blender dimension error=" + maxSourceDimensionError.ToString("F8", CultureInfo.InvariantCulture) + " m; all triangle counts match.");
    }

    private static Material PreviewMaterial(string name)
    {
        return Materials[name];
    }

    private static GameObject Cube(string name, Vector3 center, Vector3 size, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Cylinder(string name, Vector3 center, float radius, float height, Material material, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static void Rod(string name, Vector3 a, Vector3 b, float radius, Material material, Transform parent)
    {
        GameObject go = Cylinder(name, (a + b) * 0.5f, radius, Vector3.Distance(a, b), material, parent);
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
    }

    private static GameObject Prop(string name, Vector3 position, float yaw, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/" + name + ".prefab");
        if (!asset) throw new FileNotFoundException("Missing prop prefab", name);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void BuildPreviewScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        GameObject root = new("H-Props | isolated visual preview only");
        SceneManager.MoveGameObjectToScene(root, scene);
        GameObject propsObject = new("13 independent prop prefab instances");
        SceneManager.MoveGameObjectToScene(propsObject, scene);
        propsObject.transform.SetParent(root.transform, false);
        Transform props = propsObject.transform;
        GameObject displayObject = new("90_PreviewSupports_NotDeliveredAsProps");
        SceneManager.MoveGameObjectToScene(displayObject, scene);
        displayObject.transform.SetParent(root.transform, false);
        Transform stage = displayObject.transform;

        Cube("Ivory display plinth", new Vector3(0, -0.15f, 0), new Vector3(5.7f, 0.30f, 3.65f), PreviewMaterial("MV_Ivory"), stage);
        Cube("Rose foundation", new Vector3(0, -0.355f, 0), new Vector3(5.90f, 0.11f, 3.85f), PreviewMaterial("MV_Rose"), stage);
        Cube("Apricot rear edge", new Vector3(0, 0.035f, 1.71f), new Vector3(5.6f, 0.07f, 0.12f), PreviewMaterial("MV_Apricot"), stage);

        Prop("H_Bougainvillea_Square", new Vector3(-1.98f, 0, 0.14f), 0, props);
        Prop("H_Bougainvillea_Round", new Vector3(-0.95f, 0, 0.22f), 12, props);
        Prop("H_Bunting_2m", new Vector3(-1.49f, 0.83f, 1.35f), 0, props);
        foreach (float x in new[] { -2.49f, -0.49f })
            Cylinder("Bunting display pole", new Vector3(x, 0.68f, 1.35f), 0.025f, 1.36f, PreviewMaterial("MV_Ivory"), stage);

        Prop("H_Blue_Shutters_Pair", new Vector3(0.40f, 0.12f, 1.29f), 0, props);
        Cube("Temporary shutter mounting block", new Vector3(0.40f, 0.56f, 1.40f), new Vector3(0.79f, 1.12f, 0.14f), PreviewMaterial("MV_IvoryShade"), stage);
        Cube("Mounting block top trim", new Vector3(0.40f, 1.13f, 1.40f), new Vector3(0.90f, 0.06f, 0.19f), PreviewMaterial("MV_Apricot"), stage);
        Prop("H_Hanging_Lantern", new Vector3(1.42f, 0.11f, 1.22f), 0, props);
        Cylinder("Lantern display upright", new Vector3(1.42f, 0.57f, 1.49f), 0.021f, 1.14f, PreviewMaterial("MV_Ivory"), stage);
        Rod("Lantern display hook", new Vector3(1.42f, 1.09f, 1.49f), new Vector3(1.42f, 1.09f, 1.22f), 0.018f, PreviewMaterial("MV_Apricot"), stage);
        Rod("Lantern hanging link", new Vector3(1.42f, 0.95f, 1.22f), new Vector3(1.42f, 1.09f, 1.22f), 0.009f, PreviewMaterial("MV_Apricot"), stage);
        Prop("H_Copper_Bell", new Vector3(2.20f, 0.11f, 1.28f), 0, props);
        Cylinder("Bell display upright", new Vector3(2.20f, 0.33f, 1.49f), 0.018f, 0.66f, PreviewMaterial("MV_Ivory"), stage);
        Rod("Bell display hook", new Vector3(2.20f, 0.64f, 1.49f), new Vector3(2.20f, 0.64f, 1.28f), 0.013f, PreviewMaterial("MV_Apricot"), stage);
        Rod("Bell hanging link", new Vector3(2.20f, 0.50f, 1.28f), new Vector3(2.20f, 0.64f, 1.28f), 0.006f, PreviewMaterial("MV_Apricot"), stage);

        Prop("H_Domed_Chimney", new Vector3(0.23f, 0, 0.06f), 0, props);
        Prop("H_Roof_Cupola", new Vector3(1.21f, 0, 0.05f), 0, props);
        Prop("H_Terracotta_Amphora", new Vector3(-1.90f, 0, -1.08f), -12, props);
        Prop("H_Terracotta_Bowl", new Vector3(-0.95f, 0, -1.09f), 0, props);
        Prop("H_Seagull_Glide", new Vector3(0.04f, 0.19f, -1.02f), -12, props);
        Prop("H_Seagull_Upstroke", new Vector3(1.20f, 0.28f, -0.75f), 15, props);
        Prop("H_Seagull_Bank", new Vector3(2.13f, 0.44f, -0.72f), 30, props);

        GameObject cameraObject = new("H-Props Official Angle Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 2.75f;
        Vector3 target = new(0.10f, 0.24f, 0.02f);
        camera.transform.position = target + new Vector3(12, 15, -15).normalized * 18f;
        camera.transform.LookAt(target);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.56f, 0.69f, 0.76f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        camera.allowMSAA = true;
        cameraObject.tag = "MainCamera";

        GameObject keyObject = new("Soft warm key");
        SceneManager.MoveGameObjectToScene(keyObject, scene);
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.93f, 0.83f);
        key.intensity = 1.20f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.55f;
        keyObject.transform.rotation = Quaternion.Euler(46, -32, 0);
        GameObject fillObject = new("Cool fill");
        SceneManager.MoveGameObjectToScene(fillObject, scene);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.72f, 0.83f, 1f);
        fill.intensity = 0.38f;
        fill.shadows = LightShadows.None;
        fillObject.transform.rotation = Quaternion.Euler(35, 140, 0);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.79f, 0.86f, 0.95f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.65f, 0.73f);
        RenderSettings.ambientGroundColor = new Color(0.41f, 0.34f, 0.42f);
        RenderSettings.ambientIntensity = 0.75f;
        RenderSettings.fog = false;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        // Render twice to let URP create its internal resources in a fresh batch.
        Render(camera, Folder + "/Preview/H-Props_All.png", 1920, 1080);
        Report.Add("Preview: " + ScenePath + "; screenshot=Preview/H-Props_All.png; 1920x1080; orthographic; official direction (12,15,-15).");
        Report.Add("Independent prop prefab root instances in preview=" + props.childCount);
        if (props.childCount != 13) throw new InvalidOperationException("Expected 13 independent prefab instances.");
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void Render(Camera camera, string path, int width, int height)
    {
        RenderTexture texture = new(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        camera.targetTexture = texture;
        camera.Render();
        camera.Render();
        Texture2D image = new(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(texture);
        if (!File.Exists(path) || new FileInfo(path).Length < 10000)
            throw new InvalidOperationException("Missing or blank preview image " + path);
        Debug.Log("HPROPS_RENDER " + path);
    }
}
