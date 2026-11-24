using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BounceBack.GeometryWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.K01.Editor
{
    /// <summary>Creates only K-01-owned visual assets and an isolated preview scene.</summary>
    public static class K01Delivery
    {
        public const string Folder = "Assets/Scenes/Archive/BounceBack/Art/K-01";
        public const string PreviewScenePath = Folder + "/Preview/K01_Santorini_Preview.unity";
        private const string SourceScenePath = "Assets/Scenes/Archive/BounceBack/Santorini.unity";
        private static readonly string[] ModelNames =
        {
            "K01_Cupola_Tower", "K01_Blue_Cupola", "K01_Terrace_Foundation",
            "K01_Course_Launch_Terrace", "K01_Course_Stair_1", "K01_Course_Stair_2", "K01_Course_Stair_3",
            "K01_Course_Smooth_Slope", "K01_Course_Ramp_Approach", "K01_Course_Landing_Walk",
            "K01_Course_Goal_Approach", "K01_Course_Goal_Steps_Ramp", "K01_Course_Goal_Terrace",
            "K01_Cliffside_Steps", "K01_Goal_Pavilion", "K01_Cliff_Foundations"
        };
        private static readonly Dictionary<string, Color> Colors = new()
        {
            { "K01_Lime_White", new Color(0.94f, 0.92f, 0.82f) },
            { "K01_Aegean_Blue", new Color(0.20f, 0.45f, 0.73f) },
            { "K01_Recess_Blue", new Color(0.075f, 0.16f, 0.24f) },
            { "K01_Warm_Gold", new Color(0.98f, 0.63f, 0.20f) },
            { "K01_Cliff_Stone", new Color(0.64f, 0.66f, 0.61f) }
        };

        public static void BuildPreview()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateMaterials();
            ImportModels();
            CreateModelPrefabs();
            string scenePath = BuildPreviewScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("K01_PREVIEW_COMPLETE " + scenePath);
        }

        public static void DiagnoseRendering()
        {
            Scene scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Additive);
            var level = scene.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(component => component);
            var lines = new List<string>();
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("K01_"))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                string winding = "no mesh";
                if (mesh && mesh.vertexCount > 2)
                {
                    Vector3 cross = Vector3.Cross(mesh.vertices[1] - mesh.vertices[0], mesh.vertices[2] - mesh.vertices[0]).normalized;
                    winding = $"cross={cross}; normal={mesh.normals[0]}; dot={Vector3.Dot(cross, mesh.normals[0]):F4}";
                }
                lines.Add($"{renderer.name}: enabled={renderer.enabled}; active={renderer.gameObject.activeInHierarchy}; layer={renderer.gameObject.layer}; hideFlags={renderer.gameObject.hideFlags}; bounds={renderer.bounds}; localPos={renderer.transform.localPosition}; worldPos={renderer.transform.position}; localRot={renderer.transform.localRotation}; worldRot={renderer.transform.rotation}; localScale={renderer.transform.localScale}; worldScale={renderer.transform.lossyScale}; parent={renderer.transform.parent.name}; parentLocalRot={renderer.transform.parent.localRotation}; parentWorldRot={renderer.transform.parent.rotation}; meshBounds={(mesh ? mesh.bounds.ToString() : "none")}; forceOff={renderer.forceRenderingOff}; material={string.Join(",", renderer.sharedMaterials.Select(mat => mat ? $"{mat.name}/{mat.shader.name}/base={mat.color}/cull={mat.GetFloat("_Cull")}" : "NULL"))}; {winding}");
            }
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name.StartsWith("K01_"))
                    foreach (var material in renderer.sharedMaterials)
                        if (material)
                            material.SetFloat("_Cull", 0);
            Render(level.gameplayCamera, "K01_DebugDoubleSided.png", 1600, 900);
            File.WriteAllLines($"{Folder}/Documentation/K01_Render_Diagnostics.txt", lines);
            Debug.Log("K01_DIAGNOSTICS\n" + string.Join("\n", lines));
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                throw new InvalidOperationException("K-01 requires URP/Lit.");
            foreach (var entry in Colors)
            {
                string path = $"{Folder}/Materials/{entry.Key}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                    AssetDatabase.CreateAsset(new Material(shader), path);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                material.shader = shader;
                material.color = entry.Value;
                material.SetFloat("_Smoothness", 0.22f);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportModels()
        {
            foreach (string name in ModelNames)
            {
                string path = $"{Folder}/FBX/{name}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer)
                    throw new FileNotFoundException("Missing K-01 model", path);
                importer.globalScale = 1f;
                importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.isReadable = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.SaveAndReimport();
                importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
                importer.SaveAndReimport();
            }
        }

        private static void CreateModelPrefabs()
        {
            Scene previous = SceneManager.GetActiveScene();
            // Single is intentional: Unity cannot add a scene beside an unsaved untitled batch scene.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                foreach (string name in ModelNames)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/FBX/{name}.fbx");
                    var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                    imported.name = name + "_Imported";
                    imported.transform.localPosition = Vector3.zero;
                    // Keep both FBX unit conversion and axis conversion inside the wrapper.
                    imported.transform.localRotation = model.transform.localRotation;
                    imported.transform.localScale = model.transform.localScale;
                    var instance = new GameObject(name);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    imported.transform.SetParent(instance.transform, false);
                    PrefabUtility.SaveAsPrefabAsset(instance, $"{Folder}/Prefabs/{name}.prefab");
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        private static readonly Dictionary<string, Vector3> Positions = new()
        {
            { "K01_Cliffside_Steps", new Vector3(-1.66f, 4.8f, -8.75f) },
            { "K01_Goal_Pavilion", new Vector3(-7.15f, -0.12f, 21.18f) },
            { "K01_Cliff_Foundations", Vector3.zero }
        };

        private static GameObject Instantiate(string modelName, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Prefabs/{modelName}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent.gameObject.scene);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Positions.TryGetValue(modelName, out Vector3 position) ? position : Vector3.zero;
            instance.transform.localRotation = modelName == "K01_Goal_Pavilion" ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static readonly List<string> GeometryReport = new();

        private static void ValidateCourseVisual(Transform route, GameObject visual, BoxCollider box)
        {
            bool hasMesh = false;
            Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh)
                    continue;
                hasMesh = true;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 local = route.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    minimum = Vector3.Min(minimum, local);
                    maximum = Vector3.Max(maximum, local);
                }
            }
            if (!hasMesh)
                throw new InvalidOperationException("K-01 route has no imported mesh: " + route.name);
            Vector3 center = box.center;
            Vector3 half = box.size * 0.5f;
            float topError = Mathf.Abs(maximum.y - (center.y + half.y));
            float bottomError = Mathf.Abs(minimum.y - (center.y - half.y));
            bool widthOkay = Mathf.Abs(maximum.x - (center.x + half.x)) < 0.03f && Mathf.Abs(minimum.x - (center.x - half.x)) < 0.03f;
            bool depthOkay = Mathf.Abs(maximum.z - (center.z + half.z)) < 0.03f && Mathf.Abs(minimum.z - (center.z - half.z)) < 0.03f;
            if (topError > 0.002f || bottomError > 0.002f || !widthOkay || !depthOkay)
                throw new InvalidOperationException($"K-01 road bounds mismatch on {route.name}: localMin={minimum}, localMax={maximum}, colliderCenter={center}, colliderSize={box.size}");
            string axisConversion = visual.transform.childCount > 0 ? visual.transform.GetChild(0).localRotation.ToString() : "missing";
            GeometryReport.Add($"{route.name}: localMin={minimum}; localMax={maximum}; colliderSize={box.size}; rollingTopError={topError:F6}m; wrapperScale={visual.transform.localScale}; importedAxisRotation={axisConversion}");
        }

        private static string BuildPreviewScene()
        {
            GeometryReport.Clear();
            if (!AssetDatabase.CopyAsset(SourceScenePath, PreviewScenePath))
                throw new InvalidOperationException("Could not copy Santorini into the K-01 preview directory.");
            Scene scene = EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var level = scene.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(component => component);
            var course = level.transform.Find("Course");
            foreach (Transform route in course)
            {
                var oldVisual = route.Find("VisualRoot");
                if (!oldVisual)
                    throw new InvalidOperationException("Missing expected course visual: " + route.name);
                var modelName = "K01_Course_" + route.name.Replace(" ", "_");
                UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                var visual = Instantiate(modelName, route);
                visual.name = "VisualRoot";
                var box = route.GetComponent<BoxCollider>();
                if (!box || visual.GetComponentsInChildren<Collider>().Any())
                    throw new InvalidOperationException("Unexpected collider on visual-only module " + route.name);
                ValidateCourseVisual(route, visual, box);
            }

            var architecture = level.transform.Find("Architecture Visuals");
            ReplaceArchitecture(architecture, "Cupola Tower", "K01_Cupola_Tower", new Vector3(-2.4f, -0.7f, -9.9f));
            ReplaceArchitecture(architecture, "Blue Cupola", "K01_Blue_Cupola", new Vector3(-2.4f, 4.5f, -9.9f));
            ReplaceArchitecture(architecture, "Terrace Foundation", "K01_Terrace_Foundation", new Vector3(0, -1.75f, -9.9f));
            Instantiate("K01_Cliffside_Steps", architecture);
            Instantiate("K01_Goal_Pavilion", architecture);
            Instantiate("K01_Cliff_Foundations", architecture);

            var camera = level.gameplayCamera;
            camera.transform.position = new Vector3(20, 22, -6);
            camera.transform.LookAt(new Vector3(-1, 1.5f, 3.5f));
            camera.orthographic = true;
            camera.orthographicSize = 10.6f;
            var reverseObject = new GameObject("K01 Reverse Preview Camera");
            SceneManager.MoveGameObjectToScene(reverseObject, scene);
            reverseObject.transform.SetParent(level.transform, false);
            reverseObject.transform.position = new Vector3(-15, 14, 28);
            reverseObject.transform.LookAt(new Vector3(-0.5f, 2f, 3f));
            var reverse = reverseObject.AddComponent<Camera>();
            reverse.orthographic = true;
            reverse.orthographicSize = 12.2f;
            reverse.enabled = false;
            reverse.clearFlags = camera.clearFlags;
            reverse.backgroundColor = camera.backgroundColor;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            Render(camera, "K01_DefaultCamera_16x9.png", 1600, 900);
            Render(camera, "K01_DefaultCamera_4x3.png", 1200, 900);
            Render(reverse, "K01_ReverseFromGoal.png", 1600, 900);
            WriteVisibilityReport(level, architecture);
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            EditorSceneManager.CloseScene(scene, true);
            return PreviewScenePath;
        }

        private static void ReplaceArchitecture(Transform parent, string name, string modelName, Vector3 position)
        {
            Transform placeholder = parent.Find(name);
            if (!placeholder)
                throw new InvalidOperationException("Missing expected architecture visual: " + name);
            foreach (var renderer in placeholder.GetComponentsInChildren<MeshRenderer>())
                renderer.enabled = false;
            var replacement = Instantiate(modelName, parent);
            replacement.transform.localPosition = position;
        }

        private static void Render(Camera camera, string filename, int width, int height)
        {
            bool enabled = camera.enabled;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.enabled = true;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes($"{Folder}/Preview/{filename}", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.enabled = enabled;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WriteVisibilityReport(GeometryLevel level, Transform architecture)
        {
            Camera camera = level.gameplayCamera;
            var mechanisms = level.transform.Find("Rail Mechanisms");
            var targets = new List<Transform>
            {
                mechanisms.Find("Slime Block 1"), mechanisms.Find("Slime Block 2"),
                mechanisms.Find("Deflector"), level.goal.transform
            };
            var report = new List<string>();
            report.Add("K-01 Unity preview visibility (screen-space checks are not a substitute for a playtest)");
            foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
            {
                camera.aspect = aspect;
                report.Add($"Aspect {aspect:F4}");
                foreach (var target in targets)
                {
                    Vector3 point = camera.WorldToViewportPoint(target.position);
                    bool inFrame = point.z > 0 && point.x > 0.035f && point.x < 0.965f && point.y > 0.035f && point.y < 0.965f;
                    bool blocked = MeshOccludes(camera, target.position, architecture);
                    report.Add($"  {target.name}: viewport=({point.x:F4},{point.y:F4},{point.z:F2}); insideSafeFrame={inFrame}; architectureRayBlocked={blocked}");
                    if (!inFrame || blocked)
                        throw new InvalidOperationException($"K-01 target visibility failed for {target.name} at aspect {aspect}.");
                }
            }
            camera.ResetAspect();
            foreach (var renderer in architecture.GetComponentsInChildren<MeshRenderer>())
                if (renderer.GetComponent<Collider>() || renderer.GetComponentInParent<Collider>())
                    throw new InvalidOperationException("K-01 architecture must not add physics components.");
            report.Add("All K-01 architecture renderers are collider-free.");
            report.Add("Imported metric scale and axis-conversion rotation are preserved inside each visual wrapper.");
            report.AddRange(GeometryReport);
            report.Add("Route colliders and mechanism GameObjects were copied unchanged from the original scene; only renderers were replaced.");
            File.WriteAllLines($"{Folder}/Documentation/K01_Unity_Validation.txt", report);
        }

        private static bool MeshOccludes(Camera camera, Vector3 target, Transform architecture)
        {
            Ray ray = new(camera.transform.position, target - camera.transform.position);
            float targetDistance = Vector3.Distance(camera.transform.position, target);
            foreach (var renderer in architecture.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled)
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                if (!mesh)
                    continue;
                Transform transform = renderer.transform;
                var localRay = new Ray(transform.InverseTransformPoint(ray.origin), transform.InverseTransformVector(ray.direction));
                if (!mesh.bounds.IntersectRay(localRay, out float localDistance))
                    continue;
                Vector3 localHit = localRay.GetPoint(localDistance);
                if (Vector3.Distance(transform.TransformPoint(localHit), camera.transform.position) >= targetDistance - 0.15f)
                    continue;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 a = vertices[triangles[index]];
                    Vector3 b = vertices[triangles[index + 1]];
                    Vector3 c = vertices[triangles[index + 2]];
                    if (!RayTriangle(localRay, a, b, c, out float hit))
                        continue;
                    if (Vector3.Distance(transform.TransformPoint(localRay.GetPoint(hit)), camera.transform.position) < targetDistance - 0.15f)
                        return true;
                }
            }
            return false;
        }

        private static bool RayTriangle(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 p = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (Mathf.Abs(determinant) < 0.000001f)
            {
                distance = 0;
                return false;
            }
            Vector3 t = ray.origin - a;
            float u = Vector3.Dot(t, p) / determinant;
            Vector3 q = Vector3.Cross(t, edge1);
            float v = Vector3.Dot(ray.direction, q) / determinant;
            distance = Vector3.Dot(edge2, q) / determinant;
            return u >= 0 && v >= 0 && u + v <= 1 && distance > 0;
        }
    }
}
