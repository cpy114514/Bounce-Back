using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WaterfallOutletUnityValidation
{
    private const string Folder = "Assets/Scenes/Archive/BounceBack/Art/WaterfallOutlet";

    private static Vector3[] WorldVertices(MeshFilter filter)
    {
        return filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
    }

    private static Dictionary<string, object> Inspect(string file, int expectedMeshes)
    {
        string path = $"{Folder}/FBX/{file}.fbx";
        bool includesWater = !file.Contains("Stone_Eave");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new FileNotFoundException("Missing imported FBX", path);
        importer.globalScale = 1.0f;
        importer.isReadable = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
            throw new InvalidOperationException("FBX did not produce a GameObject: " + path);
        var filters = model.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null).ToArray();
        if (filters.Length != expectedMeshes)
            throw new InvalidOperationException($"{file}: expected {expectedMeshes} meshes, got {filters.Length}");
        if (model.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new InvalidOperationException(file + ": unexpected collider");
        if (model.transform.localPosition != Vector3.zero)
            throw new InvalidOperationException(file + ": root pivot is not zero");
        if (filters.Any(filter => filter.transform.position.magnitude > 0.0001f))
            throw new InvalidOperationException(file + ": a mesh origin does not coincide with the shared origin");
        var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Any(renderer => renderer.sharedMaterials.Any(mat => mat == null)))
            throw new InvalidOperationException(file + ": missing imported material");

        var allPoints = filters.SelectMany(WorldVertices).ToArray();
        Vector3 min = new(allPoints.Min(p => p.x), allPoints.Min(p => p.y), allPoints.Min(p => p.z));
        Vector3 max = new(allPoints.Max(p => p.x), allPoints.Max(p => p.y), allPoints.Max(p => p.z));
        Vector3 dimensions = max - min;
        if (file.Contains("Stone") && Mathf.Abs(dimensions.x - 1.6f) > 0.0001f)
            throw new InvalidOperationException($"{file}: stone width is {dimensions.x}, expected 1.6");
        if (file.Contains("Stone") && (Mathf.Abs(max.y) > 0.0001f || Mathf.Abs(max.z) > 0.0001f))
            throw new InvalidOperationException($"{file}: front-top lip is not at Y=0,Z=0");
        if (includesWater && (!allPoints.Any(point => Mathf.Abs(point.y + 2.2f) < 0.0001f) || min.y < -2.206f))
            throw new InvalidOperationException($"{file}: no front bottom vertex at Y=-2.2, or thickness exceeds 6 mm below it; minY={min.y}");
        if (includesWater && !filters.Any(filter => WorldVertices(filter).Any(point => point.magnitude < 0.0001f)))
            throw new InvalidOperationException(file + ": water top attachment centre is not at shared origin");
        if (filters.Any(filter => filter.sharedMesh.uv == null || filter.sharedMesh.uv.Length != filter.sharedMesh.vertexCount))
            throw new InvalidOperationException(file + ": missing UV0");

        var meshes = filters.Select(filter => new Dictionary<string, object>
        {
            ["name"] = filter.sharedMesh.name,
            ["vertices"] = filter.sharedMesh.vertexCount,
            ["triangles"] = filter.sharedMesh.triangles.Length / 3,
            ["subMeshes"] = filter.sharedMesh.subMeshCount,
            ["rootLocalPosition"] = filter.transform.localPosition.ToString("F6"),
            ["rootLocalRotation"] = filter.transform.localRotation.ToString("F6"),
            ["worldPosition"] = filter.transform.position.ToString("F6"),
            ["materials"] = string.Join(",", filter.GetComponent<MeshRenderer>().sharedMaterials.Select(mat => mat.name)),
            ["closestVertexToSharedOrigin_m"] = WorldVertices(filter).Min(point => point.magnitude),
        }).ToArray();
        return new Dictionary<string, object>
        {
            ["file"] = path,
            ["meshCount"] = filters.Length,
            ["colliderCount"] = 0,
            ["rootPosition"] = model.transform.localPosition.ToString("F6"),
            ["rootRotation"] = model.transform.localRotation.ToString("F6"),
            ["rootScale"] = model.transform.localScale.ToString("F6"),
            ["boundsMinUnity"] = min.ToString("F6"),
            ["boundsMaxUnity"] = max.ToString("F6"),
            ["dimensionsUnity"] = dimensions.ToString("F6"),
            ["meshes"] = meshes,
        };
    }

    public static string Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new Dictionary<string, object>
        {
            ["asset"] = "WaterfallOutlet",
            ["activeSceneUntouched"] = true,
            ["units"] = "metres; Unity X across, +Y up, +Z outflow",
            ["individualStone"] = Inspect("WaterfallOutlet_Stone_Eave", 1),
            ["individualWater"] = Inspect("WaterfallOutlet_Water_Tongue", 1),
            ["assembly"] = Inspect("WaterfallOutlet_Assembly", 2),
        };
        var lines = new List<string>
        {
            "WaterfallOutlet Unity import validation",
            "Active scene was not opened, reloaded, or modified.",
            "Expected axes: X across, +Y up, +Z outflow; shared root position=(0,0,0).",
        };
        foreach (var entry in report.Where(entry => entry.Value is Dictionary<string, object>))
        {
            var item = (Dictionary<string, object>)entry.Value;
            lines.Add($"{entry.Key}: meshes={item["meshCount"]}; colliders={item["colliderCount"]}; rootPos={item["rootPosition"]}; rootRot={item["rootRotation"]}; rootScale={item["rootScale"]}; min={item["boundsMinUnity"]}; max={item["boundsMaxUnity"]}; dimensions={item["dimensionsUnity"]}");
            foreach (var mesh in (Dictionary<string, object>[])item["meshes"])
                lines.Add($"  {mesh["name"]}: vertices={mesh["vertices"]}; triangles={mesh["triangles"]}; subMeshes={mesh["subMeshes"]}; worldPos={mesh["worldPosition"]}; closestVertexToOrigin={mesh["closestVertexToSharedOrigin_m"]:F6} m; UV0=OK; materials={mesh["materials"]}");
        }
        lines.Add("RESULT=PASS");
        string output = $"{Folder}/Documentation/WaterfallOutlet_Unity_Validation.txt";
        File.WriteAllLines(output, lines);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("WATERFALL_OUTLET_UNITY_VALIDATION_PASS\n" + string.Join("\n", lines));
        return string.Join("\n", lines);
    }

}
