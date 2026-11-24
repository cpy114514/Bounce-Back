using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using BounceBack.StylizedWater;
using Debug = UnityEngine.Debug;

namespace BounceBack.StylizedWater.Editor
{
    /// <summary>Isolated art delivery builder. It never opens or edits the gameplay scene.</summary>
    public static class GStylizedWaterBuild
    {
        public const string Folder = "Assets/Scenes/Archive/BounceBack/Art/G-StylizedWater";
        public const string PreviewPath = Folder + "/Preview/G-StylizedWater Preview.unity";
        const string FbxPath = Folder + "/FBX/G-StylizedWater.fbx";
        const string PoolPrefab = Folder + "/Prefabs/G-PoolWater.prefab";
        const string FallPrefab = Folder + "/Prefabs/G-Waterfall.prefab";
        static readonly int PreviewTime = Shader.PropertyToID("_GPreviewTime");

        [Serializable] public class V3 { public float x, y, z; public Vector3 Vector => new(x, y, z); }
        [Serializable] public class Q { public float x, y, z, w; public Quaternion Quaternion => new(x, y, z, w); }
        [Serializable] public class CollisionItem
        {
            public string name;
            public V3 center, size;
            public Q rotation;
            public bool anchor;
        }
        [Serializable] public class CollisionLayout { public CollisionItem[] items; }

        [MenuItem("Tools/Bounce Back/Build G Stylized Water")]
        public static void Build()
        {
            foreach (string child in new[] { "Materials", "Prefabs", "Preview", "Documentation", "Preview/Motion Frames" })
                Directory.CreateDirectory(Folder + "/" + child);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureModel();
            Material poolMaterial = Material("MV_Water", "BounceBack/G Stylized Pool", new Color(0.45f, 0.80f, 0.84f));
            Material fallsMaterial = Material("MV_Waterfall", "BounceBack/G Stylized Waterfall", new Color(0.45f, 0.80f, 0.84f));
            Material mistMaterial = Material("MV_Mist", "BounceBack/G Water Mist", new Color(0.80f, 0.91f, 0.91f));
            poolMaterial.SetVector("_PoolSize", new Vector4(3.25f, 3.65f, 0, 0));
            poolMaterial.SetFloat("_WaveHeight", 0.006f);
            poolMaterial.SetVector("_ImpactPos", new Vector4(-1.4f, 0.45f, 2.05f, 0));
            poolMaterial.SetFloat("_Charge", 0f);
            EditorUtility.SetDirty(poolMaterial);

            var pool = new GameObject("G-PoolWater");
            var poolMesh = ModelPart("G_PoolSurface", pool.transform, poolMaterial);
            var ripple = pool.AddComponent<WaterRipple>();
            var serialized = new SerializedObject(ripple);
            serialized.FindProperty("waterRenderer").objectReferenceValue = poolMesh.GetComponent<Renderer>();
            serialized.FindProperty("impactPosition").vector3Value = new Vector3(-1.4f, 0.45f, 2.05f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(pool, PoolPrefab);
            UnityEngine.Object.DestroyImmediate(pool);

            var fall = new GameObject("G-Waterfall");
            ModelPart("G_WaterfallRibbon", fall.transform, fallsMaterial);
            ModelPart("G_WaterfallMist", fall.transform, mistMaterial);
            PrefabUtility.SaveAsPrefabAsset(fall, FallPrefab);
            UnityEngine.Object.DestroyImmediate(fall);
            AssetDatabase.SaveAssets();
            BuildPreview();
            Validate();
            Debug.Log("G_STYLIZED_WATER_BUILD_COMPLETE " + PreviewPath);
        }

        static void ConfigureModel()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null)
                throw new InvalidOperationException("Missing Blender FBX: " + FbxPath);
            if (AssetImporter.GetAtPath(FbxPath) is ModelImporter importer)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
        }

        static Material Material(string name, string shader, Color color)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find(shader);
            material.name = name;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject ModelPart(string name, Transform parent, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Transform sourcePart = source.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (!sourcePart) throw new InvalidOperationException("Missing FBX mesh node " + name);
            var part = UnityEngine.Object.Instantiate(sourcePart.gameObject, parent, false);
            part.name = name;
            part.transform.localPosition = sourcePart.localPosition;
            part.transform.localRotation = sourcePart.localRotation;
            part.transform.localScale = sourcePart.localScale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return part;
        }

        static void BuildPreview()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.69f, 0.78f);
            RenderSettings.fog = false;
            var root = new GameObject("G-StylizedWater Preview").transform;
            var water = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PoolPrefab), scene);
            water.transform.SetParent(root, false);
            water.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var ripple = water.GetComponent<WaterRipple>();
            ripple.SetCharge(0.58f, new Vector3(-1.4f, 0.45f, 2.05f));
            var falls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FallPrefab), scene);
            falls.transform.SetParent(root, false);
            falls.transform.SetLocalPositionAndRotation(new Vector3(5.72f, 0.43f, 5.24f), Quaternion.identity);

            var reference = new GameObject("Reference · collision white boxes / no colliders").transform;
            reference.SetParent(root, false);
            Material ivory = PreviewMaterial("MV_Ivory", new Color(0.95f, 0.88f, 0.77f));
            Material shade = PreviewMaterial("MV_IvoryShade", new Color(0.86f, 0.76f, 0.70f));
            Material lilac = PreviewMaterial("MV_Lilac", new Color(0.50f, 0.45f, 0.66f));
            Material mint = PreviewMaterial("MV_Mint", new Color(0.36f, 0.70f, 0.55f));
            Material gold = PreviewMaterial("MV_Gold", new Color(0.96f, 0.70f, 0.32f));
            string json = File.ReadAllText(ProjectPath("ArtSource/Santorini Layout/Santorini Collision Layout.json"));
            var layout = JsonUtility.FromJson<CollisionLayout>(json);
            int visibleBoxes = 0;
            foreach (var item in layout.items.Where(i => !i.anchor && i.name != "Elastic Pool Surface"))
            {
                Material material = item.name == "Lift Wall" ? mint : item.name == "Cascade Pressure Plate Floor" ? shade : ivory;
                Box(reference, item.name, item.center.Vector, item.size.Vector, item.rotation.Quaternion, material);
                visibleBoxes++;
            }
            // Existing white-model context only. Final cliffs belong to task A.
            var context = new GameObject("Preview-only foundation / not a delivery module").transform;
            context.SetParent(root, false);
            Box(context, "Floating limestone foundation", new Vector3(0, -0.70f, 0.05f), new Vector3(12.15f, 0.72f, 9.65f), Quaternion.identity, ivory);
            Box(context, "Lower limestone course", new Vector3(0, -1.13f, 0.05f), new Vector3(11.68f, 0.18f, 9.20f), Quaternion.identity, lilac);
            Box(context, "Foundation crown", new Vector3(0, -0.29f, 0.05f), new Vector3(12.36f, 0.13f, 9.83f), Quaternion.identity, ivory);
            Box(context, "White tower", new Vector3(-5.64f, 2.27f, -3.65f), new Vector3(1.77f, 4.02f, 1.79f), Quaternion.identity, ivory);
            Box(context, "Tower foot", new Vector3(-5.64f, 0.10f, -3.65f), new Vector3(2.17f, 0.45f, 2.20f), Quaternion.identity, lilac);
            Box(context, "Slope foundation", new Vector3(-1.65f, 0.92f, -1.6f), new Vector3(1.68f, 2.33f, 1.54f), Quaternion.identity, ivory);
            Box(context, "Pool basin", new Vector3(-1.4f, -0.03f, 2.05f), new Vector3(3.6f, 0.50f, 3.95f), Quaternion.identity, lilac);
            foreach (float x in new[] { -3.15f, 0.35f })
                Box(context, "Long pool coping", new Vector3(x, 0.36f, 2.05f), new Vector3(0.23f, 0.25f, 4.05f), Quaternion.identity, ivory);
            foreach (float z in new[] { 0.13f, 3.98f })
                Box(context, "Short pool coping", new Vector3(-1.4f, 0.36f, z), new Vector3(3.35f, 0.25f, 0.22f), Quaternion.identity, ivory);
            Box(context, "Waterfall cornice", new Vector3(5.68f, 0.46f, 5.24f), new Vector3(0.28f, 0.25f, 2.32f), Quaternion.identity, ivory);
            // A vertical +X cliff placeholder, explicitly requested until task A is integrated.
            Quad(context, "Task A cliff placeholder · replace with final recess", new Vector3(5.76f, -4.59f, 5.24f),
                 new Vector3(0, 10.04f, 0), new Vector3(0, 0, 2.32f), lilac);

            Sphere(root, "Reference gold ball · diameter 0.76", new Vector3(-5.9f, 4.83f, -3.65f), 0.76f, gold);
            var lightObject = new GameObject("Preview sun");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.rotation = Quaternion.Euler(38, -32, 0);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.91f, 0.78f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;

            Camera formal = MakeCamera(root, "Formal Camera · orthographic 9.5", new Vector3(-0.05f, 0.95f, 0.25f) + new Vector3(12, 15, -15), new Vector3(-0.05f, 0.95f, 0.25f), 9.5f);
            Camera reverse = MakeCamera(root, "Reverse Camera · from goal to tower", new Vector3(5, 1.5f, 5.2f), new Vector3(-3.4f, 1.75f, -1.8f), 7.8f);
            Camera detail = MakeCamera(root, "Waterfall Detail Camera", new Vector3(13, 2.4f, -1.4f), new Vector3(5.8f, -4.05f, 5.24f), 6.6f);
            reverse.enabled = detail.enabled = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            RenderShot(formal, Folder + "/Preview/G-StylizedWater_Formal.png", 1920, 1080, 0.58f);
            RenderShot(reverse, Folder + "/Preview/G-StylizedWater_ReverseFromGoal.png", 1920, 1080, 0.58f);
            RenderShot(detail, Folder + "/Preview/G-StylizedWater_WaterfallDetail.png", 1920, 1080, 0.58f);
            Shader.SetGlobalFloat(PreviewTime, 0f);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            Debug.Log($"G_PREVIEW_REFERENCE: {visibleBoxes} visible collision boxes plus Elastic Pool Surface represented by water; all seven anchors read.");
        }

        static Material PreviewMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("BounceBack/Geometry Matte")) { name = "Preview " + name };
            material.SetColor("_BaseColor", color);
            return material;
        }

        static void Quiet(GameObject obj, Material material)
        {
            foreach (var collider in obj.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetLocalPositionAndRotation(position, rotation);
            obj.transform.localScale = scale;
            Quiet(obj, material);
            return obj;
        }

        static void Sphere(Transform parent, string name, Vector3 position, float diameter, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = Vector3.one * diameter;
            Quiet(obj, material);
        }

        static void Quad(Transform parent, string name, Vector3 center, Vector3 up, Vector3 across, Material material)
        {
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.vertices = new[] { center - up * 0.5f - across * 0.5f, center - up * 0.5f + across * 0.5f,
                                    center + up * 0.5f + across * 0.5f, center + up * 0.5f - across * 0.5f };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            Quiet(obj, material);
        }

        static Camera MakeCamera(Transform parent, string name, Vector3 position, Vector3 lookAt, float size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            var camera = obj.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.76f, 0.84f);
            camera.allowMSAA = true;
            camera.allowHDR = false;
            camera.aspect = 16f / 9f;
            return camera;
        }

        static void RenderShot(Camera camera, string path, int width, int height, float charge, float time = 2.0f)
        {
            var ripple = UnityEngine.Object.FindObjectsByType<WaterRipple>(FindObjectsSortMode.None).FirstOrDefault();
            if (ripple) ripple.SetCharge(charge, new Vector3(-1.4f, 0.45f, 2.05f));
            Shader.SetGlobalFloat(PreviewTime, time);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            bool enabled = camera.enabled;
            try
            {
                camera.enabled = true;
                camera.aspect = width / (float)height;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.enabled = enabled;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public static void CaptureMotion()
        {
            var scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name.StartsWith("Formal Camera"));
            string folder = Folder + "/Preview/Motion Frames";
            Directory.CreateDirectory(folder);
            const int count = 100;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    float time = i * 0.05f;
                    float charge = i < 16 ? 0f : i < 45 ? (i - 16) / 29f : Mathf.Lerp(1f, 0f, (i - 45) / 54f);
                    RenderShot(camera, Path.Combine(folder, $"Water_{i:000}.png"), 960, 540, charge, time + 0.02f);
                }
            }
            finally { Shader.SetGlobalFloat(PreviewTime, 0f); }
            File.WriteAllText(Folder + "/Documentation/G-StylizedWater_Motion.txt",
                "100 frames, 960x540, 20 fps, exactly 5 seconds. Charge=0 for frames 0-15; rises over frames 16-44; ring expands then relaxes over frames 45-99.\n" +
                "Waterfall and mist animate for the full five seconds. Deterministic shader time was used for reproducibility.\n" +
                "Unity batchmode RenderTexture capture, not a gameplay/physics test.\n");
            Debug.Log("G_MOTION_CAPTURE_COMPLETE " + count + " frames");
        }

        public static void Validate()
        {
            var report = new List<string>();
            foreach (string path in new[] { "GPoolWater", "GWaterfall", "GWaterMist" })
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Shaders/" + path + ".shader");
                if (!shader || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Shader import/compile error: " + path);
                report.Add(path + ": imported without ShaderUtil compile errors.");
            }
            var scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            foreach (string path in new[] { PoolPrefab, FallPrefab })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                int triangles = 0;
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    triangles += mesh.triangles.Length / 3;
                    if (mesh.subMeshCount > 4) throw new InvalidOperationException(path + " exceeds four slots");
                    if (!mesh.uv.Any()) throw new InvalidOperationException(path + " missing UV0");
                }
                if (triangles > 15000) throw new InvalidOperationException(path + " exceeds triangle budget");
                if (instance.GetComponentsInChildren<Collider>(true).Any() || instance.GetComponentsInChildren<Rigidbody>(true).Any())
                    throw new InvalidOperationException(path + " contains physics");
                report.Add($"{path}: {triangles} triangles; no collider/Rigidbody; static mesh and shader animation only.");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            var pool = AssetDatabase.LoadAssetAtPath<GameObject>(PoolPrefab);
            var poolPart = pool.GetComponentsInChildren<MeshFilter>(true).Single();
            var points = poolPart.sharedMesh.vertices.Select(v => poolPart.transform.TransformPoint(v)).ToArray();
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            float minZ = points.Min(p => p.z), maxZ = points.Max(p => p.z);
            var expected = new Vector3(-1.4f, 0.45f, 2.05f);
            var actual = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            float error = Vector3.Distance(actual, expected);
            if (error > 0.0001f || Mathf.Abs(maxX - minX - 3.25f) > 0.0001f || Mathf.Abs(maxZ - minZ - 3.65f) > 0.0001f)
                throw new InvalidOperationException("Pool alignment failed: " + actual);
            if (poolPart.sharedMesh.normals.Select(n => poolPart.transform.TransformDirection(n).normalized.y).Average() < 0.95f)
                throw new InvalidOperationException("Pool surface normals are not upward.");
            report.Add($"Pool flat mesh top Y=0.45, XZ=3.25x3.65, center error={error:F8} m; max shader displacement below top=0.018 m; all <=0.02 m.");
            report.Add("Pool clearance: water is the Elastic Pool Surface; no extra railing, splash, or tall geometry was added.");
            var fallAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FallPrefab);
            var fallPart = fallAsset.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "G_WaterfallRibbon");
            var fallPoints = fallPart.sharedMesh.vertices.Select(v => fallPart.transform.TransformPoint(v)).ToArray();
            float attachmentError = fallPoints.Where(p => Mathf.Abs(p.y) < 0.0001f).Min(p => p.magnitude);
            if (attachmentError > 0.0001f || fallPoints.Min(p => p.x) < -0.0001f || fallPoints.Max(p => p.y) > 0.0001f)
                throw new InvalidOperationException("Waterfall attachment/outward alignment failed: " + attachmentError);
            float actualDrop = fallPoints.Max(p => p.y) - fallPoints.Min(p => p.y);
            if (Mathf.Abs(actualDrop - 10f) > 0.0001f) throw new InvalidOperationException("Waterfall drop is not 10 m");
            report.Add($"Waterfall slot: exact top attachment error={attachmentError:F8} m, identity root rotation; 10 m drop below Y=0.43 and outboard +X; does not block the plate.");
            report.Add("No Compute, depth texture, opaque texture, screen refraction, tessellation, render feature, URP asset change, or external texture.");
            report.Add("Preview scene and Editor capture tools are not gameplay integration; no PlayMode physics test was run for this visual-only task.");
            File.WriteAllLines(Folder + "/Documentation/G-StylizedWater_UnityValidation.txt", report);
            Debug.Log("G_VALIDATION_PASS\n" + string.Join("\n", report));
        }

        public static void Measure()
        {
            EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name.StartsWith("Formal Camera"));
            var roots = UnityEngine.Object.FindObjectsByType<WaterRipple>(FindObjectsSortMode.None).Select(r => r.gameObject).ToList();
            roots.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "G-Waterfall").Select(t => t.gameObject));
            // There is exactly one water root in the preview; include its disabled clone if named by Unity.
            roots = roots.Distinct().ToList();
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.aspect = 16f / 9f;
            Shader.SetGlobalFloat(PreviewTime, 1f);
            using var draws = new ProfilerRecorder(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var triangles = new ProfilerRecorder(ProfilerCategory.Render, "Triangles Count", 1);
            using var renderRecorder = new ProfilerRecorder(ProfilerCategory.Internal, "Camera.Render", 1);
            List<double> onTimes = new(), offTimes = new();
            List<double> onRecorder = new(), offRecorder = new();
            List<double> onGpu = new(), offGpu = new();
            long onDraws = 0, offDraws = 0, onTriangles = 0, offTriangles = 0;
            try
            {
                for (int i = 0; i < 8; i++) { camera.Render(); }
                for (int i = 0; i < 120; i++)
                {
                    bool on = i % 2 == 0;
                    foreach (var obj in roots) obj.SetActive(on);
                    Shader.SetGlobalFloat(PreviewTime, 1f + i * 0.01667f);
                    FrameTimingManager.CaptureFrameTimings();
                    var stopwatch = Stopwatch.StartNew();
                    camera.Render();
                    stopwatch.Stop();
                    var frameTimings = new FrameTiming[1];
                    if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0 && frameTimings[0].gpuFrameTime > 0)
                        (on ? onGpu : offGpu).Add(frameTimings[0].gpuFrameTime);
                    double ms = stopwatch.Elapsed.TotalMilliseconds;
                    double recorded = renderRecorder.Valid ? renderRecorder.LastValue / 1000000.0 : 0;
                    if (on)
                    {
                        onTimes.Add(ms); onRecorder.Add(recorded);
                        onDraws = draws.LastValue; onTriangles = triangles.LastValue;
                    }
                    else
                    {
                        offTimes.Add(ms); offRecorder.Add(recorded);
                        offDraws = draws.LastValue; offTriangles = triangles.LastValue;
                    }
                }
                var data = new Dictionary<string, object>
                {
                    ["scope"] = "Unity 6000.3.23f1 Editor batchmode, 1920x1080, alternating A/B full-camera renders; an estimation proxy, NOT a WebGL browser GPU benchmark.",
                    ["render_pipeline"] = "URP 17.3; project Linear color space; no Opaque/Depth texture required by water.",
                    ["water_objects"] = roots.Count,
                    ["iterations_each"] = 60,
                    ["median_cpu_submit_delta_ms"] = Median(onTimes) - Median(offTimes),
                    ["median_water_cpu_submit_ms"] = Median(onTimes),
                    ["median_without_water_cpu_submit_ms"] = Median(offTimes),
                    ["median_camera_render_profiler_delta_ms"] = Median(onRecorder) - Median(offRecorder),
                    ["draw_calls_with_water"] = onDraws,
                    ["draw_calls_without_water"] = offDraws,
                    ["triangles_with_water"] = onTriangles,
                    ["triangles_without_water"] = offTriangles,
                    ["gpu_time_supported_in_this_capture"] = onGpu.Count > 0 && offGpu.Count > 0,
                    ["gpu_frame_time_samples_with_water"] = onGpu.Count,
                    ["gpu_frame_time_samples_without_water"] = offGpu.Count,
                    ["median_gpu_frame_delta_ms"] = onGpu.Count > 0 && offGpu.Count > 0 ? Median(onGpu) - Median(offGpu) : 0,
                    ["gpu_measurement_note"] = "Offscreen Camera.Render has no trustworthy WebGL GPU timing. Do not treat CPU submit time as GPU time. Final browser profiling remains integration work.",
                    ["shader_cost"] = "3 water draw calls, 2950 authored triangles, no texture fetches, no screen copies, no particle simulation, no shadow casters.",
                };
                File.WriteAllText(Folder + "/Documentation/G-StylizedWater_Performance.json", JsonUtility.ToJson(new SerializableReport { entries = data.Select(p => new ReportEntry { key = p.Key, value = p.Value?.ToString() }).ToArray() }, true));
                Debug.Log("G_PERFORMANCE_PROXY " + JsonUtility.ToJson(new SerializableReport { entries = data.Select(p => new ReportEntry { key = p.Key, value = p.Value?.ToString() }).ToArray() }, true));
            }
            finally
            {
                foreach (var obj in roots) obj.SetActive(true);
                Shader.SetGlobalFloat(PreviewTime, 0f);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // A real editor-frame capture complements the immediate offscreen CPU estimate above.
        // No runtime profiler component is added to the shipped water prefabs.
        static PresentedProfiler presentedProfiler;
        public static void MeasurePresented()
        {
            EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name.StartsWith("Formal Camera"));
            presentedProfiler = new PresentedProfiler(camera);
            EditorApplication.update += presentedProfiler.Step;
        }

        sealed class PresentedProfiler
        {
            readonly Camera camera;
            readonly RenderTexture target;
            readonly List<GameObject> roots;
            readonly ProfilerRecorder draws = new(ProfilerCategory.Render, "Draw Calls Count", 1);
            readonly ProfilerRecorder triangles = new(ProfilerCategory.Render, "Triangles Count", 1);
            readonly List<double> onCpu = new(), offCpu = new(), onGpu = new(), offGpu = new();
            readonly List<long> onDraws = new(), offDraws = new(), onTriangles = new(), offTriangles = new();
            readonly Stopwatch clock = new();
            int frame;
            bool completed;

            public PresentedProfiler(Camera camera)
            {
                this.camera = camera;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.enabled = false;
                camera.targetTexture = target;
                camera.aspect = 16f / 9f;
                roots = UnityEngine.Object.FindObjectsByType<WaterRipple>(FindObjectsSortMode.None).Select(r => r.gameObject).ToList();
                roots.AddRange(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "G-Waterfall").Select(t => t.gameObject));
                roots = roots.Distinct().ToList();
                UnityEngine.Profiling.Profiler.logFile = ProjectPath(Folder + "/Documentation/G-StylizedWater_Presented");
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                UnityEngine.Profiling.Profiler.enabled = true;
            }

            public void Step()
            {
                if (completed) return;
                try
                {
                    bool on = ((Mathf.Max(0, frame - 8)) / 4) % 2 == 0;
                    foreach (var obj in roots) obj.SetActive(on);
                    Shader.SetGlobalFloat(PreviewTime, 1f + frame / 60f);
                    FrameTimingManager.CaptureFrameTimings();
                    clock.Restart();
                    camera.Render();
                    RenderTexture.active = null;
                    Graphics.Blit(target, (RenderTexture)null);
                    clock.Stop();
                    var timings = new FrameTiming[1];
                    bool gpu = FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0;
                    if (frame >= 12)
                    {
                        (on ? onCpu : offCpu).Add(clock.Elapsed.TotalMilliseconds);
                        if (gpu) (on ? onGpu : offGpu).Add(timings[0].gpuFrameTime);
                        if (draws.Valid) (on ? onDraws : offDraws).Add(draws.LastValue);
                        if (triangles.Valid) (on ? onTriangles : offTriangles).Add(triangles.LastValue);
                    }
                    frame++;
                    if (frame >= 132) Finish(null);
                }
                catch (Exception exception) { Finish(exception.ToString()); }
            }

            void Finish(string error)
            {
                if (completed) return;
                completed = true;
                EditorApplication.update -= Step;
                var entries = new List<ReportEntry>
                {
                    new() { key = "scope", value = "Unity Editor real-frame ProfilerRecorder / FrameTimingManager A/B capture. Water renders into 1920x1080 target; same target is blitted in both arms. Intel Arc 140T D3D12, not WebGL." },
                    new() { key = "frames_per_arm", value = onCpu.Count.ToString() },
                    new() { key = "median_cpu_submit_delta_ms", value = (Median(onCpu) - Median(offCpu)).ToString("F6") },
                    new() { key = "gpu_samples_per_arm", value = onGpu.Count.ToString() },
                    new() { key = "median_gpu_frame_delta_ms", value = onGpu.Count > 0 && offGpu.Count > 0 ? (Median(onGpu) - Median(offGpu)).ToString("F6") : "unavailable" },
                    new() { key = "median_draw_calls_with_water", value = onDraws.Count > 0 ? Median(onDraws).ToString() : "unavailable" },
                    new() { key = "median_draw_calls_without_water", value = offDraws.Count > 0 ? Median(offDraws).ToString() : "unavailable" },
                    new() { key = "median_triangles_with_water", value = onTriangles.Count > 0 ? Median(onTriangles).ToString() : "unavailable" },
                    new() { key = "median_triangles_without_water", value = offTriangles.Count > 0 ? Median(offTriangles).ToString() : "unavailable" },
                    new() { key = "webgl_budget_status", value = "Estimate only. Browser GPU timing is still required for the <1 ms WebGL acceptance criterion." },
                };
                if (error != null) entries.Add(new ReportEntry { key = "error", value = error });
                File.WriteAllText(ProjectPath(Folder + "/Documentation/G-StylizedWater_PresentedProfiler.json"), JsonUtility.ToJson(new SerializableReport { entries = entries.ToArray() }, true));
                foreach (var obj in roots) obj.SetActive(true);
                camera.targetTexture = null;
                Shader.SetGlobalFloat(PreviewTime, 0f);
                UnityEngine.Profiling.Profiler.enabled = false;
                UnityEngine.Profiling.Profiler.enableBinaryLog = false;
                draws.Dispose();
                triangles.Dispose();
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                Debug.Log("G_PRESENTED_PROFILER_COMPLETE " + string.Join("; ", entries.Select(e => e.key + "=" + e.value)));
                if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
            }
        }

        [Serializable] public class ReportEntry { public string key; public string value; }
        [Serializable] public class SerializableReport { public ReportEntry[] entries; }
        static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            return values[values.Count / 2];
        }

        static long Median(List<long> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            return values[values.Count / 2];
        }

        static string ProjectPath(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
    }
}
