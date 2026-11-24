// J-CourtyardDecor isolated Unity delivery builder. This Editor utility is not a prop
// component and is not copied into the main project's Assets folder.
// In the isolated QA project:
// Unity.exe -batchmode -projectPath ".utmp/J-CourtyardDecor-QA" -executeMethod JCourtyardDecorEditor.BuildAndCapture -quit
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

public static class JCourtyardDecorEditor
{
    private const string Folder = "Assets/Scenes/Archive/BounceBack/Art/J-CourtyardDecor";
    private const string ScenePath = Folder + "/Preview/J-CourtyardDecor_Preview.unity";
    private static readonly Vector3 CameraDirection = new(12, 15, -15);
    private static readonly Dictionary<string, Vector3> Palette = new()
    {
        ["MV_Ivory"] = new Vector3(0.95f, 0.88f, 0.77f),
        ["MV_IvoryShade"] = new Vector3(0.86f, 0.76f, 0.70f),
        ["MV_Rose"] = new Vector3(0.74f, 0.45f, 0.47f),
        ["MV_RoseDeep"] = new Vector3(0.55f, 0.30f, 0.38f),
        ["MV_Lilac"] = new Vector3(0.50f, 0.45f, 0.66f),
        ["MV_Apricot"] = new Vector3(0.90f, 0.58f, 0.37f),
        ["MV_DomeBlue"] = new Vector3(0.30f, 0.47f, 0.80f),
        ["MV_Water"] = new Vector3(0.45f, 0.80f, 0.84f)
    };
    private static readonly string[] Names =
    {
        "J_Blue_Wooden_Bench", "J_Courtyard_Chair", "J_Cafe_Table_Round",
        "J_Ceramic_Stool", "J_House_Number_07", "J_Courtyard_Wall_Fountain",
        "J_Blue_Railing_1m", "J_Rooftop_Pergola_2m", "J_Canvas_Parasol",
        "J_Striped_Awning_2m", "J_Laundry_Line_2m", "J_Clay_Pitcher",
        "J_Fruit_Basket", "J_Blue_Wooden_Crate", "J_Rope_Coil",
        "J_Lifebuoy", "J_Book_Stack", "J_Candle_Set"
    };
    private static readonly List<string> Report = new();
    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly List<GameObject> PreviewProps = new();

    [Serializable]
    private sealed class SourceModel
    {
        public string name;
        public float[] dimensions_unity_xyz_m;
        public int triangles;
        public string[] materials;
    }

    [Serializable]
    private sealed class SourceReport
    {
        public SourceModel[] models;
    }

    [MenuItem("J-CourtyardDecor/Build Isolated Preview")]
    public static void BuildAndCapture()
    {
        string project = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace('\\', '/');
        if (!project.EndsWith("/.utmp/J-CourtyardDecor-QA", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("J-CourtyardDecor preview builder only runs in a project path ending .utmp/J-CourtyardDecor-QA.");

        Report.Clear();
        Materials.Clear();
        PreviewProps.Clear();
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
        File.WriteAllText(Folder + "/Documentation/J-CourtyardDecor_Unity_Validation.txt", string.Join("\n", Report) + "\n", Encoding.UTF8);
        Debug.Log("JDECOR_UNITY_PASS " + string.Join(" | ", Report));
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
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0) throw new InvalidOperationException("No MeshFilter under " + root.name);
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (MeshFilter filter in filters)
        {
            if (!filter.sharedMesh || filter.sharedMesh.vertexCount == 0)
                throw new InvalidOperationException("Missing or empty mesh under " + root.name);
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

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("No Renderer under " + go.name);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static string F(Vector3 p)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:F5},{1:F5},{2:F5})", p.x, p.y, p.z);
    }

    private static float ScaleError(Vector3 actual, Vector3 expected)
    {
        return Mathf.Max(Mathf.Abs(actual.x - expected.x), Mathf.Abs(actual.y - expected.y), Mathf.Abs(actual.z - expected.z));
    }

    private static void ValidatePrefabStructure(GameObject prefab, Quaternion fbxRotation, Vector3 fbxScale)
    {
        Transform root = prefab.transform;
        if (root.localPosition.sqrMagnitude > 0.000000000001f ||
            Quaternion.Angle(root.localRotation, Quaternion.identity) > 0.0001f ||
            ScaleError(root.localScale, Vector3.one) > 0.000001f)
            throw new InvalidOperationException("Outer prefab root is not identity: " + prefab.name);
        if (root.childCount != 1 || root.GetChild(0).name != "FBX_Axis_And_Unit_Conversion")
            throw new InvalidOperationException("FBX axis/unit conversion node was not preserved: " + prefab.name);
        Transform conversion = root.GetChild(0);
        if (conversion.localPosition.sqrMagnitude > 0.000000000001f ||
            Quaternion.Angle(conversion.localRotation, fbxRotation) > 0.0001f ||
            ScaleError(conversion.localScale, fbxScale) > 0.000001f)
            throw new InvalidOperationException("FBX axis/unit transform changed: " + prefab.name);
    }

    private static void CreatePrefabsAndValidate()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        Report.Add("J-CourtyardDecor isolated Unity " + Application.unityVersion + " validation: PASS");
        Report.Add("Formal gameplay scene, gameplay code and project settings: untouched; display-only preview.");
        Report.Add("Pivot convention: bottom-bounds centre; Unity +Y up, +Z forward; all errors are metres.");
        string sourcePath = Folder + "/Documentation/J-CourtyardDecor_Mesh_Report.json";
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Missing Blender mesh report", sourcePath);
        SourceReport source = JsonUtility.FromJson<SourceReport>(File.ReadAllText(sourcePath));
        if (source == null || source.models == null)
            throw new InvalidOperationException("Invalid Blender mesh report: " + sourcePath);
        HashSet<string> requiredNames = new(Names);
        if (source.models.Length != Names.Length ||
            source.models.Select(m => m.name).Distinct().Count() != Names.Length ||
            source.models.Any(m => !requiredNames.Contains(m.name)))
            throw new InvalidOperationException("Mesh report must contain exactly the 18 required J-CourtyardDecor models.");

        float maxSourceDimensionError = 0f;
        float maxPivotError = 0f;
        foreach (string name in Names)
        {
            SourceModel expected = source.models.Single(m => m.name == name);
            if (expected.dimensions_unity_xyz_m == null || expected.dimensions_unity_xyz_m.Length != 3)
                throw new InvalidOperationException("Missing Unity/Blender dimensions for " + name);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FBX/" + name + ".fbx");
            if (!model) throw new FileNotFoundException("Missing imported model", name);
            Quaternion fbxRotation = model.transform.localRotation;
            Vector3 fbxScale = model.transform.localScale;
            GameObject wrapper = new(name);
            SceneManager.MoveGameObjectToScene(wrapper, scene);
            GameObject imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            if (!imported) throw new InvalidOperationException("Could not instantiate FBX prefab: " + name);
            imported.name = "FBX_Axis_And_Unit_Conversion";
            imported.transform.SetParent(wrapper.transform, false);
            imported.transform.localPosition = Vector3.zero;
            imported.transform.localRotation = fbxRotation;
            imported.transform.localScale = fbxScale;

            foreach (MeshRenderer renderer in wrapper.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) throw new InvalidOperationException("MeshRenderer without MeshFilter/mesh on " + name);
                int slots = filter.sharedMesh.subMeshCount;
                Material[] mapped = new Material[slots];
                for (int i = 0; i < slots; i++)
                {
                    Material importedMaterial = i < renderer.sharedMaterials.Length ? renderer.sharedMaterials[i] : null;
                    string key = importedMaterial ? importedMaterial.name.Replace(" (Instance)", "") : "";
                    if (!Materials.TryGetValue(key, out Material mat))
                    {
                        string objectName = filter.sharedMesh.name;
                        mat = Materials.Values.OrderByDescending(m => m.name.Length)
                            .FirstOrDefault(m => objectName.IndexOf(m.name, StringComparison.Ordinal) >= 0);
                    }
                    if (!mat) throw new InvalidOperationException("Unmapped MV material on " + name + " slot " + i + " source=" + key);
                    mapped[i] = mat;
                }
                renderer.sharedMaterials = mapped;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            foreach (MeshFilter filter in wrapper.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.sharedMesh.uv == null || filter.sharedMesh.uv.Length == 0)
                    throw new InvalidOperationException("UV0 is empty on " + name + "/" + filter.name);
            }
            if (wrapper.GetComponentsInChildren<Collider>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<Animator>(true).Length != 0 ||
                wrapper.GetComponentsInChildren<Animation>(true).Length != 0)
                throw new InvalidOperationException("Physics, script or animation component in " + name);

            Bounds b = LocalBounds(wrapper.transform);
            float pivotBottomError = Mathf.Abs(b.min.y);
            float pivotCentreError = Mathf.Max(Mathf.Abs(b.center.x), Mathf.Abs(b.center.z));
            maxPivotError = Mathf.Max(maxPivotError, Mathf.Max(pivotBottomError, pivotCentreError));
            if (pivotBottomError > 0.0001f || pivotCentreError > 0.0001f)
                throw new InvalidOperationException("Pivot failure " + name + "; bottomError=" + pivotBottomError + "; centreXZError=" + pivotCentreError + "; min=" + F(b.min) + "; max=" + F(b.max));
            int triangles = wrapper.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            Vector3 expectedSize = new(expected.dimensions_unity_xyz_m[0], expected.dimensions_unity_xyz_m[1], expected.dimensions_unity_xyz_m[2]);
            float sourceDimensionError = ScaleError(b.size, expectedSize);
            maxSourceDimensionError = Mathf.Max(maxSourceDimensionError, sourceDimensionError);
            if (sourceDimensionError > 0.0001f || triangles != expected.triangles)
                throw new InvalidOperationException("Unity/Blender source mismatch on " + name + ": sizeError=" + sourceDimensionError + "; triangles=" + triangles + " vs " + expected.triangles);
            MeshRenderer[] renderers = wrapper.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No MeshRenderer on " + name);
            int slotsUsed = renderers.Max(r => r.sharedMaterials.Length);
            if (triangles > 1500 || slotsUsed > 4)
                throw new InvalidOperationException("Budget failure " + name + ": triangles=" + triangles + "; slots=" + slotsUsed);
            foreach (MeshRenderer renderer in renderers)
                foreach (Material mat in renderer.sharedMaterials)
                    if (!mat || !Materials.ContainsKey(mat.name))
                        throw new InvalidOperationException("Invalid or unremapped material on " + name);
            if (expected.materials != null && expected.materials.Length > 0)
            {
                string[] expectedMaterials = expected.materials.Distinct().OrderBy(m => m, StringComparer.Ordinal).ToArray();
                string[] actualMaterials = renderers.SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().OrderBy(m => m, StringComparer.Ordinal).ToArray();
                if (!expectedMaterials.SequenceEqual(actualMaterials, StringComparer.Ordinal))
                    throw new InvalidOperationException("Material set mismatch on " + name + ": expected=" + string.Join(",", expectedMaterials) + "; actual=" + string.Join(",", actualMaterials));
            }

            ValidatePrefabStructure(wrapper, fbxRotation, fbxScale);
            string prefabPath = Folder + "/Prefabs/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!savedPrefab) throw new InvalidOperationException("Prefab save failed: " + prefabPath);
            ValidatePrefabStructure(savedPrefab, fbxRotation, fbxScale);
            Report.Add(name + ": tris=" + triangles + "; slots=" + slotsUsed + "; size=" + F(b.size) + "m; sourceSizeError=" + sourceDimensionError.ToString("F8", CultureInfo.InvariantCulture) +
                "; pivotBottomError=" + pivotBottomError.ToString("F8", CultureInfo.InvariantCulture) + "; pivotCentreXZError=" + pivotCentreError.ToString("F8", CultureInfo.InvariantCulture) +
                "; UV0=nonempty; colliders=0; rigidbodies=0; scripts=0; animators=0; animations=0; outerRoot=identity; FBXAxisUnitNode=preserved; materials=" +
                string.Join(",", renderers.SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct()));
            UnityEngine.Object.DestroyImmediate(wrapper);
        }
        Report.Add("Maximum Unity vs Blender dimension error=" + maxSourceDimensionError.ToString("F8", CultureInfo.InvariantCulture) +
            " m; maximum bottom-centre pivot error=" + maxPivotError.ToString("F8", CultureInfo.InvariantCulture) + " m; all triangle counts match; all UV0 sets nonempty.");
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
        PreviewProps.Add(instance);
        return instance;
    }

    private static GameObject WallProp(string name, float x, float bottomY, float wallFrontZ, Transform parent)
    {
        GameObject instance = Prop(name, new Vector3(x, bottomY, 0), 0, parent);
        Bounds local = LocalBounds(instance.transform);
        instance.transform.localPosition = new Vector3(x, bottomY, wallFrontZ - local.max.z - 0.005f);
        return instance;
    }

    private static bool PositiveIntersection(Bounds a, Bounds b)
    {
        const float epsilon = 0.0001f;
        return a.min.x < b.max.x - epsilon && a.max.x > b.min.x + epsilon &&
               a.min.y < b.max.y - epsilon && a.max.y > b.min.y + epsilon &&
               a.min.z < b.max.z - epsilon && a.max.z > b.min.z + epsilon;
    }

    private static void ValidatePreviewLayout()
    {
        if (PreviewProps.Count != Names.Length || PreviewProps.Select(p => p.name).Distinct().Count() != Names.Length)
            throw new InvalidOperationException("Expected exactly 18 unique independent prefab instances.");
        for (int i = 0; i < PreviewProps.Count; i++)
        {
            GameObject prop = PreviewProps[i];
            if (ScaleError(prop.transform.localScale, Vector3.one) > 0.000001f)
                throw new InvalidOperationException("Preview scale changed on " + prop.name);
            Bounds a = WorldBounds(prop);
            if (a.min.x < -4f || a.max.x > 4f || a.min.z < -3f || a.max.z > 3f)
                throw new InvalidOperationException("Prop outside the 8x6m display plinth: " + prop.name + " bounds=" + F(a.min) + ".." + F(a.max));
            for (int j = i + 1; j < PreviewProps.Count; j++)
            {
                Bounds b = WorldBounds(PreviewProps[j]);
                if (PositiveIntersection(a, b))
                    throw new InvalidOperationException("Overlapping preview props: " + prop.name + " and " + PreviewProps[j].name);
            }
            Report.Add("Layout " + prop.name + ": position=" + F(prop.transform.position) + "; worldSize=" + F(a.size) + "m; scale=(1,1,1).");
        }
        Report.Add("Preview layout: tall/long objects at rear, furniture in middle, small objects at front; 18 real-scale prefabs; no pairwise bounds overlap; all props inside approximately 8x6m plinth.");
    }

    private static IEnumerable<Vector3> Corners(Bounds b)
    {
        yield return new Vector3(b.min.x, b.min.y, b.min.z);
        yield return new Vector3(b.min.x, b.min.y, b.max.z);
        yield return new Vector3(b.min.x, b.max.y, b.min.z);
        yield return new Vector3(b.min.x, b.max.y, b.max.z);
        yield return new Vector3(b.max.x, b.min.y, b.min.z);
        yield return new Vector3(b.max.x, b.min.y, b.max.z);
        yield return new Vector3(b.max.x, b.max.y, b.min.z);
        yield return new Vector3(b.max.x, b.max.y, b.max.z);
    }

    private static void FitCamera(Camera camera, Transform content, float aspect)
    {
        Renderer[] renderers = content.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Cannot fit camera to empty preview.");
        Bounds all = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) all.Encapsulate(renderer.bounds);
        camera.transform.position = all.center + CameraDirection.normalized * 30f;
        camera.transform.LookAt(all.center);

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (Renderer renderer in renderers)
        {
            foreach (Vector3 corner in Corners(renderer.bounds))
            {
                Vector3 view = camera.transform.InverseTransformPoint(corner);
                minX = Mathf.Min(minX, view.x);
                maxX = Mathf.Max(maxX, view.x);
                minY = Mathf.Min(minY, view.y);
                maxY = Mathf.Max(maxY, view.y);
            }
        }
        camera.transform.position += camera.transform.right * ((minX + maxX) * 0.5f) + camera.transform.up * ((minY + maxY) * 0.5f);
        float halfHeight = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect);
        camera.orthographicSize = Mathf.Max(0.1f, halfHeight * 1.10f);

        foreach (Renderer renderer in renderers)
            foreach (Vector3 corner in Corners(renderer.bounds))
            {
                Vector3 view = camera.transform.InverseTransformPoint(corner);
                if (Mathf.Abs(view.x) > camera.orthographicSize * aspect + 0.0001f ||
                    Mathf.Abs(view.y) > camera.orthographicSize + 0.0001f ||
                    view.z < camera.nearClipPlane || view.z > camera.farClipPlane)
                    throw new InvalidOperationException("Automatic camera fitting failed for " + renderer.name);
            }
    }

    private static void BuildPreviewScene()
    {
        PreviewProps.Clear();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        GameObject root = new("J-CourtyardDecor | isolated display-only visual preview");
        SceneManager.MoveGameObjectToScene(root, scene);
        GameObject propsObject = new("18 independent real-scale prop prefab instances");
        SceneManager.MoveGameObjectToScene(propsObject, scene);
        propsObject.transform.SetParent(root.transform, false);
        Transform props = propsObject.transform;
        GameObject displayObject = new("90_PreviewBaseAndSupports_NotPartOfAnyPropPrefab");
        SceneManager.MoveGameObjectToScene(displayObject, scene);
        displayObject.transform.SetParent(root.transform, false);
        Transform stage = displayObject.transform;

        Cube("8x6m ivory display plinth", new Vector3(0, -0.15f, 0), new Vector3(8f, 0.30f, 6f), PreviewMaterial("MV_Ivory"), stage);
        Cube("Rose foundation", new Vector3(0, -0.355f, 0), new Vector3(8.20f, 0.11f, 6.20f), PreviewMaterial("MV_Rose"), stage);
        Cube("Apricot rear edge", new Vector3(0, 0.035f, 2.94f), new Vector3(7.9f, 0.07f, 0.10f), PreviewMaterial("MV_Apricot"), stage);

        // Rear: the largest/tallest objects and a temporary wall-mounted gallery.
        Prop("J_Rooftop_Pergola_2m", new Vector3(-2.75f, 0, 1.50f), 0, props);
        Prop("J_Canvas_Parasol", new Vector3(-0.35f, 0, 1.50f), 0, props);
        Prop("J_Blue_Railing_1m", new Vector3(1.05f, 0, 1.65f), 90, props);
        const float wallFront = 2.715f;
        Cube("Temporary courtyard display wall", new Vector3(2.50f, 1.25f, 2.80f), new Vector3(2.80f, 2.50f, 0.16f), PreviewMaterial("MV_IvoryShade"), stage);
        Cube("Temporary wall cap", new Vector3(2.50f, 2.53f, 2.80f), new Vector3(2.90f, 0.08f, 0.22f), PreviewMaterial("MV_Apricot"), stage);
        WallProp("J_Striped_Awning_2m", 2.35f, 1.45f, wallFront, props);
        WallProp("J_House_Number_07", 3.62f, 1.55f, wallFront, props);
        WallProp("J_Courtyard_Wall_Fountain", 2.65f, 0, wallFront, props);
        GameObject laundry = Prop("J_Laundry_Line_2m", new Vector3(1.70f, 1.05f, 0.30f), 0, props);
        Bounds laundryBounds = WorldBounds(laundry);
        float poleHeight = laundryBounds.max.y + 0.04f;
        foreach (float x in new[] { laundryBounds.min.x - 0.04f, laundryBounds.max.x + 0.04f })
            Cylinder("Laundry display pole", new Vector3(x, poleHeight * 0.5f, laundryBounds.center.z), 0.022f, poleHeight, PreviewMaterial("MV_Ivory"), stage);

        // Middle: full-size seating, table, stool and crate, with clear floor gaps.
        Prop("J_Blue_Wooden_Bench", new Vector3(-2.75f, 0, -0.85f), 0, props);
        Prop("J_Courtyard_Chair", new Vector3(-1.45f, 0, -0.85f), -10, props);
        Prop("J_Cafe_Table_Round", new Vector3(-0.35f, 0, -0.80f), 0, props);
        Prop("J_Ceramic_Stool", new Vector3(0.55f, 0, -0.90f), 0, props);
        Prop("J_Blue_Wooden_Crate", new Vector3(1.35f, 0, -0.85f), 8, props);

        // Front: small tabletop/loose accessories, all directly at their real scale.
        Prop("J_Clay_Pitcher", new Vector3(-2.95f, 0, -2.20f), -8, props);
        Prop("J_Fruit_Basket", new Vector3(-2.15f, 0, -2.20f), 0, props);
        Prop("J_Rope_Coil", new Vector3(-1.35f, 0, -2.20f), 0, props);
        Prop("J_Book_Stack", new Vector3(-0.45f, 0, -2.20f), -8, props);
        Prop("J_Candle_Set", new Vector3(0.40f, 0, -2.20f), 0, props);
        Prop("J_Lifebuoy", new Vector3(1.55f, 0, -2.15f), -8, props);
        ValidatePreviewLayout();
        if (props.childCount != Names.Length) throw new InvalidOperationException("Expected 18 independent prefab instances.");

        GameObject cameraObject = new("J-CourtyardDecor Official Angle Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.56f, 0.69f, 0.76f, 1f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100f;
        camera.allowMSAA = true;
        cameraObject.tag = "MainCamera";
        FitCamera(camera, root.transform, 1920f / 1080f);

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

        if (!GraphicsSettings.currentRenderPipeline ||
            GraphicsSettings.currentRenderPipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException("The isolated QA project must use a Universal Render Pipeline asset for the actual camera render.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        // Render twice to let URP create its internal resources in a fresh batch.
        Render(camera, Folder + "/Preview/J-CourtyardDecor_All.png", 1920, 1080);
        Report.Add("Preview: " + ScenePath + "; screenshot=Preview/J-CourtyardDecor_All.png; 1920x1080; URP actual camera; orthographicSize=" +
            camera.orthographicSize.ToString("F5", CultureInfo.InvariantCulture) + "; auto-fitted with 10% margin; official direction (12,15,-15).");
        Report.Add("Independent prop prefab root instances in preview=" + props.childCount + "; base/supports are scene-only and excluded from every prop prefab.");
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void Render(Camera camera, string path, int width, int height)
    {
        if (width != 1920 || height != 1080) throw new InvalidOperationException("Preview PNG must be 1920x1080.");
        RenderTexture texture = new(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = texture;
            camera.targetTexture = texture;
            camera.Render();
            camera.Render();
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            if (image) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }
        if (!File.Exists(path) || new FileInfo(path).Length < 10000)
            throw new InvalidOperationException("Missing or blank preview image " + path);
        Debug.Log("JDECOR_RENDER " + path + " 1920x1080");
    }
}
