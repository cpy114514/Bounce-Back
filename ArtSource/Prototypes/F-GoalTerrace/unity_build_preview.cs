using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copy this file into the isolated project's Assets/Editor folder, then call:
// Unity.exe -batchmode -projectPath .utmp/F-GoalTerrace-QA -executeMethod FGoalTerraceDelivery.Build -quit
// Do not use -nographics: the two required images are rendered through the active URP pipeline.
public static class FGoalTerraceDelivery
{
    public const string Folder = "Assets/Scenes/Archive/BounceBack/Art/F-GoalTerrace";
    public static readonly Vector3 Anchor = new Vector3(4.90f, 0.83f, 4.10f);

    private const string ArtRoot = "Assets/Scenes/Archive/BounceBack/Art";
    private const string LayoutPath = "ArtSource/Santorini Layout/Santorini Collision Layout.json";
    private const string PreviewScenePath = Folder + "/Preview/FGoalTerrace_Preview.unity";
    private const string ReportPath = Folder + "/Documentation/FGoalTerrace_Unity_Validation.txt";
    private const string CombinedPrefabPath = Folder + "/Prefabs/FGoalTerrace.prefab";
    private const string BTowerPrefabPath = ArtRoot + "/B-ReleaseTower/Prefabs/BReleaseTower.prefab";
    private const string ReferenceMeshFolder = Folder + "/Preview/ReferenceMeshes";
    private const float MaximumCornerError = 0.0002f;
    private const int ImageWidth = 1920;
    private const int ImageHeight = 1080;

    private static readonly string[] Modules =
    {
        "FGoalTerrace_Terrace",
        "FGoalTerrace_Cascade",
        "FGoalTerrace_Pavilion",
        "FGoalTerrace_Railing",
        "FGoalTerrace_Waterfall"
    };

    private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "MV_Ivory", new Color(0.95f, 0.88f, 0.77f) },
        { "MV_IvoryShade", new Color(0.86f, 0.76f, 0.70f) },
        { "MV_Rose", new Color(0.74f, 0.45f, 0.47f) },
        { "MV_Lilac", new Color(0.50f, 0.45f, 0.66f) },
        { "MV_Apricot", new Color(0.90f, 0.58f, 0.37f) },
        { "MV_DomeBlue", new Color(0.30f, 0.47f, 0.80f) },
        { "MV_Water", new Color(0.45f, 0.80f, 0.84f) },
        { "MV_Gold", new Color(0.96f, 0.70f, 0.32f) }
    };

    private static readonly CoreSpec[] CoreSpecs =
    {
        new CoreSpec("FGoalTerrace_Terrace", "Terrace_Collision_Aligned_Core", "Landing Terrace"),
        new CoreSpec("FGoalTerrace_Cascade", "Cascade_Lip_Collision_Aligned", "Cascade Lip Ramp"),
        new CoreSpec("FGoalTerrace_Cascade", "Cascade_Floor_Collision_Aligned", "Cascade Pressure Plate Floor")
    };

    // Y is the road surface height, not the centre of the clearance volume.
    private static readonly Vector3[] ClearanceRoute =
    {
        new Vector3(2.10f, 0.96f, 4.90f),
        new Vector3(4.60f, 0.78f, 6.60f),
        new Vector3(5.05f, 0.78f, 5.24f),
        new Vector3(5.31f, 0.78f, 5.24f)
    };

    private static readonly List<string> Report = new List<string>();

    [Serializable]
    private sealed class LayoutFile
    {
        public LayoutItem[] items;
    }

    [Serializable]
    private sealed class LayoutItem
    {
        public string name;
        public Vector3 center;
        public Vector3 size;
        public Quaternion rotation;
        public bool anchor;
        public Vector3[] corners;
    }

    private sealed class CoreSpec
    {
        public readonly string Module;
        public readonly string MeshPrefix;
        public readonly string CollisionName;

        public CoreSpec(string module, string meshPrefix, string collisionName)
        {
            Module = module;
            MeshPrefix = meshPrefix;
            CollisionName = collisionName;
        }
    }

    private sealed class ArtValidation
    {
        public int TotalTriangles;
        public int MeshCount;
        public float MaximumError;
    }

    private struct WorldTriangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;
        public Vector3 Min;
        public Vector3 Max;
        public string Source;
        public int TriangleIndex;
    }

    public static void Build()
    {
        Report.Clear();
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("FGoalTerrace requires a graphics device. Run batchmode without -nographics.");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureTaskFolders();
        CreateMaterials();
        ImportModules();
        LayoutFile layout = ReadLayout();

        // This is an independent empty scene. No formal scene is opened or copied.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        CreatePrefabAssets(scene);
        CleanupExtractedMaterials();

        GameObject art = InstantiateArt(scene);
        bool hasBTower = TryInstantiateBTower(scene);
        CreateReferenceProxies(scene, layout, hasBTower);
        CreateGoldBall(scene);
        CreateLighting(scene);

        Camera official = CreateCamera(
            scene,
            "FGoalTerrace Official Camera",
            new Vector3(-0.05f, 0.95f, 0.25f) + new Vector3(12f, 15f, -15f),
            new Vector3(-0.05f, 0.95f, 0.25f),
            true,
            9.5f,
            true);
        official.gameObject.tag = "MainCamera";
        Camera reverse = CreateCamera(
            scene,
            "FGoalTerrace Reverse From Goal Camera",
            new Vector3(5f, 1.5f, 5.2f),
            new Vector3(-5.9f, 3.2f, -3.65f),
            true,
            6.5f,
            false);

        ValidatePrefabAssets();
        ArtValidation validation = InspectArt(art, layout);
        ValidateClearance(art, validation.TotalTriangles);
        AssertPreviewHasNoPhysics(scene);

        Report.Add("Official camera: position=(11.95,15.95,-14.75), target=(-0.05,0.95,0.25), orthographic size=9.5.");
        Report.Add("Reverse camera: position=(5,1.5,5.2), target=(-5.9,3.2,-3.65), orthographic size=6.5 for an uncropped tower/goal read.");
        Report.Add("Preview saved with Official enabled and Reverse disabled; no formal scene was opened or copied.");
        SavePreview(scene);
        AssetDatabase.SaveAssets();

        Render(official, Folder + "/Preview/FGoalTerrace_Official_1920x1080.png");
        Render(reverse, Folder + "/Preview/FGoalTerrace_ReverseFromGoal_1920x1080.png");

        official.enabled = true;
        reverse.enabled = false;
        official.targetTexture = null;
        reverse.targetTexture = null;
        SavePreview(scene);
        AssetDatabase.SaveAssets();

        Report.Add("F prefab physics: Collider=0, Rigidbody=0, MonoBehaviour=0; imported Camera/Light/Animation/Animator=0.");
        Report.Add("Result: Pass");
        Report.Add("RESULT=PASS");
        string contents = string.Join("\n", Report) + "\n";
        File.WriteAllText(OutputPath(ReportPath), contents, new System.Text.UTF8Encoding(false));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("F_GOAL_TERRACE_UNITY_PASS\n" + contents);
    }

    private static void EnsureTaskFolders()
    {
        if (!AssetDatabase.IsValidFolder(ArtRoot))
            throw new DirectoryNotFoundException("Expected existing art root: " + ArtRoot);
        EnsureFolder(Folder);
        EnsureFolder(Folder + "/Materials");
        EnsureFolder(Folder + "/Prefabs");
        EnsureFolder(Folder + "/Preview");
        EnsureFolder(Folder + "/Documentation");
        EnsureFolder(ReferenceMeshFolder);
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;
        string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            throw new DirectoryNotFoundException("Missing parent Unity folder for " + assetPath);
        string name = assetPath.Substring(parent.Length + 1);
        string guid = AssetDatabase.CreateFolder(parent, name);
        if (string.IsNullOrEmpty(guid))
            throw new IOException("Could not create Unity folder " + assetPath);
    }

    private static string AbsolutePath(string projectRelativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string OutputPath(string assetPath)
    {
        string prefix = Folder + "/";
        if (!assetPath.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to write outside the F-GoalTerrace task folder: " + assetPath);
        string root = AbsolutePath(Folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = AbsolutePath(assetPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved output escapes the task folder: " + assetPath);
        return fullPath;
    }

    private static void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader)
            throw new InvalidOperationException("URP/Lit was not found.");

        foreach (KeyValuePair<string, Color> entry in Palette)
        {
            string path = Folder + "/Materials/" + entry.Key + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            // Match the existing B-task convention: these values are supplied directly as sRGB floats.
            material.color = entry.Value;
            material.SetColor("_BaseColor", entry.Value);
            material.SetFloat("_Smoothness", entry.Key == "MV_Water" ? 0.32f : 0.18f);
            material.SetFloat("_Metallic", 0f);
            ClearTextures(material, shader);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();

        foreach (KeyValuePair<string, Color> entry in Palette)
        {
            Material material = Material(entry.Key);
            if (!ColorEquals(material.color, entry.Value) || !ColorEquals(material.GetColor("_BaseColor"), entry.Value))
                throw new InvalidOperationException("Base colour verification failed for " + entry.Key);
            float expectedSmoothness = entry.Key == "MV_Water" ? 0.32f : 0.18f;
            if (Mathf.Abs(material.GetFloat("_Smoothness") - expectedSmoothness) > 0.000001f ||
                Mathf.Abs(material.GetFloat("_Metallic")) > 0.000001f)
                throw new InvalidOperationException("Surface parameter verification failed for " + entry.Key);
            EnsureNoTextures(material, shader);
            Report.Add(string.Format(CultureInfo.InvariantCulture,
                "Material {0}: URP/Lit _BaseColor=({1:F2},{2:F2},{3:F2}), Smoothness={4:F2}, Metallic=0, textures=none.",
                entry.Key, entry.Value.r, entry.Value.g, entry.Value.b, expectedSmoothness));
        }
    }

    private static bool ColorEquals(Color actual, Color expected)
    {
        return Mathf.Abs(actual.r - expected.r) < 0.000001f &&
               Mathf.Abs(actual.g - expected.g) < 0.000001f &&
               Mathf.Abs(actual.b - expected.b) < 0.000001f &&
               Mathf.Abs(actual.a - expected.a) < 0.000001f;
    }

    private static void ClearTextures(Material material, Shader shader)
    {
        int count = ShaderUtil.GetPropertyCount(shader);
        for (int index = 0; index < count; index++)
        {
            if (ShaderUtil.GetPropertyType(shader, index) == ShaderUtil.ShaderPropertyType.TexEnv)
                material.SetTexture(ShaderUtil.GetPropertyName(shader, index), null);
        }
    }

    private static void EnsureNoTextures(Material material, Shader shader)
    {
        int count = ShaderUtil.GetPropertyCount(shader);
        for (int index = 0; index < count; index++)
        {
            if (ShaderUtil.GetPropertyType(shader, index) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;
            string property = ShaderUtil.GetPropertyName(shader, index);
            if (material.GetTexture(property))
                throw new InvalidOperationException(material.name + " unexpectedly has texture " + property);
        }
    }

    private static Material Material(string name)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");
        if (!material)
            throw new FileNotFoundException("Missing task material", name);
        return material;
    }

    private static void ImportModules()
    {
        foreach (string module in Modules)
        {
            string path = Folder + "/FBX/" + module + ".fbx";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer)
                throw new FileNotFoundException("Missing FGoalTerrace FBX importer", path);

            importer.globalScale = 1f;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialSearch = ModelImporterMaterialSearch.Local;

            foreach (string materialName in Palette.Keys)
            {
                Material remap = Material(materialName);
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), materialName);
                importer.AddRemap(identifier, remap);
            }
            importer.SaveAndReimport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model)
                throw new FileNotFoundException("FBX did not produce a GameObject", path);
            AssertVisualHierarchy(model, "FBX " + module);
        }
    }

    private static void CleanupExtractedMaterials()
    {
        // Unity may extract a second local set before the explicit remaps are saved.
        // Delete it only after all prefab MeshRenderers carry canonical material overrides.
        string extractedFolder = Folder + "/FBX/Materials";
        if (AssetDatabase.IsValidFolder(extractedFolder))
            AssetDatabase.DeleteAsset(extractedFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void CreatePrefabAssets(Scene scene)
    {
        foreach (string module in Modules)
        {
            string sourcePath = Folder + "/FBX/" + module + ".fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (!model)
                throw new FileNotFoundException("Missing imported model", sourcePath);

            GameObject wrapper = NewRoot(module, scene);
            GameObject imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            if (!imported)
                throw new InvalidOperationException("Could not instantiate " + sourcePath);
            imported.name = module + "_Imported";
            imported.transform.SetParent(wrapper.transform, false);
            imported.transform.localPosition = Vector3.zero;
            // Keep the importer's axis conversion and file-unit conversion inside the visual wrapper.
            imported.transform.localRotation = model.transform.localRotation;
            imported.transform.localScale = model.transform.localScale;
            RemapMaterials(wrapper);
            AssertVisualHierarchy(wrapper, module);

            string prefabPath = ModulePrefabPath(module);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
            if (!saved)
                throw new InvalidOperationException("Could not save " + prefabPath);
            Object.DestroyImmediate(wrapper);
        }

        GameObject combined = NewRoot("FGoalTerrace", scene);
        foreach (string module in Modules)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModulePrefabPath(module));
            if (!prefab)
                throw new FileNotFoundException("Missing module prefab", ModulePrefabPath(module));
            GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            child.name = module;
            child.transform.SetParent(combined.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }
        AssertVisualHierarchy(combined, "FGoalTerrace combined");
        GameObject combinedAsset = PrefabUtility.SaveAsPrefabAsset(combined, CombinedPrefabPath);
        if (!combinedAsset)
            throw new InvalidOperationException("Could not save " + CombinedPrefabPath);
        Object.DestroyImmediate(combined);
        AssetDatabase.SaveAssets();
    }

    private static string ModulePrefabPath(string module)
    {
        return Folder + "/Prefabs/" + module + ".prefab";
    }

    private static void RemapMaterials(GameObject root)
    {
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] source = renderer.sharedMaterials;
            if (source.Length == 0)
                throw new InvalidOperationException("No material slots on " + renderer.name);
            Material[] remapped = new Material[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                string sourceName = source[index] ? source[index].name.Replace(" (Instance)", "") : "";
                if (!Palette.ContainsKey(sourceName))
                    throw new InvalidOperationException("No explicit MV remap for material '" + sourceName + "' on " + renderer.name);
                remapped[index] = Material(sourceName);
            }
            renderer.sharedMaterials = remapped;
        }
    }

    private static GameObject InstantiateArt(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
        if (!prefab)
            throw new FileNotFoundException("Missing combined prefab", CombinedPrefabPath);
        GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        art.name = "FGoalTerrace";
        art.transform.localPosition = Anchor;
        art.transform.localRotation = Quaternion.identity;
        art.transform.localScale = Vector3.one;
        if (art.transform.childCount != Modules.Length)
            throw new InvalidOperationException("Combined prefab must contain exactly five module roots.");
        foreach (string module in Modules)
        {
            Transform child = art.transform.Find(module);
            if (!child)
                throw new InvalidOperationException("Missing module root " + module);
            AssertZeroTransform(child, "Preview module " + module);
        }
        return art;
    }

    private static bool TryInstantiateBTower(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BTowerPrefabPath);
        if (!prefab)
        {
            Report.Add("BReleaseTower preview reference: not present; JSON white proxies only.");
            return false;
        }
        AssertVisualHierarchy(prefab, "BReleaseTower optional reference");
        GameObject tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        tower.name = "BReleaseTower Reference - Preview Only";
        tower.transform.localPosition = new Vector3(-5.64f, -0.22f, -3.65f);
        tower.transform.localRotation = Quaternion.identity;
        tower.transform.localScale = Vector3.one;
        Report.Add("BReleaseTower preview reference: instantiated at (-5.64,-0.22,-3.65); B assets unchanged; Launch Coping proxy hidden.");
        return true;
    }

    private static LayoutFile ReadLayout()
    {
        string path = AbsolutePath(LayoutPath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Missing authoritative collision layout", path);
        LayoutFile layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
        if (layout?.items == null)
            throw new InvalidOperationException("Could not parse " + LayoutPath);
        LayoutItem[] solids = layout.items.Where(item => !item.anchor).ToArray();
        if (solids.Length != 20)
            throw new InvalidOperationException("Expected exactly 20 non-anchor collision bodies, found " + solids.Length);
        foreach (LayoutItem item in solids)
        {
            if (item.corners == null || item.corners.Length != 8)
                throw new InvalidOperationException("Expected eight corners for collision body " + item.name);
        }
        foreach (CoreSpec spec in CoreSpecs)
        {
            if (layout.items.Count(item => item.name == spec.CollisionName) != 1)
                throw new InvalidOperationException("Missing or duplicate collision body " + spec.CollisionName);
        }
        Report.Add("Authoritative layout: 20 eight-corner collision bodies and " +
                   layout.items.Count(item => item.anchor) + " anchors; Goal Terrace Art anchor=(4.90,0.83,4.10).");
        return layout;
    }

    private static void ValidatePrefabAssets()
    {
        foreach (string module in Modules)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModulePrefabPath(module));
            if (!prefab)
                throw new FileNotFoundException("Missing saved module prefab", ModulePrefabPath(module));
            AssertVisualHierarchy(prefab, module + " prefab");
            AssertZeroTransform(prefab.transform, module + " prefab root");
        }

        GameObject combined = AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath);
        if (!combined)
            throw new FileNotFoundException("Missing saved combined prefab", CombinedPrefabPath);
        AssertVisualHierarchy(combined, "FGoalTerrace prefab");
        AssertZeroTransform(combined.transform, "FGoalTerrace prefab root");
        if (combined.transform.childCount != Modules.Length)
            throw new InvalidOperationException("FGoalTerrace.prefab must have five direct module children.");
        foreach (string module in Modules)
        {
            Transform child = combined.transform.Find(module);
            if (!child)
                throw new InvalidOperationException("Saved combined prefab is missing " + module);
            AssertZeroTransform(child, "Saved module " + module);
        }
    }

    private static void AssertZeroTransform(Transform transform, string label)
    {
        if (transform.localPosition.sqrMagnitude > 0.000000000001f ||
            Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.0001f ||
            (transform.localScale - Vector3.one).sqrMagnitude > 0.000000000001f)
            throw new InvalidOperationException(label + " must have localPosition=zero, localRotation=identity, localScale=one.");
    }

    private static void AssertVisualHierarchy(GameObject root, string label)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (!component)
                throw new InvalidOperationException(label + " contains a missing script/component.");
            if (component is Transform || component is MeshFilter || component is MeshRenderer)
                continue;
            throw new InvalidOperationException(label + " contains forbidden component " + component.GetType().FullName +
                                                " on " + component.gameObject.name);
        }
        if (root.GetComponentsInChildren<Collider>(true).Length != 0 ||
            root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
            root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
            root.GetComponentsInChildren<Camera>(true).Length != 0 ||
            root.GetComponentsInChildren<Light>(true).Length != 0 ||
            root.GetComponentsInChildren<Animation>(true).Length != 0 ||
            root.GetComponentsInChildren<Animator>(true).Length != 0)
            throw new InvalidOperationException(label + " must be visual Mesh content only.");
    }

    private static ArtValidation InspectArt(GameObject art, LayoutFile layout)
    {
        if ((art.transform.position - Anchor).sqrMagnitude > 0.000000000001f)
            throw new InvalidOperationException("F preview root is not at the authoritative Goal Terrace Art anchor.");
        AssertVisualHierarchy(art, "F preview art");

        var result = new ArtValidation { MaximumError = 0f };
        var validatedCores = new HashSet<string>();
        foreach (string module in Modules)
        {
            Transform moduleRoot = art.transform.Find(module);
            MeshFilter[] filters = moduleRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh).ToArray();
            if (filters.Length == 0)
                throw new InvalidOperationException(module + " has no readable imported Mesh.");
            int moduleTriangles = 0;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                    throw new InvalidOperationException("Mesh is not readable: " + mesh.name);
                if (mesh.vertexCount == 0)
                    throw new InvalidOperationException("Empty mesh: " + mesh.name);
                if (mesh.subMeshCount < 1 || mesh.subMeshCount > 4)
                    throw new InvalidOperationException(mesh.name + " exceeds the four-submesh limit or has no submesh.");

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer)
                    throw new InvalidOperationException("MeshFilter has no MeshRenderer: " + filter.name);
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0 || materials.Length > 4)
                    throw new InvalidOperationException(filter.name + " must have one to four material slots.");
                foreach (Material material in materials)
                {
                    if (!material || !Palette.ContainsKey(material.name))
                        throw new InvalidOperationException("Non-task or null material on " + filter.name);
                    string expectedPath = Folder + "/Materials/" + material.name + ".mat";
                    if (AssetDatabase.GetAssetPath(material) != expectedPath)
                        throw new InvalidOperationException(filter.name + " does not reference the F task material " + material.name);
                }

                int meshTriangles = CountTriangles(mesh);
                moduleTriangles += meshTriangles;
                result.MeshCount++;
                Bounds worldBounds = WorldBounds(filter);
                Report.Add(module + "/" + mesh.name +
                           ": triangles=" + meshTriangles +
                           "; vertices=" + mesh.vertexCount +
                           "; submeshes=" + mesh.subMeshCount +
                           "; slots=" + materials.Length +
                           "; materials=" + string.Join(",", materials.Select(material => material.name).ToArray()) +
                           "; localBounds=" + BoundsText(mesh.bounds) +
                           "; worldBounds=" + BoundsText(worldBounds));
            }

            if (moduleTriangles > 15000)
                throw new InvalidOperationException(module + " exceeds 15000 triangles: " + moduleTriangles);
            result.TotalTriangles += moduleTriangles;
            Report.Add(module + ": moduleTriangles=" + moduleTriangles + " (limit=15000).");

            foreach (CoreSpec spec in CoreSpecs.Where(spec => spec.Module == module))
            {
                float error = ValidateCoreCorners(filters, spec, layout);
                result.MaximumError = Mathf.Max(result.MaximumError, error);
                validatedCores.Add(spec.MeshPrefix);
            }
        }

        if (validatedCores.Count != CoreSpecs.Length)
            throw new InvalidOperationException("Not all three aligned core meshes were validated.");
        if (!(result.MaximumError < MaximumCornerError))
            throw new InvalidOperationException("Maximum core corner error must be <0.0002m; actual=" + result.MaximumError);
        Report.Add(string.Format(CultureInfo.InvariantCulture,
            "Total F art triangles: {0}; MeshFilters: {1}; maximum nearest-vertex core corner error: {2:F8}m (required <0.00020000m).",
            result.TotalTriangles, result.MeshCount, result.MaximumError));
        return result;
    }

    private static int CountTriangles(Mesh mesh)
    {
        int count = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            count += mesh.GetTriangles(submesh).Length / 3;
        return count;
    }

    private static float ValidateCoreCorners(MeshFilter[] moduleFilters, CoreSpec spec, LayoutFile layout)
    {
        MeshFilter[] matches = moduleFilters.Where(filter =>
            filter.sharedMesh.name.StartsWith(spec.MeshPrefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Expected exactly one Mesh with prefix " + spec.MeshPrefix +
                                                " in " + spec.Module + "; found " + matches.Length);
        LayoutItem collision = layout.items.Single(item => item.name == spec.CollisionName);
        Vector3[] vertices = matches[0].sharedMesh.vertices;
        Transform transform = matches[0].transform;
        float maximum = 0f;
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            Vector3 expected = collision.corners[cornerIndex];
            float nearestSquared = float.PositiveInfinity;
            Vector3 nearest = Vector3.zero;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 world = transform.TransformPoint(vertex);
                float squared = (world - expected).sqrMagnitude;
                if (squared >= nearestSquared)
                    continue;
                nearestSquared = squared;
                nearest = world;
            }
            float error = Mathf.Sqrt(nearestSquared);
            maximum = Mathf.Max(maximum, error);
            Report.Add(string.Format(CultureInfo.InvariantCulture,
                "Core {0} corner[{1}] ({2}): nearestVertex={3}, error={4:F8}m.",
                spec.MeshPrefix, cornerIndex, spec.CollisionName, VectorText(nearest), error));
        }
        Report.Add(string.Format(CultureInfo.InvariantCulture,
            "Core {0} <-> {1}: eight-corner maximum nearest-vertex error={2:F8}m.",
            spec.MeshPrefix, spec.CollisionName, maximum));
        if (!(maximum < MaximumCornerError))
            throw new InvalidOperationException(spec.MeshPrefix + " corner alignment failed: " + maximum + "m");
        return maximum;
    }

    private static Bounds WorldBounds(MeshFilter filter)
    {
        Vector3[] vertices = filter.sharedMesh.vertices;
        Transform transform = filter.transform;
        Bounds bounds = new Bounds(transform.TransformPoint(vertices[0]), Vector3.zero);
        for (int index = 1; index < vertices.Length; index++)
            bounds.Encapsulate(transform.TransformPoint(vertices[index]));
        return bounds;
    }

    private static void ValidateClearance(GameObject art, int expectedTriangleCount)
    {
        List<WorldTriangle> triangles = CollectWorldTriangles(art);
        int alignedSupportTriangles = art.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh && IsAlignedSupportMesh(filter.sharedMesh))
            .Sum(filter => CountTriangles(filter.sharedMesh));
        if (triangles.Count != expectedTriangleCount - alignedSupportTriangles)
            throw new InvalidOperationException("Triangle accounting mismatch before clearance test.");

        int hits = 0;
        int samples = 0;
        var examples = new List<string>();
        for (int segment = 0; segment < ClearanceRoute.Length - 1; segment++)
        {
            Vector3 from = ClearanceRoute[segment];
            Vector3 to = ClearanceRoute[segment + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.12f));
            int firstStep = segment == 0 ? 0 : 1;
            for (int step = firstStep; step <= steps; step++)
            {
                float t = (float)step / steps;
                Vector3 routePoint = Vector3.Lerp(from, to, t);
                float roadY = RoadSurfaceHeight(routePoint.x);
                float lowerY = roadY + 0.005f;
                float upperY = roadY + 1.30f;
                var box = new Bounds(
                    new Vector3(routePoint.x, (lowerY + upperY) * 0.5f, routePoint.z),
                    new Vector3(1.20f, upperY - lowerY, 1.20f));
                int sampleIndex = samples++;

                foreach (WorldTriangle triangle in triangles)
                {
                    if (triangle.Max.x < box.min.x || triangle.Min.x > box.max.x ||
                        triangle.Max.y < box.min.y || triangle.Min.y > box.max.y ||
                        triangle.Max.z < box.min.z || triangle.Min.z > box.max.z)
                        continue;
                    if (!TriangleIntersectsAabb(triangle.A, triangle.B, triangle.C, box))
                        continue;

                    hits++;
                    if (examples.Count < 16)
                    {
                        examples.Add("sample=" + sampleIndex + ", segment=" + segment +
                                     ", box=" + BoundsText(box) + ", triangle=" + triangle.Source +
                                     ", triangleIndex=" + triangle.TriangleIndex);
                    }
                }
            }
        }

        Report.Add("Clearance: " + samples + " route samples at <=0.12m spacing; X/Z half-extents=0.60m; " +
                   "Y=roadY+0.005..roadY+1.30; roadY is terrace 0.96 through X=4.00, exact Lip slope to X=4.49, then plate 0.78. Tested " + triangles.Count +
                   " non-support world triangles using broad-phase AABB plus 13-axis SAT; intersections=" + hits +
                   ". The three exact collision-support cores (" + alignedSupportTriangles +
                   " triangles) were validated separately against all 24 JSON corners and excluded as the supporting road itself.");
        if (hits != 0)
            throw new InvalidOperationException("F art clearance failed with " + hits +
                                                " triangle/AABB intersections. First hits: " + string.Join(" | ", examples.ToArray()));
    }

    private static float RoadSurfaceHeight(float worldX)
    {
        if (worldX <= 4.0f)
            return 0.96f;
        if (worldX < 4.49f)
            return 0.96f + (worldX - 4.0f) * (-0.18f / 0.49f);
        return 0.78f;
    }

    private static bool IsAlignedSupportMesh(Mesh mesh)
    {
        return CoreSpecs.Any(spec => mesh.name.StartsWith(spec.MeshPrefix, StringComparison.Ordinal));
    }

    private static List<WorldTriangle> CollectWorldTriangles(GameObject art)
    {
        var result = new List<WorldTriangle>();
        foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (!mesh || IsAlignedSupportMesh(mesh))
                continue;
            Vector3[] vertices = mesh.vertices;
            Transform transform = filter.transform;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] indices = mesh.GetTriangles(submesh);
                for (int index = 0; index < indices.Length; index += 3)
                {
                    Vector3 a = transform.TransformPoint(vertices[indices[index]]);
                    Vector3 b = transform.TransformPoint(vertices[indices[index + 1]]);
                    Vector3 c = transform.TransformPoint(vertices[indices[index + 2]]);
                    result.Add(new WorldTriangle
                    {
                        A = a,
                        B = b,
                        C = c,
                        Min = Vector3.Min(a, Vector3.Min(b, c)),
                        Max = Vector3.Max(a, Vector3.Max(b, c)),
                        Source = filter.transform.root.name + "/" + filter.name + "/" + mesh.name + "/submesh" + submesh,
                        TriangleIndex = index / 3
                    });
                }
            }
        }
        return result;
    }

    private static bool TriangleIntersectsAabb(Vector3 a, Vector3 b, Vector3 c, Bounds box)
    {
        // Akenine-Moller triangle/AABB SAT: three box axes, one triangle normal,
        // and nine cross products of box axes with triangle edges. No vertex-only shortcut.
        Vector3 center = box.center;
        Vector3 extents = box.extents;
        Vector3 v0 = a - center;
        Vector3 v1 = b - center;
        Vector3 v2 = c - center;
        Vector3 edge0 = v1 - v0;
        Vector3 edge1 = v2 - v1;
        Vector3 edge2 = v0 - v2;

        if (SeparatedByAxis(v0, v1, v2, Vector3.right, extents) ||
            SeparatedByAxis(v0, v1, v2, Vector3.up, extents) ||
            SeparatedByAxis(v0, v1, v2, Vector3.forward, extents) ||
            SeparatedByAxis(v0, v1, v2, Vector3.Cross(edge0, edge1), extents))
            return false;

        Vector3[] boxAxes = { Vector3.right, Vector3.up, Vector3.forward };
        Vector3[] edges = { edge0, edge1, edge2 };
        foreach (Vector3 edge in edges)
        {
            foreach (Vector3 axis in boxAxes)
            {
                if (SeparatedByAxis(v0, v1, v2, Vector3.Cross(edge, axis), extents))
                    return false;
            }
        }
        return true;
    }

    private static bool SeparatedByAxis(Vector3 a, Vector3 b, Vector3 c, Vector3 axis, Vector3 extents)
    {
        if (axis.sqrMagnitude < 0.00000000000000000001f)
            return false; // Degenerate cross products carry no separating information.
        float p0 = Vector3.Dot(a, axis);
        float p1 = Vector3.Dot(b, axis);
        float p2 = Vector3.Dot(c, axis);
        float minimum = Mathf.Min(p0, Mathf.Min(p1, p2));
        float maximum = Mathf.Max(p0, Mathf.Max(p1, p2));
        float radius = extents.x * Mathf.Abs(axis.x) +
                       extents.y * Mathf.Abs(axis.y) +
                       extents.z * Mathf.Abs(axis.z);
        // Touching is conservatively counted as intersection. The 0.005m lower-Y offset
        // keeps exact road-surface triangles below every sampled clearance AABB.
        return minimum > radius || maximum < -radius;
    }

    private static void CreateReferenceProxies(Scene scene, LayoutFile layout, bool hasBTower)
    {
        GameObject root = NewRoot("Reference White Layout - 20 Collision Bodies", scene);
        Material white = Material("MV_IvoryShade");
        var hiddenTargets = new HashSet<string>
        {
            "Landing Terrace",
            "Cascade Lip Ramp",
            "Cascade Pressure Plate Floor"
        };
        int index = 0;
        int visible = 0;
        foreach (LayoutItem item in layout.items.Where(item => !item.anchor))
        {
            GameObject proxy = new GameObject("Proxy - " + item.name);
            SceneManager.MoveGameObjectToScene(proxy, scene);
            proxy.transform.SetParent(root.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.transform.localRotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;

            Mesh mesh = new Mesh { name = "Reference_" + item.name };
            mesh.vertices = (Vector3[])item.corners.Clone();
            mesh.triangles = ClosedCornerTriangles(item.corners);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = ReferenceMeshFolder + "/Proxy_" + index.ToString("00", CultureInfo.InvariantCulture) + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);

            MeshFilter filter = proxy.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = proxy.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = white;
            bool visibleProxy = !hiddenTargets.Contains(item.name);
            if (hasBTower && item.name == "Launch Coping")
                visibleProxy = false;
            renderer.enabled = visibleProxy;
            if (visibleProxy)
                visible++;
            index++;
        }

        int expectedVisible = hasBTower ? 16 : 17;
        if (index != 20 || visible != expectedVisible)
            throw new InvalidOperationException("Reference proxy visibility mismatch: bodies=" + index + ", visible=" + visible);
        Report.Add("Reference white proxies: 20 meshes built directly from JSON corner data; uniform matte MV_IvoryShade; " +
                   "Landing Terrace/Cascade Lip Ramp/Cascade Pressure Plate Floor renderers disabled; visible=" + visible +
                   (hasBTower ? " (Launch Coping also hidden for BReleaseTower)." : "."));
    }

    private static int[] ClosedCornerTriangles(Vector3[] corners)
    {
        // The layout corner order is the local +/-X, +/-Y, +/-Z OBB order. These six
        // quads use all eight supplied world-space corners, including rotated ramps.
        int[][] faces =
        {
            new[] { 0, 1, 5, 4 }, // -Y
            new[] { 2, 6, 7, 3 }, // +Y
            new[] { 0, 2, 3, 1 }, // -Z
            new[] { 4, 5, 7, 6 }, // +Z
            new[] { 0, 4, 6, 2 }, // -X
            new[] { 1, 3, 7, 5 }  // +X
        };
        Vector3 meshCenter = corners.Aggregate(Vector3.zero, (sum, point) => sum + point) / corners.Length;
        var triangles = new List<int>(36);
        foreach (int[] face in faces)
        {
            Vector3 faceCenter = (corners[face[0]] + corners[face[1]] + corners[face[2]] + corners[face[3]]) * 0.25f;
            Vector3 normal = Vector3.Cross(corners[face[1]] - corners[face[0]], corners[face[2]] - corners[face[0]]);
            bool reverse = Vector3.Dot(normal, faceCenter - meshCenter) < 0f;
            int second = reverse ? face[3] : face[1];
            int fourth = reverse ? face[1] : face[3];
            triangles.Add(face[0]);
            triangles.Add(second);
            triangles.Add(face[2]);
            triangles.Add(face[0]);
            triangles.Add(face[2]);
            triangles.Add(fourth);
        }
        return triangles.ToArray();
    }

    private static void CreateGoldBall(Scene scene)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Reference Gold Ball - Diameter 0.76 - Preview Only";
        SceneManager.MoveGameObjectToScene(ball, scene);
        ball.transform.localPosition = new Vector3(2.10f, 1.34f, 4.90f);
        ball.transform.localRotation = Quaternion.identity;
        ball.transform.localScale = Vector3.one * 0.76f;
        Collider collider = ball.GetComponent<Collider>();
        if (collider)
            Object.DestroyImmediate(collider);
        ball.GetComponent<MeshRenderer>().sharedMaterial = Material("MV_Gold");
        Report.Add("Preview scale ball: centre=(2.10,1.34,4.90), diameter=0.76, MV_Gold, Collider removed.");
    }

    private static void CreateLighting(Scene scene)
    {
        GameObject keyObject = NewRoot("Warm Directional Key - From (-35,50,-30)", scene);
        Vector3 lightFrom = new Vector3(-35f, 50f, -30f);
        keyObject.transform.rotation = Quaternion.LookRotation(-lightFrom, Vector3.up);
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f, 0.93f, 0.84f);
        key.intensity = 1.25f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.68f;
        key.shadowBias = 0.04f;
        key.shadowNormalBias = 0.35f;

        GameObject fillObject = NewRoot("Cool Broad Fill", scene);
        fillObject.transform.rotation = Quaternion.Euler(55f, 135f, 0f);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.68f, 0.79f, 1f);
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.66f, 0.72f, 0.82f);
        RenderSettings.ambientIntensity = 0.80f;
        RenderSettings.fog = false;
        RenderSettings.sun = key;
        QualitySettings.shadowDistance = 80f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = 4;
        QualitySettings.antiAliasing = 4;
        Report.Add("Lighting: warm soft directional key approximately from (-35,50,-30), cool fill, flat ambient; pale blue-grey camera background.");
    }

    private static Camera CreateCamera(Scene scene, string name, Vector3 position, Vector3 target,
        bool orthographic, float sizeOrFov, bool enabled)
    {
        GameObject cameraObject = NewRoot(name, scene);
        cameraObject.transform.position = position;
        cameraObject.transform.LookAt(target, Vector3.up);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = orthographic;
        if (orthographic)
            camera.orthographicSize = sizeOrFov;
        else
            camera.fieldOfView = sizeOrFov;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.70f, 0.78f, 0.85f, 1f);
        camera.allowMSAA = true;
        camera.allowHDR = false;
        camera.useOcclusionCulling = false;
        camera.aspect = (float)ImageWidth / ImageHeight;
        camera.enabled = enabled;
        return camera;
    }

    private static void AssertPreviewHasNoPhysics(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            int colliders = root.GetComponentsInChildren<Collider>(true).Length;
            int rigidbodies = root.GetComponentsInChildren<Rigidbody>(true).Length;
            if (colliders != 0 || rigidbodies != 0)
                throw new InvalidOperationException("Preview contains physics on " + root.name +
                                                    ": colliders=" + colliders + ", rigidbodies=" + rigidbodies);
        }
    }

    private static void SavePreview(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, PreviewScenePath))
            throw new IOException("Could not save " + PreviewScenePath);
    }

    private static void Render(Camera camera, string outputAssetPath)
    {
        RenderTexture renderTexture = null;
        Texture2D image = null;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        bool previousEnabled = camera.enabled;
        try
        {
            renderTexture = new RenderTexture(ImageWidth, ImageHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "FGoalTerrace Temporary RenderTexture",
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            if (!renderTexture.IsCreated())
                throw new InvalidOperationException("Could not create 1920x1080 ARGB32/depth24/MSAA4 RenderTexture.");
            image = new Texture2D(ImageWidth, ImageHeight, TextureFormat.RGBA32, false, false);

            camera.enabled = true;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, ImageWidth, ImageHeight), 0, 0);
            image.Apply();
            if (image.width != ImageWidth || image.height != ImageHeight)
                throw new InvalidOperationException("Unexpected ReadPixels dimensions for " + outputAssetPath);

            Color32[] pixels = image.GetPixels32();
            if (pixels.Length != ImageWidth * ImageHeight)
                throw new InvalidOperationException("Unexpected rendered pixel count for " + outputAssetPath);
            double meanR = 0;
            double meanG = 0;
            double meanB = 0;
            foreach (Color32 pixel in pixels)
            {
                meanR += pixel.r;
                meanG += pixel.g;
                meanB += pixel.b;
            }
            meanR /= pixels.Length;
            meanG /= pixels.Length;
            meanB /= pixels.Length;
            double squared = 0;
            foreach (Color32 pixel in pixels)
            {
                double dr = pixel.r - meanR;
                double dg = pixel.g - meanG;
                double db = pixel.b - meanB;
                squared += dr * dr + dg * dg + db * db;
            }
            double variance = squared / (pixels.Length * 3.0);
            if (!(variance > 1.0))
                throw new InvalidOperationException("Rendered image is empty or effectively uniform: " + outputAssetPath +
                                                    ", RGB variance=" + variance);

            byte[] png = image.EncodeToPNG();
            ValidatePng(png, ImageWidth, ImageHeight);
            string fullPath = OutputPath(outputAssetPath);
            File.WriteAllBytes(fullPath, png);
            FileInfo info = new FileInfo(fullPath);
            if (!info.Exists || info.Length < 10000)
                throw new InvalidOperationException("PNG is missing or too small: " + outputAssetPath);
            Report.Add(string.Format(CultureInfo.InvariantCulture,
                "Screenshot: {0}; dimensions={1}x{2}; ARGB32 depth24 MSAA4; bytes={3}; RGB variance={4:F2}; non-empty=Pass.",
                outputAssetPath, ImageWidth, ImageHeight, info.Length, variance));
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.enabled = previousEnabled;
            RenderTexture.active = previousActive;
            // UnityEngine.Object resources are not managed IDisposable objects:
            // explicitly release the RenderTexture and destroy both temporary objects.
            if (image)
                Object.DestroyImmediate(image);
            if (renderTexture)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }
        }
    }

    private static void ValidatePng(byte[] png, int expectedWidth, int expectedHeight)
    {
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (png == null || png.Length < 24 || !signature.Select((value, index) => png[index] == value).All(value => value))
            throw new InvalidOperationException("Invalid PNG signature.");
        int width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        int height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        if (width != expectedWidth || height != expectedHeight)
            throw new InvalidOperationException("PNG dimensions do not match 1920x1080.");
    }

    private static GameObject NewRoot(string name, Scene scene)
    {
        GameObject root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root;
    }

    private static string VectorText(Vector3 value)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:F6},{1:F6},{2:F6})", value.x, value.y, value.z);
    }

    private static string BoundsText(Bounds bounds)
    {
        return "min=" + VectorText(bounds.min) + ",max=" + VectorText(bounds.max);
    }
}
