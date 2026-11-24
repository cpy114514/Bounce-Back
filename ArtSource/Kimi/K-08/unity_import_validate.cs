// Evaluate with Unity CLI script eval --file ArtSource/Kimi/K-08/unity_import_validate.cs
// Imports only K-08 assets, builds one visual prefab, and validates the FBX in a
// temporary additive scene. The active Santorini scene is never opened or saved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

const string Folder = "Assets/Scenes/Archive/BounceBack/Art/K-08";
const string AssetName = "K08_Aegean_Arch_Window";
const string FbxPath = Folder + "/FBX/" + AssetName + ".fbx";
const string PrefabPath = Folder + "/Prefabs/" + AssetName + ".prefab";

Directory.CreateDirectory(Folder + "/Materials");
Directory.CreateDirectory(Folder + "/Prefabs");
Directory.CreateDirectory(Folder + "/Documentation");
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

Shader shader = Shader.Find("Universal Render Pipeline/Lit");
if (!shader)
    throw new InvalidOperationException("K-08 requires the URP/Lit shader.");

Material CreateOrUpdateMaterial(string name, Color color, float smoothness)
{
    string path = Folder + "/Materials/" + name + ".mat";
    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (!material)
    {
        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
    }
    material.shader = shader;
    material.color = color;
    material.SetFloat("_Smoothness", smoothness);
    material.SetFloat("_Metallic", 0f);
    EditorUtility.SetDirty(material);
    return material;
}

Material white = CreateOrUpdateMaterial("K08_White_Limestone", new Color(0.91f, 0.885f, 0.80f), 0.18f);
Material cobalt = CreateOrUpdateMaterial("K08_Cobalt_Blue", new Color(0.025f, 0.105f, 0.52f), 0.44f);
AssetDatabase.SaveAssets();

ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
if (!importer)
    throw new FileNotFoundException("Missing K-08 FBX importer.", FbxPath);
importer.globalScale = 1f;
importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
importer.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
importer.materialLocation = ModelImporterMaterialLocation.External;
importer.isReadable = true;
importer.importNormals = ModelImporterNormals.Import;
importer.importCameras = false;
importer.importLights = false;
importer.importAnimation = false;
importer.animationType = ModelImporterAnimationType.None;
importer.meshCompression = ModelImporterMeshCompression.Off;
importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), white.name), white);
importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), cobalt.name), cobalt);
importer.SaveAndReimport();
AssetDatabase.SaveAssets();
// Unity may auto-extract materials on the first import before the explicit map.
// Remove only that unused K-08-generated folder after the canonical map is saved.
string extractedMaterials = Folder + "/FBX/Materials";
if (AssetDatabase.IsValidFolder(extractedMaterials) && !AssetDatabase.DeleteAsset(extractedMaterials))
    throw new InvalidOperationException("Could not remove redundant auto-extracted K-08 materials.");
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
// Reimport once after deleting stale extracted assets so no in-memory renderer
// can retain a reference to an object removed earlier in this same execution.
importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
importer.SaveAndReimport();
white = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/K08_White_Limestone.mat");
cobalt = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/K08_Cobalt_Blue.mat");

GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
if (!model)
    throw new InvalidOperationException("Unity did not import the K-08 FBX root.");

Scene original = SceneManager.GetActiveScene();
bool originalDirtyBefore = original.isDirty;
Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
StringBuilder report = new();
try
{
    GameObject wrapper = new(AssetName);
    SceneManager.MoveGameObjectToScene(wrapper, temporary);
    GameObject imported = (GameObject)PrefabUtility.InstantiatePrefab(model, temporary);
    imported.name = AssetName + "_Imported";
    imported.transform.localPosition = Vector3.zero;
    imported.transform.localRotation = model.transform.localRotation;
    imported.transform.localScale = model.transform.localScale;
    imported.transform.SetParent(wrapper.transform, false);

    MeshFilter[] filters = wrapper.GetComponentsInChildren<MeshFilter>();
    MeshRenderer[] renderers = wrapper.GetComponentsInChildren<MeshRenderer>();
    if (filters.Length != 2 || renderers.Length != 2)
        throw new InvalidOperationException($"Expected two mesh parts, got {filters.Length} filters and {renderers.Length} renderers.");
    if (wrapper.GetComponentsInChildren<Collider>().Any() || wrapper.GetComponentsInChildren<Rigidbody>().Any())
        throw new InvalidOperationException("K-08 is visual-only and cannot contain physics components.");

    Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    foreach (MeshFilter filter in filters)
    {
        Mesh mesh = filter.sharedMesh;
        if (!mesh || !mesh.isReadable)
            throw new InvalidOperationException("Missing readable imported mesh on " + filter.name);
        foreach (Vector3 vertex in mesh.vertices)
        {
            Vector3 local = wrapper.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
            minimum = Vector3.Min(minimum, local);
            maximum = Vector3.Max(maximum, local);
        }
    }
    Vector3 dimensions = maximum - minimum;
    Vector3 expectedMin = new(-0.60f, 0f, -0.12f);
    Vector3 expectedMax = new(0.60f, 1.80f, 0.12f);
    if ((minimum - expectedMin).magnitude > 0.0002f || (maximum - expectedMax).magnitude > 0.0002f)
        throw new InvalidOperationException($"K-08 bounds mismatch: min={minimum}, max={maximum}, dimensions={dimensions}");
    if (wrapper.transform.position != Vector3.zero || wrapper.transform.localScale != Vector3.one)
        throw new InvalidOperationException("K-08 wrapper pivot or scale changed.");

    bool RayIntersectsMesh(Mesh mesh, Ray ray, float maxDistance)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        for (int index = 0; index < triangles.Length; index += 3)
        {
            Vector3 a = vertices[triangles[index]];
            Vector3 edge1 = vertices[triangles[index + 1]] - a;
            Vector3 edge2 = vertices[triangles[index + 2]] - a;
            Vector3 p = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < 1e-8f)
                continue;
            float inverse = 1f / determinant;
            Vector3 t = ray.origin - a;
            float u = Vector3.Dot(t, p) * inverse;
            if (u < -1e-6f || u > 1.000001f)
                continue;
            Vector3 q = Vector3.Cross(t, edge1);
            float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < -1e-6f || u + v > 1.000001f)
                continue;
            float distance = Vector3.Dot(edge2, q) * inverse;
            if (distance >= 0f && distance <= maxDistance)
                return true;
        }
        return false;
    }

    bool HitsAny(Vector3 sample)
    {
        Vector3 worldOrigin = wrapper.transform.TransformPoint(new Vector3(sample.x, sample.y, -1f));
        Vector3 worldDirection = wrapper.transform.TransformDirection(Vector3.forward);
        foreach (MeshFilter filter in filters)
        {
            Transform transform = filter.transform;
            Vector3 localOrigin = transform.InverseTransformPoint(worldOrigin);
            Vector3 localDirection = transform.InverseTransformDirection(worldDirection).normalized;
            if (RayIntersectsMesh(filter.sharedMesh, new Ray(localOrigin, localDirection), 5f))
                return true;
        }
        return false;
    }

    var clear = new Dictionary<string, Vector3>
    {
        ["center"] = new(0f, 0.90f, 0f),
        ["upper_arch"] = new(0f, 1.56f, 0f),
        ["left_jamb_clear"] = new(-0.36f, 1f, 0f),
        ["right_jamb_clear"] = new(0.36f, 1f, 0f),
        ["lower_clear"] = new(0f, 0.22f, 0f),
    };
    var whiteSolid = new Dictionary<string, Vector3>
    {
        ["left_outer_jamb"] = new(-0.55f, 0.90f, 0f),
        ["right_outer_jamb"] = new(0.55f, 0.90f, 0f),
        ["white_crown"] = new(0f, 1.72f, 0f),
        ["white_base"] = new(0f, 0.07f, 0f),
    };
    var blueSolid = new Dictionary<string, Vector3>
    {
        ["left_blue_reveal"] = new(-0.425f, 0.90f, 0f),
        ["right_blue_reveal"] = new(0.425f, 0.90f, 0f),
        ["blue_crown"] = new(0f, 1.625f, 0f),
        ["blue_base"] = new(0f, 0.155f, 0f),
    };
    foreach (var sample in clear)
        if (HitsAny(sample.Value))
            throw new InvalidOperationException("Unity clear-opening ray was blocked: " + sample.Key);
    foreach (var sample in whiteSolid.Concat(blueSolid))
        if (!HitsAny(sample.Value))
            throw new InvalidOperationException("Unity solid-material ray missed: " + sample.Key);

    foreach (MeshRenderer renderer in renderers)
    {
        if (renderer.sharedMaterials.Length != 1 || !renderer.sharedMaterials[0])
            throw new InvalidOperationException("Expected one remapped material on " + renderer.name);
        string materialName = renderer.sharedMaterials[0].name;
        if (renderer.name.Contains("White") && renderer.sharedMaterials[0] != white)
            throw new InvalidOperationException("White frame material remap failed: " + materialName);
        if (renderer.name.Contains("Cobalt") && renderer.sharedMaterials[0] != cobalt)
            throw new InvalidOperationException("Cobalt liner material remap failed: " + materialName);
    }

    PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
    AssetDatabase.SaveAssets();

    report.AppendLine("K-08 Unity FBX import and single-asset prefab validation: PASS");
    report.AppendLine($"Active scene before/after: {original.path}; dirty before={originalDirtyBefore}; dirty after={original.isDirty}");
    report.AppendLine($"Overall dimensions W x H x D: {dimensions.x:F6} x {dimensions.y:F6} x {dimensions.z:F6} m");
    report.AppendLine($"Local bounds: min={minimum}; max={maximum}; pivot=bottom bounds center (0,0,0)");
    report.AppendLine($"Imported axis rotation preserved inside wrapper: {imported.transform.localRotation}; scale={imported.transform.localScale}");
    report.AppendLine("Clear through-rays PASS: " + string.Join(", ", clear.Keys));
    report.AppendLine("White solid rays PASS: " + string.Join(", ", whiteSolid.Keys));
    report.AppendLine("Cobalt solid rays PASS: " + string.Join(", ", blueSolid.Keys));
    report.AppendLine("Physics components: none; glass/mullions/props: none; external textures: none");
    foreach (MeshFilter filter in filters)
    {
        Mesh mesh = filter.sharedMesh;
        MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
        report.AppendLine($"Part {filter.name}: vertices={mesh.vertexCount}; triangles={mesh.triangles.Length / 3}; material={renderer.sharedMaterials[0].name}; UV0={mesh.uv.Length > 0}");
    }
    report.AppendLine("Prefab: " + PrefabPath);
}
finally
{
    if (temporary.IsValid() && temporary.isLoaded)
        EditorSceneManager.CloseScene(temporary, true);
    if (original.IsValid() && original.isLoaded)
        SceneManager.SetActiveScene(original);
}

File.WriteAllText(Folder + "/Documentation/K08_Unity_Validation.txt", report.ToString(), Encoding.UTF8);
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
Debug.Log("K08_UNITY_VALIDATION_COMPLETE\n" + report);
return report.ToString();
