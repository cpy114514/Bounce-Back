// Editor-only isolated-project validation and preview generation for A-FloatingIsland.
// Kept under ArtSource: no runtime or authoring assembly is changed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AFloatingIslandPreview
{
    private const string Root = "Assets/Scenes/Archive/BounceBack/Art/A-FloatingIsland";
    private const string FbxPath = Root + "/FBX/A_FloatingIsland.fbx";
    private const string PrefabPath = Root + "/Prefabs/A_FloatingIsland.prefab";
    private static readonly Vector3 Anchor = new Vector3(0f, -0.22f, 0.05f);
    private static readonly Vector3 CameraFocus = new Vector3(-0.05f, 0.95f, 0.25f);
    private static readonly Vector3 CameraDirection = new Vector3(12f, 15f, -15f);
    // Unity/FBX preserves the source Blender slot order differently from the
    // declaration order. These triangle counts are unique per material and make
    // the renderer mapping deterministic instead of relying on slot numbers.
    private static readonly Dictionary<int, string> MaterialByTriangleCount = new Dictionary<int, string>
    {
        { 1804, "MV_Ivory" }, { 496, "MV_DomeBlue" }, { 448, "MV_Rose" }, { 388, "MV_RoseDeep" }
    };
    private static readonly string[] ExpectedSlotOrder = { "MV_Ivory", "MV_Rose", "MV_RoseDeep", "MV_DomeBlue" };

    [Serializable] private class Layout { public LayoutItem[] items; }
    [Serializable]
    private class LayoutItem
    {
        public string name;
        public Vector3 center;
        public Vector3 size;
        public Quaternion rotation;
        public bool anchor;
        public Vector3[] corners;
    }
    [Serializable]
    private class Report
    {
        public bool passed;
        public string unityVersion;
        public string scope;
        public string error = "";
        public int collisionBoxes;
        public int anchors;
        public int meshObjects;
        public int triangles;
        public int materialSlots;
        public int vertices;
        public int uvLayers;
        public string[] materialNames;
        public int[] trianglesPerMaterial;
        public float[] dimensionsLocal;
        public float[] boundsMinLocal;
        public float[] boundsMaxLocal;
        public float[] anchorWorld;
        public float topLocalY;
        public float authoringFoundationTopWorldY = -0.225f;
        public float authoringTopAlignmentError;
        public int topCoverageSamples;
        public int topCoverageMisses;
        public float topCoverageMaxError;
        public float minimumCollisionTopWorldY;
        public float artMaximumWorldY;
        public float roadAboveSurfaceVerticalClearance;
        public float waterfallBackWorldX;
        public int waterfallVerticalSamples;
        public int waterfallVerticalMisses;
        public float eastRouteMinimumSurfaceDistance;
        public bool identityRoot;
        public bool visualOnly;
        public bool[] screenshot1920x1080;
        public string[] screenshots;
        public float formalBaseViewportMinY;
        public float formalBaseViewportMaxY;
        public string formalCamera;
        public string reverseCamera;
    }

    public static void BuildAndValidate()
    {
        string output = GetOutputDirectory();
        Report report = new Report
        {
            unityVersion = Application.unityVersion,
            scope = "Isolated Unity import, visual Prefab, whitebox preview and camera rendering; no formal scene or gameplay changes.",
            anchorWorld = Vec(Anchor),
            formalCamera = "Orthographic size 9.5; focus (-0.05,0.95,0.25); camera focus+(12,15,-15).",
            reverseCamera = "Position (5,1.5,5.2), looking at reference tower centre (-5.64,2.3,-3.65); perspective FOV 70."
        };
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolders();
            ConfigureImporter();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject prefabRoot = BuildPrefab(materials);
            ValidatePrefab(prefabRoot, report);
            Layout layout = ReadLayout();
            report.collisionBoxes = layout.items.Count(item => !item.anchor);
            report.anchors = layout.items.Length - report.collisionBoxes;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject island = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            island.name = "A_FloatingIsland @ (0,-0.22,0.05)";
            island.transform.SetPositionAndRotation(Anchor, Quaternion.identity);
            island.transform.localScale = Vector3.one;
            GameObject whitebox = new GameObject("Reference Whitebox - preview only");
            BuildWhitebox(layout, whitebox.transform, materials);
            BuildScaleBall(layout, whitebox.transform, materials["MV_Gold"]);
            ConfigureLighting();
            Camera formal = CreateFormalCamera();
            Camera reverse = CreateReverseCamera();
            reverse.gameObject.SetActive(false);

            MeshFilter filter = island.GetComponentsInChildren<MeshFilter>(true)[0];
            ValidateTopAndChannels(island, filter, report);
            ValidateRoadAndEastRoute(island, filter, layout, report);
            MeasureBaseProjection(island, filter, formal, report);

            string scenePath = Root + "/Preview/A_FloatingIsland_Preview.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            bool noRender = Environment.GetCommandLineArgs().Contains("--afloat-no-render");
            if (noRender)
            {
                report.screenshots = new string[0];
                report.screenshot1920x1080 = new bool[0];
                Debug.Log("AFLOAT_RENDER_SKIPPED: headless import/Prefab/alignment validation only.");
            }
            else
            {
                string formalPath = Path.Combine(output, "Preview", "A_FloatingIsland_Formal.png");
                string reversePath = Path.Combine(output, "Preview", "A_FloatingIsland_FromGoal.png");
                RenderCamera(formal, formalPath);
                RenderCamera(reverse, reversePath);
                report.screenshots = new[]
                {
                    "Preview/A_FloatingIsland_Formal.png",
                    "Preview/A_FloatingIsland_FromGoal.png"
                };
                report.screenshot1920x1080 = new[] { true, true };
                formal.targetTexture = null;
                reverse.targetTexture = null;
                reverse.gameObject.SetActive(false);
                formal.gameObject.SetActive(true);
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            report.passed = report.meshObjects == 1 && report.triangles <= 15000 &&
                            report.materialSlots <= 4 && report.visualOnly && report.identityRoot &&
                            report.topCoverageMisses == 0 && report.topCoverageMaxError <= 0.02f &&
                            report.authoringTopAlignmentError <= 0.02f && report.waterfallVerticalMisses == 0 &&
                            Mathf.Abs(report.waterfallBackWorldX - 5.72f) <= 0.02f &&
                            report.eastRouteMinimumSurfaceDistance > 0.76f &&
                            report.roadAboveSurfaceVerticalClearance > 0.02f;
            if (!report.passed)
                throw new InvalidOperationException("One or more A-FloatingIsland validation checks failed.");
            File.WriteAllText(Path.Combine(output, "Documentation", "A_FloatingIsland_Unity_Validation.json"),
                JsonUtility.ToJson(report, true));
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            Debug.Log("AFLOAT_UNITY_VALIDATION_PASSED triangles=" + report.triangles +
                      " materials=" + report.materialSlots + " topError=" + report.topCoverageMaxError +
                      " eastRouteDistance=" + report.eastRouteMinimumSurfaceDistance);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            report.passed = false;
            report.error = exception.ToString();
            Directory.CreateDirectory(Path.Combine(output, "Documentation"));
            File.WriteAllText(Path.Combine(output, "Documentation", "A_FloatingIsland_Unity_Validation.json"),
                JsonUtility.ToJson(report, true));
            Debug.LogError("AFLOAT_UNITY_VALIDATION_FAILED\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    private static string GetOutputDirectory()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--afloat-output");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("Missing --afloat-output <absolute delivery directory>.");
        string output = args[index + 1];
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "Preview"));
        Directory.CreateDirectory(Path.Combine(output, "Documentation"));
        return output;
    }

    private static void EnsureFolders()
    {
        foreach (string suffix in new[] { "Materials", "Prefabs", "Preview", "Documentation" })
            Directory.CreateDirectory(Path.GetFullPath(Root + "/" + suffix));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void ConfigureImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("FBX did not import", FbxPath);
        importer.globalScale = 1f;
        importer.useFileUnits = true;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;
        importer.generateSecondaryUV = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        Dictionary<string, Color> colors = new Dictionary<string, Color>
        {
            { "MV_Ivory", new Color(0.95f, 0.88f, 0.77f, 1f) },
            { "MV_DomeBlue", new Color(0.30f, 0.47f, 0.80f, 1f) },
            { "MV_Rose", new Color(0.74f, 0.45f, 0.47f, 1f) },
            { "MV_RoseDeep", new Color(0.55f, 0.30f, 0.38f, 1f) },
            { "MV_Gold", new Color(0.96f, 0.70f, 0.32f, 1f) },
            { "MV_Mint", new Color(0.36f, 0.70f, 0.55f, 1f) },
            { "MV_Water", new Color(0.45f, 0.80f, 0.84f, 1f) }
        };
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP/Lit shader unavailable.");
        Dictionary<string, Material> result = new Dictionary<string, Material>();
        foreach (KeyValuePair<string, Color> pair in colors)
        {
            string path = Root + "/Materials/" + pair.Key + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                AssetDatabase.DeleteAsset(path);
            Material material = new Material(shader) { name = pair.Key, enableInstancing = true };
            material.SetColor("_BaseColor", pair.Value);
            material.SetColor("_Color", pair.Value);
            material.SetFloat("_Metallic", pair.Key == "MV_Gold" ? 0.25f : 0f);
            material.SetFloat("_Smoothness", pair.Key == "MV_DomeBlue" || pair.Key == "MV_Water" ? 0.32f : 0.16f);
            AssetDatabase.CreateAsset(material, path);
            result.Add(pair.Key, material);
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static GameObject BuildPrefab(Dictionary<string, Material> materials)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null) throw new InvalidOperationException("No imported FBX root.");
        GameObject root = new GameObject("A_FloatingIsland");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        instance.name = "A_FloatingIsland_Imported_AxisFix";
        instance.transform.localPosition = Vector3.zero;
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length != 1) throw new InvalidOperationException("Expected one MeshRenderer.");
        MeshFilter importedFilter = root.GetComponentsInChildren<MeshFilter>(true)[0];
        Mesh importedMesh = importedFilter.sharedMesh;
        if (importedMesh.subMeshCount != 4)
            throw new InvalidOperationException("Expected four imported material submeshes.");
        List<string> mappedNames = new List<string>();
        for (int slot = 0; slot < importedMesh.subMeshCount; slot++)
        {
            int triangles = importedMesh.GetTriangles(slot).Length / 3;
            if (!MaterialByTriangleCount.TryGetValue(triangles, out string materialName))
                throw new InvalidOperationException("No unique material mapping for submesh triangles=" + triangles);
            if (mappedNames.Contains(materialName))
                throw new InvalidOperationException("Ambiguous repeated material triangle count: " + materialName);
            mappedNames.Add(materialName);
        }
        renderers[0].sharedMaterials = mappedNames.Select(name => materials[name]).ToArray();
        renderers[0].shadowCastingMode = ShadowCastingMode.On;
        renderers[0].receiveShadows = true;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        return root;
    }

    private static void ValidatePrefab(GameObject root, Report report)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        if (filters.Length != 1 || renderers.Length != 1 || filters[0].sharedMesh == null)
            throw new InvalidOperationException("Expected exactly one imported mesh and renderer.");
        Mesh mesh = filters[0].sharedMesh;
        Bounds bounds = LocalBounds(root.transform, filters[0]);
        report.meshObjects = filters.Length;
        report.triangles = mesh.triangles.Length / 3;
        report.materialSlots = mesh.subMeshCount;
        report.vertices = mesh.vertexCount;
        report.uvLayers = mesh.uv.Length == mesh.vertexCount ? 1 : 0;
        report.materialNames = renderers[0].sharedMaterials.Select(material => material.name).ToArray();
        report.trianglesPerMaterial = Enumerable.Range(0, mesh.subMeshCount)
            .Select(index => mesh.GetTriangles(index).Length / 3).ToArray();
        report.dimensionsLocal = Vec(bounds.size);
        report.boundsMinLocal = Vec(bounds.min);
        report.boundsMaxLocal = Vec(bounds.max);
        report.topLocalY = bounds.max.y;
        report.identityRoot = root.transform.localPosition == Vector3.zero &&
                              root.transform.localRotation == Quaternion.identity &&
                              root.transform.localScale == Vector3.one;
        report.visualOnly = root.GetComponentsInChildren<Collider>(true).Length == 0 &&
                            root.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
                            root.GetComponentsInChildren<Animator>(true).Length == 0 &&
                            root.GetComponentsInChildren<Animation>(true).Length == 0 &&
                            root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0;
        bool expectedBounds = Vector3.Distance(bounds.min, new Vector3(-6.76f, -11.15f, -5.495f)) < 0.001f &&
                              Vector3.Distance(bounds.max, new Vector3(7.48f, 0f, 5.495f)) < 0.001f;
        if (!expectedBounds)
            throw new InvalidOperationException($"Unexpected Unity-axis bounds: min={bounds.min}, max={bounds.max}");
        if (report.triangles > 15000 || report.materialSlots != 4 || report.uvLayers != 1 ||
            !report.identityRoot || !report.visualOnly || Mathf.Abs(bounds.max.y) > 0.0001f ||
            mesh.normals.Length != mesh.vertexCount || mesh.vertices.Any(v => !Finite(v)))
            throw new InvalidOperationException("Mesh budget, UV, pivot or visual-only validation failed: " + bounds);
        if (report.trianglesPerMaterial.Any(count => count == 0))
            throw new InvalidOperationException("An MV material submesh has no triangles.");
        if (!ExpectedSlotOrder.SequenceEqual(report.materialNames))
            throw new InvalidOperationException("Unexpected material slot order: " + string.Join(",", report.materialNames));
        for (int slot = 0; slot < mesh.subMeshCount; slot++)
        {
            int triangles = mesh.GetTriangles(slot).Length / 3;
            if (!MaterialByTriangleCount.TryGetValue(triangles, out string expectedName) ||
                expectedName != report.materialNames[slot])
                throw new InvalidOperationException("Material/submesh triangle-count mismatch at slot " + slot);
        }
    }

    private static Layout ReadLayout()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(project, "ArtSource", "Santorini Layout", "Santorini Collision Layout.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Missing layout truth", path);
        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
        if (layout == null || layout.items == null || layout.items.Length != 27)
            throw new InvalidOperationException("Expected 27 layout entries (20 collision boxes and 7 anchors).");
        return layout;
    }

    private static void BuildWhitebox(Layout layout, Transform parent, Dictionary<string, Material> materials)
    {
        foreach (LayoutItem item in layout.items.Where(item => !item.anchor))
        {
            Material material = item.name == "Lift Wall" ? materials["MV_Mint"] :
                item.name == "Elastic Pool Surface" ? materials["MV_Water"] : materials["MV_Ivory"];
            CreateBox(parent, item.name, item.center, item.size, item.rotation, material);
        }
        // Foundation whiteboxes are not present in the JSON. Read-only dimensions
        // come from SantoriniSystemsAuthoring.cs lines 114-117.
        CreateBox(parent, "Reference Floating Limestone Foundation", new Vector3(0f, -0.70f, 0.05f),
            new Vector3(12.15f, 0.72f, 9.65f), Quaternion.identity, materials["MV_Ivory"]);
        CreateBox(parent, "Reference Lower Limestone Course", new Vector3(0f, -1.13f, 0.05f),
            new Vector3(11.68f, 0.18f, 9.20f), Quaternion.identity, materials["MV_Ivory"]);
        CreateBox(parent, "Reference Foundation Crown (top -0.225)", new Vector3(0f, -0.29f, 0.05f),
            new Vector3(12.36f, 0.13f, 9.83f), Quaternion.identity, materials["MV_Ivory"]);
        // The JSON carries the tower's collision coping only. Read-only blockout
        // dimensions from SantoriniSystemsAuthoring.cs lines 122-123 complete the
        // visual reference so the reverse shot actually shows a tower.
        CreateBox(parent, "Reference Tower Foot", new Vector3(-5.64f, 0.10f, -3.65f),
            new Vector3(2.17f, 0.45f, 2.20f), Quaternion.identity, materials["MV_Ivory"]);
        CreateBox(parent, "Reference White Tower", new Vector3(-5.64f, 2.27f, -3.65f),
            new Vector3(1.77f, 4.02f, 1.79f), Quaternion.identity, materials["MV_Ivory"]);
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size,
        Quaternion rotation, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.SetPositionAndRotation(center, rotation);
        obj.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    private static void BuildScaleBall(Layout layout, Transform parent, Material gold)
    {
        LayoutItem release = layout.items.First(item => item.name == "Tower Release");
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Reference Gold Ball - diameter 0.76m";
        ball.transform.SetParent(parent, false);
        ball.transform.position = release.center;
        ball.transform.localScale = Vector3.one * 0.76f;
        UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.GetComponent<MeshRenderer>().sharedMaterial = gold;
    }

    private static void ConfigureLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.74f, 0.68f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.64f, 0.60f, 0.66f);
        RenderSettings.ambientGroundColor = new Color(0.38f, 0.32f, 0.45f);
        RenderSettings.ambientIntensity = 0.75f;
        RenderSettings.fog = false;
        CreateLight("Warm soft key", new Vector3(0.55f, -1f, 0.70f), 1.20f, new Color(1f, 0.88f, 0.76f));
        CreateLight("Cool fill", new Vector3(-0.65f, -0.40f, -0.55f), 0.32f, new Color(0.72f, 0.82f, 1f));
    }

    private static void CreateLight(string name, Vector3 rays, float intensity, Color color)
    {
        GameObject obj = new GameObject(name);
        Light light = obj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.Soft;
        obj.transform.rotation = Quaternion.LookRotation(rays.normalized, Vector3.up);
    }

    private static Camera CreateFormalCamera()
    {
        GameObject obj = new GameObject("Camera - Formal Orthographic");
        Camera camera = obj.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9.5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.71f, 0.82f, 0.87f);
        camera.transform.SetPositionAndRotation(CameraFocus + CameraDirection,
            Quaternion.LookRotation(-CameraDirection, Vector3.up));
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        return camera;
    }

    private static Camera CreateReverseCamera()
    {
        GameObject obj = new GameObject("Camera - From Goal Back To Tower");
        Camera camera = obj.AddComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.71f, 0.82f, 0.87f);
        camera.transform.position = new Vector3(5f, 1.5f, 5.2f);
        camera.transform.LookAt(new Vector3(-5.64f, 2.3f, -3.65f), Vector3.up);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        return camera;
    }

    private static void ValidateTopAndChannels(GameObject island, MeshFilter filter, Report report)
    {
        Mesh mesh = filter.sharedMesh;
        const int countX = 25;
        const int countZ = 21;
        float maxError = 0f;
        int misses = 0;
        for (int ix = 0; ix < countX; ix++)
        {
            float x = Mathf.Lerp(-6.18f + 0.0001f, 6.18f - 0.0001f, ix / (float)(countX - 1));
            for (int iz = 0; iz < countZ; iz++)
            {
                float z = Mathf.Lerp(-4.915f + 0.0001f, 4.915f - 0.0001f, iz / (float)(countZ - 1));
                float y;
                if (!MeshHeightAt(island.transform, filter, new Vector3(x, 2f, z), 4f, out y)) misses++;
                else maxError = Mathf.Max(maxError, Mathf.Abs(y));
            }
        }
        report.topCoverageSamples = countX * countZ;
        report.topCoverageMisses = misses;
        report.topCoverageMaxError = maxError;
        report.authoringTopAlignmentError = Mathf.Abs(Anchor.y + maxError - report.authoringFoundationTopWorldY);
        report.artMaximumWorldY = island.transform.TransformPoint(new Vector3(0f, report.topLocalY, 0f)).y;

        Vector3 hitLocal;
        bool channelHit = MeshRay(island.transform, filter, new Vector3(8f, -1f, 5.19f), Vector3.left, 4f, out hitLocal);
        report.waterfallBackWorldX = channelHit ? island.transform.TransformPoint(hitLocal).x : float.NaN;
        if (!channelHit) throw new InvalidOperationException("Waterfall groove back plane not found.");
        int verticalMisses = 0;
        foreach (float x in new[] { 5.73f, 5.85f, 6.00f, 6.15f })
        foreach (float z in new[] { 5.041f, 5.10f, 5.27f, 5.44f, 5.499f })
        {
            if (MeshRay(island.transform, filter, new Vector3(x, 0.74f, z), Vector3.down, 12.24f, out hitLocal))
                verticalMisses++;
        }
        report.waterfallVerticalSamples = 20;
        report.waterfallVerticalMisses = verticalMisses;
        if (misses != 0 || maxError > 0.02f || verticalMisses != 0)
            throw new InvalidOperationException($"Top/channel validation failed: topMisses={misses}, error={maxError}, waterHits={verticalMisses}");
    }

    private static void ValidateRoadAndEastRoute(GameObject island, MeshFilter filter, Layout layout, Report report)
    {
        report.minimumCollisionTopWorldY = layout.items.Where(item => !item.anchor)
            .Min(item => item.corners.Max(corner => corner.y));
        report.roadAboveSurfaceVerticalClearance = report.minimumCollisionTopWorldY - report.artMaximumWorldY;
        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices.Select(vertex =>
            island.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex))).ToArray();
        int[] triangles = mesh.triangles;
        Vector3[] route =
        {
            new Vector3(9.7f, 0.86f, 3.83f), new Vector3(12.03f, 0.86f, 3.83f),
            new Vector3(14.6f, 1.54f, 4.50f), new Vector3(15.7f, 1.54f, 4.50f),
            new Vector3(14.74f, 0.58f, 5.46f)
        };
        float minimum = float.PositiveInfinity;
        for (int segment = 0; segment < route.Length - 1; segment++)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(route[segment], route[segment + 1]) / 0.25f);
            for (int step = 0; step <= steps; step++)
            {
                Vector3 world = Vector3.Lerp(route[segment], route[segment + 1], step / (float)steps);
                Vector3 local = island.transform.InverseTransformPoint(world);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 closest = ClosestPointOnTriangle(local, vertices[triangles[index]],
                        vertices[triangles[index + 1]], vertices[triangles[index + 2]]);
                    minimum = Mathf.Min(minimum, Vector3.Distance(local, closest));
                }
            }
        }
        report.eastRouteMinimumSurfaceDistance = minimum;
        if (report.roadAboveSurfaceVerticalClearance <= 0.02f || minimum <= 0.76f)
            throw new InvalidOperationException("Road or east route clearance failed.");
    }

    private static void MeasureBaseProjection(GameObject island, MeshFilter filter, Camera camera, Report report)
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (Vector3 vertex in filter.sharedMesh.vertices)
        {
            Vector3 local = island.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
            if (local.y >= -0.0001f) continue;
            Vector3 viewport = camera.WorldToViewportPoint(island.transform.TransformPoint(local));
            min = Mathf.Min(min, viewport.y);
            max = Mathf.Max(max, viewport.y);
        }
        report.formalBaseViewportMinY = min;
        report.formalBaseViewportMaxY = max;
    }

    private static bool MeshHeightAt(Transform root, MeshFilter filter, Vector3 localOrigin, float distance, out float localY)
    {
        Vector3 hit;
        bool result = MeshRay(root, filter, localOrigin, Vector3.down, distance, out hit);
        localY = result ? hit.y : 0f;
        return result;
    }

    private static bool MeshRay(Transform root, MeshFilter filter, Vector3 localOrigin, Vector3 localDirection,
        float distance, out Vector3 localHit)
    {
        Vector3 direction = localDirection.normalized;
        Vector3[] vertices = filter.sharedMesh.vertices.Select(vertex =>
            root.InverseTransformPoint(filter.transform.TransformPoint(vertex))).ToArray();
        int[] triangles = filter.sharedMesh.triangles;
        float nearest = distance;
        bool found = false;
        for (int index = 0; index < triangles.Length; index += 3)
        {
            float hitDistance;
            if (RayTriangle(localOrigin, direction, vertices[triangles[index]],
                    vertices[triangles[index + 1]], vertices[triangles[index + 2]],
                    nearest, out hitDistance))
            {
                nearest = hitDistance;
                found = true;
            }
        }
        localHit = found ? localOrigin + direction * nearest : Vector3.zero;
        return found;
    }

    private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
        float maximumDistance, out float distance)
    {
        const float epsilon = 0.0000001f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = Vector3.Cross(direction, edge2);
        float determinant = Vector3.Dot(edge1, p);
        if (Mathf.Abs(determinant) < epsilon) { distance = 0f; return false; }
        float inverse = 1f / determinant;
        Vector3 t = origin - a;
        float u = Vector3.Dot(t, p) * inverse;
        if (u < -epsilon || u > 1f + epsilon) { distance = 0f; return false; }
        Vector3 q = Vector3.Cross(t, edge1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < -epsilon || u + v > 1f + epsilon) { distance = 0f; return false; }
        distance = Vector3.Dot(edge2, q) * inverse;
        return distance >= 0f && distance <= maximumDistance;
    }

    private static Bounds LocalBounds(Transform root, MeshFilter filter)
    {
        bool initialized = false;
        Bounds result = new Bounds();
        foreach (Vector3 vertex in filter.sharedMesh.vertices)
        {
            Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
            if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
            else result.Encapsulate(point);
        }
        return result;
    }

    // Real-time Collision Detection, Christer Ericson, closest-point-on-triangle.
    private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + v * ab;
        }
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + w * ac;
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + w * (c - b);
        }
        float denominator = 1f / (va + vb + vc);
        float baryV = vb * denominator;
        float baryW = vc * denominator;
        return a + ab * baryV + ac * baryW;
    }

    private static void RenderCamera(Camera camera, string output)
    {
        const int width = 1920;
        const int height = 1080;
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        target.Create();
        camera.targetTexture = target;
        camera.gameObject.SetActive(true);
        DynamicGI.UpdateEnvironment();
        // The first editor batch render can precede URP material/shader readiness.
        // Render twice with synchronous shader compilation before reading pixels.
        camera.Render();
        GL.Flush();
        camera.Render();
        GL.Flush();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(output, image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        if (!File.Exists(output) || new FileInfo(output).Length < 10000)
            throw new InvalidOperationException("Screenshot was not written: " + output);
        Debug.Log("AFLOAT_SCREENSHOT=" + output);
    }

    private static float[] Vec(Vector3 value) => new[] { value.x, value.y, value.z };
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
