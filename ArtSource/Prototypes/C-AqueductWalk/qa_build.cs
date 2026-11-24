// Editor-only isolated QA. Copy into .utmp/C-AqueductWalk-QA/Assets/Editor.
// The delivered prefab contains only four static mesh children and no scripts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CAqueductWalkQABuild
{
    private const string Folder = "Assets/Scenes/Archive/BounceBack/Art/C-AqueductWalk";
    private const string FbxPath = Folder + "/FBX/C-AqueductWalk.fbx";
    private const string PrefabPath = Folder + "/Prefabs/C-AqueductWalk.prefab";
    private const string ScenePath = Folder + "/Preview/C-AqueductWalk Preview.unity";
    private static readonly Vector3 Anchor = new(-3.20f, 4.27f, -2.10f);
    private static readonly string[] DeckNames = { "Straight Stair Ramp", "Turn Ramp 1", "Turn Ramp 2", "Turn Ramp 3", "Turn Ramp 4", "Pool Drop Ramp" };
    private static readonly string[] ParapetNames = Enumerable.Range(1, 4).SelectMany(i => new[] { $"Turn Parapet {i} -1", $"Turn Parapet {i} 1" }).ToArray();
    private static readonly List<string> Report = new();

    [Serializable] public class Float3 { public float x, y, z; public Vector3 Vector => new(x, y, z); }
    [Serializable] public class Float4 { public float x, y, z, w; public Quaternion Quaternion => new(x, y, z, w); }
    [Serializable] public class LayoutItem
    {
        public string name;
        public Float3 center, size;
        public Float4 rotation;
        public bool anchor;
        public Float3[] corners;
    }
    [Serializable] public class LayoutData { public LayoutItem[] items; }
    private struct Triangle { public Vector3 A, B, C; }
    private static readonly int[][] BoxFaces =
    {
        new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
        new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }
    };

    private static string Absolute(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

    public static void Build()
    {
        try
        {
            Report.Clear();
            Report.Add("C-AqueductWalk isolated Unity validation");
            Report.Add("Unity=" + Application.unityVersion);
            Report.Add("Project=" + Directory.GetParent(Application.dataPath).FullName);
            if (Application.unityVersion != "6000.3.23f1")
                throw new InvalidOperationException("Unexpected Unity version: " + Application.unityVersion);
            Directory.CreateDirectory(Absolute(Folder + "/Materials"));
            Directory.CreateDirectory(Absolute(Folder + "/Prefabs"));
            Directory.CreateDirectory(Absolute(Folder + "/Preview"));
            Directory.CreateDirectory(Absolute(Folder + "/Preview/Materials"));
            Directory.CreateDirectory(Absolute(Folder + "/Documentation"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var materials = new Dictionary<string, Material>
            {
                ["MV_Ivory"] = Material("MV_Ivory", new Color(0.95f, 0.88f, 0.77f)),
                ["MV_IvoryShade"] = Material("MV_IvoryShade", new Color(0.86f, 0.76f, 0.70f)),
                ["MV_Lilac"] = Material("MV_Lilac", new Color(0.50f, 0.45f, 0.66f)),
                ["MV_Apricot"] = Material("MV_Apricot", new Color(0.90f, 0.58f, 0.37f)),
            };
            Material water = Material("MV_Water", new Color(0.45f, 0.80f, 0.84f));
            Material gold = Material("MV_Gold", new Color(0.96f, 0.70f, 0.32f));
            Material mint = Material("MV_Mint", new Color(0.36f, 0.70f, 0.55f));
            AssetDatabase.SaveAssets();
            ConfigureImporter(materials);

            LayoutData layout = JsonUtility.FromJson<LayoutData>(File.ReadAllText(Absolute("ArtSource/Santorini Layout/Santorini Collision Layout.json")));
            Dictionary<string, LayoutItem> items = layout.items.ToDictionary(item => item.name);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (!model)
                throw new InvalidOperationException("Missing imported FBX root.");

            var wrapper = new GameObject("C-AqueductWalk");
            SceneManager.MoveGameObjectToScene(wrapper, scene);
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            imported.name = "C-AqueductWalk_Imported";
            imported.transform.localPosition = Vector3.zero;
            imported.transform.localRotation = model.transform.localRotation;
            imported.transform.localScale = model.transform.localScale;
            imported.transform.SetParent(wrapper.transform, false);
            wrapper.transform.SetPositionAndRotation(Anchor, Quaternion.identity);
            ValidateModule(wrapper, items, materials);
            wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
            UnityEngine.Object.DestroyImmediate(wrapper);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab || prefab.transform.localPosition != Vector3.zero || prefab.transform.localRotation != Quaternion.identity || prefab.transform.localScale != Vector3.one)
                throw new InvalidOperationException("Prefab outer root is not identity at its local anchor.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Anchor, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var reference = new GameObject("Reference Blockout - toggle core boxes for comparison");
            SceneManager.MoveGameObjectToScene(reference, scene);
            BuildReference(reference.transform, layout, materials["MV_Ivory"], water, gold, mint);
            ConfigureLighting();
            Camera formal = Camera("Formal Camera", new Vector3(-0.05f, 0.95f, 0.25f) + new Vector3(12, 15, -15),
                new Vector3(-0.05f, 0.95f, 0.25f), 9.5f);
            Camera reverse = Camera("Reverse From Goal Camera", new Vector3(5, 1.5f, 5.2f), new Vector3(-3.70f, 2.15f, -2.40f), 3.8f);
            reverse.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Render(formal, Folder + "/Preview/C-AqueductWalk_Formal.png");
            List<MeshRenderer> hiddenForReverse = IsolateReverseContext(reference.transform);
            Light keyLight = RenderSettings.sun;
            LightShadows savedShadows = keyLight.shadows;
            try
            {
                // Shadow-free inspection prevents thin parapet shadow acne from
                // obscuring the arch/collision comparison in this reverse view.
                keyLight.shadows = LightShadows.None;
                Render(reverse, Folder + "/Preview/C-AqueductWalk_ReverseFromGoal.png");
            }
            finally
            {
                keyLight.shadows = savedShadows;
                foreach (MeshRenderer renderer in hiddenForReverse)
                    renderer.enabled = true;
            }
            formal.enabled = true;
            reverse.enabled = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectionReport(formal, "Formal");
            ProjectionReport(reverse, "Reverse");
            Report.Add("Screenshots=1920x1080; formal camera size=9.5; reverse position=(5,1.5,5.2), size=3.8, target=(-3.70,2.15,-2.40)");
            Report.Add("Reverse image temporarily hides unrelated pool/course reference proxies and uses shadow-free inspection lighting; module, tower, launch, ball and foundation remain. Scene defaults restore full context and soft shadows.");
            Report.Add("Formal scene and generator were not opened or changed by this isolated QA.");
            Report.Add("Final integration and gameplay validation remain task I.");
            Report.Add("RESULT=PASS");
            WriteReport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("C_AQUEDUCT_WALK_QA_PASS\n" + string.Join("\n", Report));
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Report.Add("RESULT=FAIL");
            Report.Add(exception.ToString());
            WriteReport();
            Debug.LogError("C_AQUEDUCT_WALK_QA_FAIL\n" + exception);
            EditorApplication.Exit(1);
        }
    }

    private static Material Material(string name, Color srgb)
    {
        bool previewOnly = name == "MV_Water" || name == "MV_Gold" || name == "MV_Mint";
        string path = $"{(previewOnly ? Folder + "/Preview/Materials" : Folder + "/Materials")}/{name}.mat";
        string legacyPath = $"{Folder}/Materials/{name}.mat";
        if (previewOnly && AssetDatabase.LoadAssetAtPath<Material>(legacyPath))
            AssetDatabase.DeleteAsset(legacyPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        // Match the project's established MV material convention: serialized
        // color channels are the specified sRGB palette values.
        material.SetColor("_BaseColor", srgb);
        material.SetColor("_Color", srgb);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureImporter(Dictionary<string, Material> materials)
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (!importer)
            throw new FileNotFoundException("FBX importer unavailable", FbxPath);
        importer.globalScale = 1f;
        importer.useFileUnits = true;
        importer.useFileScale = true;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = true;
        importer.generateSecondaryUV = false;
        foreach (var pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        string generated = Folder + "/FBX/Materials";
        if (AssetDatabase.IsValidFolder(generated))
            AssetDatabase.DeleteAsset(generated);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        importer.SaveAndReimport();
        foreach (var renderer in AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath).GetComponentsInChildren<MeshRenderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (!material || !materials.Values.Contains(material))
                    throw new InvalidOperationException("Unresolved or duplicate imported material on " + renderer.name + ": " + material);
    }

    private static void ValidateModule(GameObject root, Dictionary<string, LayoutItem> items, Dictionary<string, Material> materials)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 4)
            throw new InvalidOperationException($"Expected 4 mesh parts, got {filters.Length}.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
            root.GetComponentsInChildren<Animator>(true).Length != 0 || root.GetComponentsInChildren<Animation>(true).Length != 0 ||
            root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("The module must contain no colliders, physics, animation, or scripts.");
        int triangles = 0;
        Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var verticesByPart = new Dictionary<string, Vector3[]>();
        var trianglesByPart = new Dictionary<string, Triangle[]>();
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (!mesh || !mesh.isReadable)
                throw new InvalidOperationException("Unreadable or missing mesh: " + filter.name);
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException("Missing metric UV0: " + filter.name);
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh.subMeshCount > 4 || renderer.sharedMaterials.Length > 4)
                throw new InvalidOperationException("More than four material slots on " + filter.name);
            if (renderer.sharedMaterials.Any(material => !material || !materials.Values.Contains(material)))
                throw new InvalidOperationException("Material object reference mismatch on " + filter.name);
            int count = mesh.triangles.Length / 3;
            triangles += count;
            Report.Add($"{filter.name}: triangles={count}; materialSlots={renderer.sharedMaterials.Length}; UV0=OK");
            Vector3[] vertices = mesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            verticesByPart[filter.name] = vertices;
            foreach (Vector3 vertex in vertices)
            {
                minimum = Vector3.Min(minimum, vertex);
                maximum = Vector3.Max(maximum, vertex);
            }
            int[] indices = mesh.triangles;
            trianglesByPart[filter.name] = Enumerable.Range(0, indices.Length / 3).Select(i => new Triangle
            {
                A = vertices[indices[i * 3]],
                B = vertices[indices[i * 3 + 1]],
                C = vertices[indices[i * 3 + 2]]
            }).ToArray();
        }
        if (triangles > 15000)
            throw new InvalidOperationException("Triangle budget exceeded: " + triangles);
        if (Mathf.Abs(minimum.y + 0.22f) > 0.0002f)
            throw new InvalidOperationException("Column bases do not reach Y=-0.22: " + minimum.y);
        if (maximum.z > 0.025f)
            throw new InvalidOperationException("Geometry intrudes into the pool flight zone, maxZ=" + maximum.z);
        Report.Add($"Total triangles={triangles}; world bounds min={minimum}, max={maximum}; colliders/scripts=0");

        string deckKey = filters.Single(filter => filter.name.Contains("Deck")).name;
        string parapetKey = filters.Single(filter => filter.name.Contains("Parapet")).name;
        float deckError = CornerError(trianglesByPart[deckKey], items, DeckNames, new[] { 2, 3, 6, 7 });
        float parapetError = CornerError(trianglesByPart[parapetKey], items, ParapetNames, Enumerable.Range(0, 8).ToArray());
        Triangle[] collisionSurface = DeckNames.SelectMany(name => CollisionTriangles(items[name])).ToArray();
        float deckSurfaceError = verticesByPart[deckKey].Max(point => Mathf.Sqrt(collisionSurface.Min(triangle =>
            (ClosestPoint(point, triangle.A, triangle.B, triangle.C) - point).sqrMagnitude)));
        if (deckError > 0.02f || parapetError > 0.02f || deckSurfaceError > 0.02f)
            throw new InvalidOperationException($"Collision alignment failed: deck={deckError}, parapet={parapetError}, externalDeck={deckSurfaceError}");
        Report.Add($"Alignment: maximum deck-top corner error={deckError:F8} m; maximum external deck vertex-to-collision error={deckSurfaceError:F8} m; maximum parapet corner error={parapetError:F8} m; tolerance=0.02 m");

        string stairKey = filters.Single(filter => filter.name.Contains("Donkey_Stairs")).name;
        Vector3[] stairSamples = SampleTriangles(verticesByPart[stairKey], trianglesByPart[stairKey]).ToArray();
        string[] stairIntrusions = stairSamples.Where(point => DeckNames.Any(name => InsidePrism(items[name], point, 0.001f)))
            .Select(point => point.ToString("F6")).Take(12).ToArray();
        if (stairIntrusions.Length != 0)
            throw new InvalidOperationException("Decorative stair collision intrusions: " + string.Join("; ", stairIntrusions));
        float stairGap = verticesByPart[stairKey].Min(point => DeckNames.Min(name =>
        {
            RoadClearance(items[name], point, out float distance);
            return distance;
        }));
        if (stairGap < 0.024f)
            throw new InvalidOperationException("Decorative stair band is too close to collision: " + stairGap);
        Report.Add($"Decorative stair collision intrusion samples=0; measured minimum horizontal gap={stairGap:F6} m; band width=0.50 m; highest tread is 0.12 m below road");

        int clearanceViolations = 0;
        int clearanceSamples = 0;
        foreach (var pair in verticesByPart.Where(pair => pair.Key.Contains("Arcade") || pair.Key.Contains("Donkey_Stairs")))
            foreach (Vector3 point in SampleTriangles(pair.Value, trianglesByPart[pair.Key]))
            {
                clearanceSamples++;
                if (DeckNames.Any(name =>
                {
                    float height = RoadClearance(items[name], point, out float lateral);
                    return lateral <= 0.60f && height > 0.02f && height < 1.30f;
                }))
                    clearanceViolations++;
            }
        if (clearanceViolations != 0)
            throw new InvalidOperationException("Non-parapet road-clearance violations: " + clearanceViolations);
        Report.Add($"Road clearance: {clearanceSamples} arcade/stair vertex/edge/centroid samples, 0 added-geometry intrusions in road+0.60 m side band and road+0.02..1.30 m height band; aligned road deck and required collision-matched parapet/posts excluded");
        Report.Add($"Pool flight clearance: maximum art Z={maximum.z:F8}; target=(-1.40,0.45,2.05); last support Z=-0.25; last decorative step Z=-0.28");
    }

    private static float CornerError(Triangle[] triangles, Dictionary<string, LayoutItem> items, string[] names, int[] indices)
    {
        float maximum = 0;
        foreach (string name in names)
            foreach (int index in indices)
            {
                Vector3 point = items[name].corners[index].Vector;
                float distance = Mathf.Sqrt(triangles.Min(triangle => (ClosestPoint(point, triangle.A, triangle.B, triangle.C) - point).sqrMagnitude));
                maximum = Mathf.Max(maximum, distance);
            }
        return maximum;
    }

    private static IEnumerable<Triangle> CollisionTriangles(LayoutItem item)
    {
        Float3[] c = item.corners;
        Vector3[] vertices = { c[0].Vector, c[1].Vector, c[5].Vector, c[4].Vector, c[2].Vector, c[3].Vector, c[7].Vector, c[6].Vector };
        foreach (int[] face in BoxFaces)
        {
            yield return new Triangle { A = vertices[face[0]], B = vertices[face[1]], C = vertices[face[2]] };
            yield return new Triangle { A = vertices[face[0]], B = vertices[face[2]], C = vertices[face[3]] };
        }
    }

    private static Vector3 ClosestPoint(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = point - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return a;
        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
        float denominator = 1f / (va + vb + vc);
        return a + ab * (vb * denominator) + ac * (vc * denominator);
    }

    private static bool InsidePrism(LayoutItem item, Vector3 point, float margin)
    {
        Float3[] c = item.corners;
        Vector3[] vertices = { c[0].Vector, c[1].Vector, c[5].Vector, c[4].Vector, c[2].Vector, c[3].Vector, c[7].Vector, c[6].Vector };
        Vector3 center = vertices.Aggregate(Vector3.zero, (sum, value) => sum + value) / 8f;
        foreach (int[] face in BoxFaces)
        {
            Vector3 a = vertices[face[0]], b = vertices[face[1]], d = vertices[face[2]];
            Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
            Vector3 faceCenter = face.Select(index => vertices[index]).Aggregate(Vector3.zero, (sum, value) => sum + value) / face.Length;
            if (Vector3.Dot(center - faceCenter, normal) > 0)
                normal = -normal;
            if (Vector3.Dot(point - faceCenter, normal) > margin)
                return false;
        }
        return true;
    }

    private static IEnumerable<Vector3> SampleTriangles(Vector3[] vertices, Triangle[] triangles)
    {
        foreach (Vector3 vertex in vertices)
            yield return vertex;
        foreach (Triangle triangle in triangles)
        {
            yield return (triangle.A + triangle.B + triangle.C) / 3f;
            yield return (triangle.A + triangle.B) * 0.5f;
            yield return (triangle.B + triangle.C) * 0.5f;
            yield return (triangle.C + triangle.A) * 0.5f;
        }
    }

    // Return height above the collision top plane and horizontal distance to its
    // rectangular footprint. Vertical projection is important on sloped ramps.
    private static float RoadClearance(LayoutItem item, Vector3 point, out float horizontalDistance)
    {
        Quaternion rotation = item.rotation.Quaternion;
        Vector3 normal = rotation * Vector3.up;
        Vector3 center = item.center.Vector;
        Vector3 topCenter = center + normal * (item.size.y * 0.5f);
        float lift = Vector3.Dot(topCenter - point, normal) / normal.y;
        Vector3 onRoad = point + Vector3.up * lift;
        Vector3 local = Quaternion.Inverse(rotation) * (onRoad - center);
        float x = Mathf.Clamp(local.x, -item.size.x * 0.5f, item.size.x * 0.5f);
        float z = Mathf.Clamp(local.z, -item.size.z * 0.5f, item.size.z * 0.5f);
        Vector3 closest = center + rotation * new Vector3(x, item.size.y * 0.5f, z);
        horizontalDistance = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(closest.x, closest.z));
        return -lift;
    }

    private static void BuildReference(Transform parent, LayoutData layout, Material white, Material water, Material gold, Material mint)
    {
        HashSet<string> core = new(DeckNames.Concat(ParapetNames));
        foreach (LayoutItem item in layout.items)
        {
            if (item.anchor || item.corners.Length == 0)
                continue;
            Material material = item.name == "Elastic Pool Surface" ? water : (item.name.Contains("Pressure Plate") || item.name.Contains("Lift Wall") ? mint : white);
            GameObject box = Box(parent, item.name, item.center.Vector, item.size.Vector, item.rotation.Quaternion, material);
            if (core.Contains(item.name))
                box.GetComponent<MeshRenderer>().enabled = false;
        }
        // Reference-only tower/foundation masses from the existing white-model dimensions.
        // They are not part of the module and have no colliders.
        Box(parent, "Reference Floating Foundation", new Vector3(0, -0.58f, 0.05f), new Vector3(12.15f, 0.72f, 9.65f), Quaternion.identity, white);
        Box(parent, "Reference Tower Foot", new Vector3(-5.64f, 0.10f, -3.65f), new Vector3(2.17f, 0.45f, 2.20f), Quaternion.identity, white);
        Box(parent, "Reference White Tower", new Vector3(-5.64f, 2.27f, -3.65f), new Vector3(1.77f, 4.02f, 1.79f), Quaternion.identity, white);
        Box(parent, "Reference Start Rail", new Vector3(-5.72f, 4.55f, -4.56f), new Vector3(1.85f, 0.20f, 0.17f), Quaternion.identity, white);
        Box(parent, "Reference Pool Rim Left", new Vector3(-3.095f, 0.53f, 2.05f), new Vector3(0.12f, 0.32f, 3.70f), Quaternion.identity, white);
        Box(parent, "Reference Pool Rim Right", new Vector3(0.295f, 0.53f, 2.05f), new Vector3(0.12f, 0.32f, 3.70f), Quaternion.identity, white);
        Box(parent, "Reference Pool Rim Near", new Vector3(-1.4f, 0.53f, 0.18f), new Vector3(3.50f, 0.32f, 0.12f), Quaternion.identity, white);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.name = "Reference Ball - diameter 0.76 m";
        sphere.transform.SetParent(parent, false);
        sphere.transform.position = new Vector3(-5.9f, 4.83f, -3.65f);
        sphere.transform.localScale = Vector3.one * 0.76f;
        sphere.GetComponent<MeshRenderer>().sharedMaterial = gold;
    }

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.SetPositionAndRotation(center, rotation);
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        return box;
    }

    private static void ConfigureLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.56f, 0.60f, 0.68f);
        RenderSettings.fog = false;
        var lightObject = new GameObject("Soft Key Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1.0f, 0.91f, 0.81f);
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
        light.shadowBias = 0.10f;
        light.shadowNormalBias = 0.60f;
        light.shadowNearPlane = 0.10f;
        lightObject.transform.rotation = Quaternion.Euler(42, -32, 0);
        RenderSettings.sun = light;
        QualitySettings.antiAliasing = 8;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 70;
    }

    private static List<MeshRenderer> IsolateReverseContext(Transform reference)
    {
        var hidden = new List<MeshRenderer>();
        foreach (MeshRenderer renderer in reference.GetComponentsInChildren<MeshRenderer>(true))
        {
            string name = renderer.name;
            bool keep = name == "Launch Coping" || name == "Reference Floating Foundation" ||
                        name == "Reference Tower Foot" || name == "Reference White Tower" ||
                        name == "Reference Start Rail" || name.StartsWith("Reference Ball");
            if (!keep && renderer.enabled)
            {
                renderer.enabled = false;
                hidden.Add(renderer);
            }
        }
        return hidden;
    }

    private static Camera Camera(string name, Vector3 position, Vector3 target, float size)
    {
        var cameraObject = new GameObject(name);
        cameraObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.aspect = 16f / 9f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.71f, 0.82f, 0.87f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 150;
        camera.allowHDR = false;
        return camera;
    }

    private static void Render(Camera camera, string path)
    {
        const int width = 1920, height = 1080;
        bool previousEnabled = camera.enabled;
        float previousAspect = camera.aspect;
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        bool previousAsync = ShaderUtil.allowAsyncCompilation;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.enabled = true;
            camera.aspect = 16f / 9f;
            ShaderUtil.allowAsyncCompilation = false;
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
                RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                camera.targetTexture = target;
                camera.Render();
            }
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Absolute(path), image.EncodeToPNG());
        }
        finally
        {
            camera.enabled = previousEnabled;
            camera.aspect = previousAspect;
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            ShaderUtil.allowAsyncCompilation = previousAsync;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
        var check = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!check.LoadImage(File.ReadAllBytes(Absolute(path))) || check.width != width || check.height != height)
            throw new InvalidOperationException("Rendered PNG is not 1920x1080: " + path);
        UnityEngine.Object.DestroyImmediate(check);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        Report.Add("Rendered=" + path + " (1920x1080)");
    }

    private static void ProjectionReport(Camera camera, string label)
    {
        foreach (Vector3 point in new[] { new Vector3(-4.5f, 4.44f, -3.65f), new Vector3(-3.2f, 4.27f, -2.1f), new Vector3(-1.65f, 2.52f, 0), new Vector3(-1.4f, 0.45f, 2.05f) })
        {
            Vector3 projected = camera.WorldToViewportPoint(point);
            Report.Add($"{label} viewport {point} -> {projected}");
        }
    }

    private static void WriteReport()
    {
        Directory.CreateDirectory(Absolute(Folder + "/Documentation"));
        File.WriteAllLines(Absolute(Folder + "/Documentation/C-AqueductWalk_Unity_Validation.txt"), Report, Encoding.UTF8);
    }
}
