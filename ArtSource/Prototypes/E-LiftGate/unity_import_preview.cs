// Evaluate with:
// unity command script eval --project-path "<project>" --file "ArtSource/Prototypes/E-LiftGate/unity_import_preview.cs" --timeout 300
// Builds visual prefabs and a preview scene. Does not open or save Santorini Gameplay.
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

const string Root = "Assets/Scenes/Archive/BounceBack/Art/E-LiftGate";
const string FbxDir = Root + "/FBX";
const string MaterialDir = Root + "/Materials";
const string PrefabDir = Root + "/Prefabs";
const string PreviewDir = Root + "/Preview";
const string DocDir = Root + "/Documentation";
const int PreviewLayer = 31;

Vector3 wallCenter = new(-1.55f, 2.15f, 3.95f);
Vector3 frameAnchor = new(-1.55f, -0.22f, 3.95f);
Vector3 orientationOffset = new(0f, 2.37f, 0f);
Quaternion wallRotation = new(0.0124137737f, 0.9237968922f, -0.0300598480f, 0.3814991415f);
Quaternion yawRotation = new(0f, 0.9242858319f, 0f, 0.3817010624f);
Vector3 halfExtents = new(1.30f, 0.625f, 0.145f);

Directory.CreateDirectory(MaterialDir);
Directory.CreateDirectory(PrefabDir);
Directory.CreateDirectory(PreviewDir);
Directory.CreateDirectory(DocDir);
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

Shader shader = Shader.Find("Universal Render Pipeline/Lit");
if (!shader)
    throw new InvalidOperationException("URP/Lit shader is required.");

Color Srgb(float r, float g, float b) => new Color(r, g, b, 1f).linear;

Material MakeMaterial(string name, Color color, float smoothness)
{
    string path = MaterialDir + "/" + name + ".mat";
    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (!material)
    {
        material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
    }
    material.shader = shader;
    material.color = color;
    material.SetFloat("_Smoothness", smoothness);
    material.SetFloat("_Metallic", 0f);
    EditorUtility.SetDirty(material);
    return material;
}

Material ivory = MakeMaterial("MV_Ivory", Srgb(0.95f, 0.88f, 0.77f), 0.18f);
Material apricot = MakeMaterial("MV_Apricot", Srgb(0.90f, 0.58f, 0.37f), 0.32f);
Material mint = MakeMaterial("MV_Mint", Srgb(0.36f, 0.70f, 0.55f), 0.72f);
Material dome = MakeMaterial("MV_DomeBlue", Srgb(0.30f, 0.47f, 0.80f), 0.40f);
Material ground = MakeMaterial("Preview_Foundation", Srgb(0.74f, 0.45f, 0.47f), 0.12f);
Material ghost = MakeMaterial("Preview_CollisionGhost", new Color(1f, 1f, 1f, 0.18f), 0.1f);
ghost.SetFloat("_Surface", 1f);
ghost.SetOverrideTag("RenderType", "Transparent");
ghost.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
ghost.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
ghost.SetInt("_ZWrite", 0);
ghost.renderQueue = (int)RenderQueue.Transparent;
ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
EditorUtility.SetDirty(ghost);
AssetDatabase.SaveAssets();

void PrepareModel(string fileName, params Material[] materials)
{
    string path = FbxDir + "/" + fileName;
    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
    if (!importer)
        throw new FileNotFoundException("Missing FBX importer.", path);
    importer.globalScale = 1f;
    importer.useFileUnits = true;
    importer.bakeAxisConversion = false;
    importer.importCameras = false;
    importer.importLights = false;
    importer.importAnimation = false;
    importer.animationType = ModelImporterAnimationType.None;
    importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
    importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
    importer.materialLocation = ModelImporterMaterialLocation.External;
    importer.isReadable = true;
    importer.addCollider = false;
    importer.meshCompression = ModelImporterMeshCompression.Off;
    foreach (Material material in materials)
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), material.name), material);
    importer.SaveAndReimport();
}

PrepareModel("LiftGate_Shutter.fbx", mint, apricot, dome);
PrepareModel("LiftGate_FrameLintel.fbx", ivory, apricot);
PrepareModel("LiftGate_FramePiers.fbx", ivory, apricot, dome);
string extracted = FbxDir + "/Materials";
if (AssetDatabase.IsValidFolder(extracted))
    AssetDatabase.DeleteAsset(extracted);
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
PrepareModel("LiftGate_Shutter.fbx", mint, apricot, dome);
PrepareModel("LiftGate_FrameLintel.fbx", ivory, apricot);
PrepareModel("LiftGate_FramePiers.fbx", ivory, apricot, dome);

GameObject shutterModel = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + "/LiftGate_Shutter.fbx");
GameObject lintelModel = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + "/LiftGate_FrameLintel.fbx");
GameObject pierModel = AssetDatabase.LoadAssetAtPath<GameObject>(FbxDir + "/LiftGate_FramePiers.fbx");
if (!shutterModel || !lintelModel || !pierModel)
    throw new InvalidOperationException("Unity did not import one of the lift-gate FBX files.");

Scene original = SceneManager.GetActiveScene();
bool originalDirty = original.isDirty;
string originalPath = original.path;
Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
EditorSceneManager.SetActiveScene(preview);
var report = new StringBuilder();
try
{
    void SetLayer(GameObject go)
    {
        go.layer = PreviewLayer;
        foreach (Transform child in go.transform)
            SetLayer(child.gameObject);
    }

    GameObject Mount(Transform parent, GameObject model)
    {
        var imported = (GameObject)PrefabUtility.InstantiatePrefab(model);
        imported.transform.SetParent(parent, false);
        imported.transform.localPosition = Vector3.zero;
        imported.transform.localRotation = model.transform.localRotation;
        imported.transform.localScale = model.transform.localScale;
        report.AppendLine($"{model.name} import localRot={imported.transform.localRotation.eulerAngles} scale={imported.transform.localScale}");
        return imported;
    }

    Transform Axis(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    var shutterRoot = new GameObject("LiftGate Shutter");
    Mount(Axis(shutterRoot.transform, "Orientation", Vector3.zero, wallRotation), shutterModel);
    var frameRoot = new GameObject("LiftGate Frame");
    Mount(Axis(frameRoot.transform, "Orientation", orientationOffset, wallRotation), lintelModel);
    Mount(Axis(frameRoot.transform, "PierYaw", orientationOffset, yawRotation), pierModel);

    if (shutterRoot.GetComponentsInChildren<Collider>(true).Length > 0 || frameRoot.GetComponentsInChildren<Collider>(true).Length > 0)
        throw new InvalidOperationException("Lift-gate art must not contain colliders.");

    string shutterPath = PrefabDir + "/LiftGate Shutter.prefab";
    string framePath = PrefabDir + "/LiftGate Frame.prefab";
    GameObject shutterPrefab = PrefabUtility.SaveAsPrefabAsset(shutterRoot, shutterPath);
    GameObject framePrefab = PrefabUtility.SaveAsPrefabAsset(frameRoot, framePath);
    UnityEngine.Object.DestroyImmediate(shutterRoot);
    UnityEngine.Object.DestroyImmediate(frameRoot);

    var frame = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, preview);
    frame.transform.SetPositionAndRotation(frameAnchor, Quaternion.identity);
    var shutter = (GameObject)PrefabUtility.InstantiatePrefab(shutterPrefab, preview);
    shutter.name = "LiftGate Shutter";
    shutter.transform.SetPositionAndRotation(wallCenter, Quaternion.identity);
    SetLayer(frame);
    SetLayer(shutter);

    var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
    UnityEngine.Object.DestroyImmediate(foundation.GetComponent<Collider>());
    foundation.name = "Preview Foundation";
    foundation.transform.SetPositionAndRotation(new Vector3(-0.8f, -0.42f, 2.2f), Quaternion.identity);
    foundation.transform.localScale = new Vector3(14f, 0.40f, 12f);
    foundation.GetComponent<MeshRenderer>().sharedMaterial = ground;
    SceneManager.MoveGameObjectToScene(foundation, preview);
    SetLayer(foundation);

    var reference = GameObject.CreatePrimitive(PrimitiveType.Cube);
    UnityEngine.Object.DestroyImmediate(reference.GetComponent<Collider>());
    reference.name = "Collision Reference";
    reference.transform.SetPositionAndRotation(wallCenter, wallRotation);
    reference.transform.localScale = new Vector3(2.60f, 1.25f, 0.29f);
    reference.GetComponent<MeshRenderer>().sharedMaterial = ghost;
    reference.GetComponent<MeshRenderer>().enabled = false;
    SceneManager.MoveGameObjectToScene(reference, preview);
    SetLayer(reference);

    Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    float excess = 0f;
    int triangles = 0;
    int shutterTriangles = 0;
    int frameTriangles = 0;
    var materials = new HashSet<string>();
    foreach (var filter in shutter.GetComponentsInChildren<MeshFilter>())
    {
        Mesh mesh = filter.sharedMesh;
        if (!mesh || !mesh.isReadable)
            throw new InvalidOperationException("Shutter mesh is not readable: " + filter.name);
        shutterTriangles += mesh.triangles.Length / 3;
        var renderer = filter.GetComponent<MeshRenderer>();
        if (renderer)
            foreach (Material material in renderer.sharedMaterials)
                if (material)
                    materials.Add(material.name);
        foreach (Vector3 vertex in mesh.vertices)
        {
            Vector3 world = filter.transform.TransformPoint(vertex);
            Vector3 local = Quaternion.Inverse(wallRotation) * (world - wallCenter);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
            excess = Mathf.Max(excess, local.x - halfExtents.x, -halfExtents.x - local.x,
                local.y - halfExtents.y, -halfExtents.y - local.y,
                local.z - halfExtents.z, -halfExtents.z - local.z);
        }
    }
    foreach (var filter in frame.GetComponentsInChildren<MeshFilter>())
    {
        if (filter.sharedMesh)
            frameTriangles += filter.sharedMesh.triangles.Length / 3;
        var renderer = filter.GetComponent<MeshRenderer>();
        if (renderer)
            foreach (Material material in renderer.sharedMaterials)
                if (material)
                    materials.Add(material.name);
    }
    triangles = shutterTriangles + frameTriangles;
    report.AppendLine($"shutterLocalMin={min}");
    report.AppendLine($"shutterLocalMax={max}");
    report.AppendLine($"maxExcessOutsideCollisionM={excess:F5}");
    report.AppendLine($"triangles shutter={shutterTriangles} frame={frameTriangles} total={triangles}");
    report.AppendLine("materials=" + string.Join(", ", materials.OrderBy(name => name)));

    void AddLight(string name, Vector3 rayDirection, float intensity, bool shadows)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, preview);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Srgb(1f, 0.94f, 0.84f);
        light.intensity = intensity;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        go.transform.rotation = Quaternion.LookRotation(rayDirection);
        SetLayer(go);
    }
    AddLight("Key Light", new Vector3(-0.35f, -0.82f, 0.45f), 1.25f, true);
    AddLight("Fill Light", new Vector3(0.55f, -0.25f, -0.70f), 0.28f, false);

    Camera MakeCamera(string name, Vector3 position, Quaternion rotation, bool ortho, float sizeOrFov)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, preview);
        var camera = go.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(position, rotation);
        camera.orthographic = ortho;
        if (ortho)
            camera.orthographicSize = sizeOrFov;
        else
            camera.fieldOfView = sizeOrFov;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Srgb(0.86f, 0.78f, 0.74f);
        camera.cullingMask = 1 << PreviewLayer;
        camera.allowHDR = false;
        camera.allowMSAA = true;
        SetLayer(go);
        return camera;
    }

    Vector3 officialFocus = new(-0.05f, 0.95f, 0.25f);
    Vector3 officialOffset = new(12f, 15f, -15f);
    Camera official = MakeCamera("Official Camera", officialFocus + officialOffset, Quaternion.LookRotation(-officialOffset), true, 9.5f);
    Vector3 gateFocus = new(-1.55f, 2.45f, 3.95f);
    Camera pattern = MakeCamera("Pattern Camera", gateFocus + officialOffset.normalized * 16f, Quaternion.LookRotation(-officialOffset), true, 2.85f);
    Vector3 goal = new(5f, 1.5f, 5.2f);
    Vector3 lookTarget = new(-1.55f, 2.05f, 3.95f);
    Camera lookback = MakeCamera("Goal Lookback", goal, Quaternion.LookRotation(lookTarget - goal), false, 64f);

    void Capture(Camera camera, string fileName)
    {
        var texture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.targetTexture = texture;
        camera.aspect = 1920f / 1080f;
        var request = new RenderPipeline.StandardRequest { destination = texture };
        if (RenderPipeline.SupportsRenderRequest(camera, request))
            RenderPipeline.SubmitRenderRequest(camera, request);
        else
            camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(PreviewDir, fileName), image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(texture);
        report.AppendLine("wrote " + fileName);
    }

    Capture(official, "E-LiftGate_Official_Aligned.png");
    Capture(pattern, "E-LiftGate_Pattern_Aligned.png");
    Capture(lookback, "E-LiftGate_Goal_Lookback.png");
    Vector3 aligned = shutter.transform.position;
    shutter.transform.position = aligned + new Vector3(0f, -0.5f, 0f);
    Capture(official, "E-LiftGate_Official_Offset.png");
    Capture(pattern, "E-LiftGate_Pattern_Offset.png");
    shutter.transform.position = aligned;

    string scenePath = PreviewDir + "/E-LiftGate Preview.unity";
    EditorSceneManager.SaveScene(preview, scenePath);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    File.WriteAllText(DocDir + "/E-LiftGate_Unity.txt", report.ToString());
    if (excess > 0.02f)
        throw new InvalidOperationException($"Shutter exceeds the collision box by {excess:F4} m. See Documentation/E-LiftGate_Unity.txt.");
    Debug.Log("E-LiftGate import OK\n" + report);
}
finally
{
    if (original.IsValid() && original.isLoaded)
        EditorSceneManager.SetActiveScene(original);
    if (preview.IsValid() && preview.isLoaded)
        EditorSceneManager.CloseScene(preview, true);
    report.AppendLine($"originalScene={originalPath} dirtyBefore={originalDirty} dirtyAfter={original.isDirty}");
    File.WriteAllText(DocDir + "/E-LiftGate_Unity.txt", report.ToString());
}
