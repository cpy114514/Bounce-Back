using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BounceBack.GeometryWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Compiled only inside .utmp/B-ReleaseTower-QA/Assets/BReleaseTowerQA.
// The source copy remains in ArtSource/Prototypes/B-ReleaseTower for reproducibility.
public static class BReleaseTowerPreview
{
    private const string Folder = "Assets/Scenes/Archive/BounceBack/Art/B-ReleaseTower";
    private const string LayoutPath = "ArtSource/Santorini Layout/Santorini Collision Layout.json";
    private const string PreviewScenePath = Folder + "/Preview/BReleaseTower_Preview.unity";
    private const int QueryLayer = 30;
    private static readonly Vector3 Anchor = new(-5.64f,-0.22f,-3.65f);
    private static readonly List<string> Report = new();

    [Serializable] private class LayoutFile { public LayoutItem[] items; }
    [Serializable] private class LayoutItem
    {
        public string name;
        public Vector3 center;
        public Vector3 size;
        public Quaternion rotation;
        public bool anchor;
        public Vector3[] corners;
    }

    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/Materials/{name}.mat");

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rotation, Material material, bool visible = true)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = center;
        obj.transform.rotation = rotation;
        obj.transform.localScale = size;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        obj.GetComponent<MeshRenderer>().enabled = visible;
        return obj;
    }

    private static void BuildReference(Scene scene, out float highestReference)
    {
        var file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(LayoutPath));
        if (file?.items == null || file.items.Length != 27) throw new InvalidOperationException("Expected the authoritative 27-item layout");
        var root = new GameObject("Reference White Layout - 27 JSON items");
        SceneManager.MoveGameObjectToScene(root, scene);
        highestReference = float.NegativeInfinity;
        foreach (var item in file.items)
        {
            if (item.anchor)
            {
                var marker = new GameObject(item.name + " Anchor");
                SceneManager.MoveGameObjectToScene(marker, scene);
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = item.center;
                marker.transform.rotation = item.rotation;
                continue;
            }
            var reference = Box(item.name, root.transform, item.center, item.size, item.rotation, Mat("MV_IvoryShade"), item.name != "Launch Coping");
            if (item.name == "Launch Coping") reference.name = "Launch Coping Reference - replaced by BReleaseTower";
            if (item.corners != null && item.corners.Length > 0) highestReference = Mathf.Max(highestReference, item.corners.Max(c => c.y));
            else highestReference = Mathf.Max(highestReference, item.center.y + item.size.y * 0.5f);
        }
        Box("Tower Body Reference - replaced by BReleaseTower", root.transform,
            new Vector3(-5.64f,2.27f,-3.65f),new Vector3(1.77f,4.02f,1.79f),Quaternion.identity,Mat("MV_IvoryShade"),false);
        var context = new GameObject("Preview-only foundation context");
        SceneManager.MoveGameObjectToScene(context, scene);
        Box("Terrace Foundation",context.transform,new Vector3(-0.01f,-0.58f,0.55f),new Vector3(12.15f,0.72f,9.65f),Quaternion.identity,Mat("MV_IvoryShade"));
        Box("Tower Foot Reference",context.transform,new Vector3(-5.64f,0.02f,-3.65f),new Vector3(2.17f,0.48f,2.20f),Quaternion.identity,Mat("MV_IvoryShade"),false);
        highestReference = Mathf.Max(highestReference, -0.22f);
        Report.Add($"Reference layout items: {file.items.Length}; hidden replaced references: Launch Coping + supplied tower body; highest reference Y={highestReference:F3}");
    }

    private static void BuildPistonAndBall(Scene scene)
    {
        string path = "Assets/GeometryWorldLibrary/Prefabs/Starter/Gadgets/Spawn.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new FileNotFoundException("Starter Spawn prefab not found", path);
        var spawn = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        spawn.name = "Reference Ball Start and Existing Piston";
        spawn.transform.position = new Vector3(-5.90f,4.83f,-3.65f);
        spawn.transform.rotation = Quaternion.LookRotation(Vector3.right,Vector3.up);
        var ballSpawn = spawn.GetComponent<BallSpawn>();
        var piston = spawn.GetComponent<PistonLauncher>();
        ballSpawn.diameter = 0.76f;
        piston.stroke = 0.28f;
        // Apply the existing editable piston art if this source prefab still carries primitive meshes.
        var parts = new[]
        {
            (part:piston.housing,name:"Arch Housing"),
            (part:piston.head.Find("Push Plate"),name:"Octagonal Push Plate"),
            (part:piston.head.Find("Rod"),name:"Fluted Rod")
        };
        string artFolder = "Assets/GeometryWorldLibrary/Prefabs/Starter/Art/Piston";
        string materialFolder = "Assets/GeometryWorldLibrary/Prefabs/Starter/Materials";
        var materials = new[] { "Ivory","Apricot","Rose","Mint","Lilac" }.Select(name => AssetDatabase.LoadAssetAtPath<Material>($"{materialFolder}/{name}.mat")).ToArray();
        foreach (var item in parts)
        {
            if (!item.part) continue;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{artFolder}/{item.name}.asset");
            if (mesh && item.part.TryGetComponent(out MeshFilter filter)) filter.sharedMesh = mesh;
            if (item.part.TryGetComponent(out MeshRenderer renderer) && materials.All(m => m)) renderer.sharedMaterials = materials;
            item.part.localScale = Vector3.one;
        }
        piston.AlignVisuals();
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Reference Gold Ball - diameter 0.76";
        SceneManager.MoveGameObjectToScene(ball, scene);
        ball.transform.position = new Vector3(-5.90f,4.83f,-3.65f);
        ball.transform.localScale = Vector3.one * 0.76f;
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.GetComponent<MeshRenderer>().sharedMaterial = Mat("MV_Gold");
        Report.Add($"Piston: source Spawn prefab; world rotation={spawn.transform.eulerAngles}; stroke=0.28; housing={piston.housing.position}; head={piston.head.position}");
        Report.Add("Ball reference centre=(-5.90,4.83,-3.65), diameter=0.76, material=MV_Gold");
    }

    private static Camera CreateCamera(Scene scene, string name, Vector3 position, Vector3 target, bool orthographic, float sizeOrFov)
    {
        var obj = new GameObject(name);
        SceneManager.MoveGameObjectToScene(obj, scene);
        obj.transform.position = position;
        obj.transform.LookAt(target);
        var camera = obj.AddComponent<Camera>();
        camera.orthographic = orthographic;
        camera.orthographicSize = sizeOrFov;
        camera.fieldOfView = sizeOrFov;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.26f,0.31f,0.43f);
        camera.allowMSAA = true;
        camera.useOcclusionCulling = false;
        camera.aspect = 16f/9f;
        return camera;
    }

    private static void BuildLighting(Scene scene)
    {
        var keyObject = new GameObject("Warm Soft Key");
        SceneManager.MoveGameObjectToScene(keyObject, scene);
        keyObject.transform.rotation = Quaternion.Euler(38f,-32f,0f);
        var key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.color = new Color(1f,0.92f,0.82f);
        key.intensity = 1.35f;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.66f;
        key.shadowBias = 0.04f;
        key.shadowNormalBias = 0.35f;
        var fillObject = new GameObject("Cool Soft Fill");
        SceneManager.MoveGameObjectToScene(fillObject, scene);
        fillObject.transform.rotation = Quaternion.Euler(58f,142f,0f);
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.66f,0.78f,1f);
        fill.intensity = 0.30f;
        fill.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f,0.64f,0.76f);
        RenderSettings.ambientIntensity = 0.65f;
        RenderSettings.sun = key;
        QualitySettings.shadowDistance = 80f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = 2;
        QualitySettings.antiAliasing = 4;
    }

    private static Bounds MeshBounds(MeshFilter filter)
    {
        var vertices = filter.sharedMesh.vertices;
        var bounds = new Bounds(filter.transform.TransformPoint(vertices[0]),Vector3.zero);
        foreach (var vertex in vertices) bounds.Encapsulate(filter.transform.TransformPoint(vertex));
        return bounds;
    }

    private static float BoundsError(Bounds actual, Bounds expected)
    {
        return Mathf.Max(Mathf.Abs(actual.min.x-expected.min.x),Mathf.Abs(actual.min.y-expected.min.y),Mathf.Abs(actual.min.z-expected.min.z),
            Mathf.Abs(actual.max.x-expected.max.x),Mathf.Abs(actual.max.y-expected.max.y),Mathf.Abs(actual.max.z-expected.max.z));
    }

    private static void Validate(GameObject tower, Camera formal, float highestReference)
    {
        var filters = tower.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).ToArray();
        var core = filters.Single(f => f.sharedMesh.name.StartsWith("Body_Collision_Aligned_Core"));
        var slab = filters.Single(f => f.sharedMesh.name.StartsWith("Launch_Coping_Aligned"));
        var bell = filters.Single(f => f.sharedMesh.name.StartsWith("Bell_Pavilion_Dome_And_Flag"));
        var rails = filters.Single(f => f.sharedMesh.name.StartsWith("Platform_Broken_Railings"));
        float coreError = BoundsError(MeshBounds(core),new Bounds(new Vector3(-5.64f,2.27f,-3.65f),new Vector3(1.77f,4.02f,1.79f)));
        float slabError = BoundsError(MeshBounds(slab),new Bounds(new Vector3(-5.91f,4.27f,-3.65f),new Vector3(2.83f,0.36f,1.92f)));
        if (coreError > 0.02f || slabError > 0.02f) throw new InvalidOperationException("Collision/art alignment exceeds 0.02 m");
        var bellBounds = MeshBounds(bell);
        var railBounds = MeshBounds(rails);
        if (bellBounds.max.y < 7.5f || bellBounds.max.y > 8.5f) throw new InvalidOperationException("Bell top outside Y=7.5..8.5");
        if (railBounds.max.y-4.45f > 0.35f) throw new InvalidOperationException("Rail above 0.35 m");
        if (bellBounds.max.y <= highestReference) throw new InvalidOperationException("Tower is not the highest formal-view element");
        Report.Add($"Tower core bounds error={coreError:F6} m; Launch Coping bounds error={slabError:F6} m; allowed<=0.02");
        Report.Add($"Bell bounds min={bellBounds.min}, max={bellBounds.max}; rail height={railBounds.max.y-4.45f:F3} m; tower/reference height margin={bellBounds.max.y-highestReference:F3} m");
        if (tower.GetComponentsInChildren<Collider>(true).Length != 0 || tower.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            throw new InvalidOperationException("Tower must remain visual-only");

        var temporary = new List<Collider>();
        foreach (var filter in filters)
        {
            filter.gameObject.layer = QueryLayer;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            temporary.Add(collider);
        }
        Physics.SyncTransforms();
        int mask = 1 << QueryLayer;
        try
        {
            // Start just above the supporting slab to avoid counting intentional surface contact.
            var pistonHits = Physics.OverlapBox(new Vector3(-6.60f,5.4251f,-3.65f),new Vector3(0.60f,0.9749f,0.35f),Quaternion.identity,mask,QueryTriggerInteraction.Ignore);
            var routeHits = Physics.OverlapBox(new Vector3(-5.345f,5.1004f,-3.65f),new Vector3(0.855f,0.6494f,0.60f),Quaternion.identity,mask,QueryTriggerInteraction.Ignore);
            Report.Add($"Piston exclusion overlap: {pistonHits.Length} colliders (X[-7.2,-6.0], Z[-4.0,-3.3], Y>4.45)");
            Report.Add($"Ball-route exclusion overlap: {routeHits.Length} colliders (X[-6.2,-4.49], Z[-4.25,-3.05], Y=4.45..5.75)");
            if (pistonHits.Length != 0 || routeHits.Length != 0)
                throw new InvalidOperationException("Clearance failure: " + string.Join(",",pistonHits.Concat(routeHits).Select(h => h.name).Distinct()));

            var spawn = GameObject.Find("Reference Ball Start and Existing Piston");
            var piston = spawn.GetComponent<PistonLauncher>();
            var targets = new List<Vector3>
            {
                new(-5.90f,4.83f,-3.65f),new(-5.90f,5.21f,-3.65f),
                piston.housing.position,piston.head.position,
                new(-4.80f,4.45f,-3.65f),new(-3.90f,4.43f,-3.65f)
            };
            int blocked = 0;
            foreach (var target in targets)
            {
                Vector3 delta = target-formal.transform.position;
                bool hit = Physics.Raycast(formal.transform.position,delta.normalized,out RaycastHit ray,delta.magnitude+0.02f,mask,QueryTriggerInteraction.Ignore);
                bool occluded = hit && ray.distance < delta.magnitude-0.04f;
                Report.Add($"Formal sightline target={target}: {(occluded ? "BLOCKED by "+ray.collider.name : "clear")}");
                if (occluded) blocked++;
            }
            if (blocked != 0) throw new InvalidOperationException($"Tower blocks {blocked} required formal-view targets");
        }
        finally
        {
            foreach (var collider in temporary) Object.DestroyImmediate(collider);
        }
        Report.Add("Temporary query colliders removed; delivered tower colliders=0; scripts=0");
    }

    private static void Render(Camera camera, string filename)
    {
        var texture = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        bool wasEnabled = camera.enabled;
        try
        {
            camera.enabled = true;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);
            image.Apply();
            File.WriteAllBytes($"{Folder}/Preview/{filename}",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.enabled = wasEnabled;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
        }
        var info = new FileInfo($"{Folder}/Preview/{filename}");
        if (!info.Exists || info.Length < 10000) throw new InvalidOperationException("Missing or empty render " + filename);
        Report.Add($"Rendered {filename}: 1920x1080; bytes={info.Length}");
    }

    public static void Build()
    {
        Report.Clear();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        BuildReference(scene,out float highestReference);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/BReleaseTower.prefab");
        if (!prefab) throw new FileNotFoundException("Combined tower prefab missing");
        var tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        tower.name = "B-ReleaseTower @ authoritative anchor";
        tower.transform.position = Anchor;
        tower.transform.rotation = Quaternion.identity;
        tower.transform.localScale = Vector3.one;
        BuildPistonAndBall(scene);
        BuildLighting(scene);
        var formal = CreateCamera(scene,"Formal Camera - required",new Vector3(11.95f,15.95f,-14.75f),new Vector3(-0.05f,0.95f,0.25f),true,9.5f);
        var reverse = CreateCamera(scene,"Reverse Camera - from goal",new Vector3(5f,1.5f,5.2f),new Vector3(-5.90f,3.60f,-3.65f),false,45f);
        reverse.enabled = false;
        Validate(tower,formal,highestReference);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene,PreviewScenePath);
        Render(formal,"BReleaseTower_Formal_1920x1080.png");
        Render(reverse,"BReleaseTower_ReverseFromGoal_1920x1080.png");
        EditorSceneManager.SaveScene(scene,PreviewScenePath);
        int total = tower.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length/3);
        Report.Add($"Total tower triangles: {total}; per-module limits already validated in the main project");
        Report.Add("RESULT=PASS");
        File.WriteAllText(Folder+"/Documentation/BReleaseTower_QA_Report.txt",string.Join("\n",Report)+"\n");
        Debug.Log("B_RELEASE_TOWER_PREVIEW_PASS\n"+string.Join("\n",Report));
    }
}
