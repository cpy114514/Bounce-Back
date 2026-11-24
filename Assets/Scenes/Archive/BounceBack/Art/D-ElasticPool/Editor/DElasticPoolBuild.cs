using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BounceBack.DElasticPool
{
    /// <summary>
    /// Builds the visual-only pool prefab and the preview scene. Nothing here is attached to the prefab.
    /// </summary>
    public static class DElasticPoolBuild
    {
        const string Root = "Assets/Scenes/Archive/BounceBack/Art/D-ElasticPool";
        const string FbxPath = Root + "/FBX/D-ElasticPool.fbx";
        const string PrefabPath = Root + "/Prefabs/D-ElasticPool.prefab";
        const string PreviewScenePath = Root + "/Preview/D-ElasticPool Preview.unity";
        const string ValidationPath = Root + "/Documentation/D-ElasticPool_UnityValidation.txt";

        static readonly Vector3 Anchor = new(-1.40f, 0.45f, 2.05f);
        const float PoolX0 = -3.025f;
        const float PoolX1 = 0.225f;
        const float PoolZ0 = 0.225f;
        const float PoolZ1 = 3.875f;
        const float CollisionBottom = 0.03f;
        const float RimTop = 0.49f;
        const int PreviewLayer = 31;

        [MenuItem("Tools/Bounce Back/Build D-ElasticPool Preview")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.isDirty)
                    throw new InvalidOperationException("A loaded scene has unsaved changes, so the preview build was not started: " + loaded.path);
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene preview = default;
            try
            {
                EnsureFolders();
                Material[] slots = CreateModuleMaterials();
                ConfigureImporter();
                preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(preview);
                GameObject placed = CreatePrefab(preview, slots);
                BuildPreviewContents(preview, placed);
                AssignPreviewLayer(preview);
                ApplyPreviewLighting();
                EditorSceneManager.SaveScene(preview, PreviewScenePath);
                string report = Measure(placed);
                var pausedLights = PauseForeignLights(preview);
                try
                {
                    Render(preview, "D-ElasticPool_OfficialCamera.png", "Official Camera");
                    Render(preview, "D-ElasticPool_FromGoal.png", "From Goal");
                }
                finally
                {
                    RestoreForeignLights(pausedLights);
                    ClearForeignDirtiness(preview);
                }
                File.WriteAllText(ValidationPath, report);
                EditorSceneManager.SaveScene(preview, PreviewScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log(report);
            }
            finally
            {
                if (preview.IsValid() && preview.isLoaded)
                    EditorSceneManager.CloseScene(preview, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                AssetDatabase.Refresh();
            }
        }

        static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(Root + "/FBX");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Preview");
            EnsureFolder(Root + "/Documentation");
        }

        static void EnsureFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        static Material[] CreateModuleMaterials()
        {
            return new[]
            {
                EnsureLit(Root + "/Materials/MV_Ivory.mat", "MV_Ivory", new Color(0.95f, 0.88f, 0.77f), 0.18f),
                EnsureLit(Root + "/Materials/MV_Apricot.mat", "MV_Apricot", new Color(0.90f, 0.58f, 0.37f), 0.22f),
                EnsureLit(Root + "/Materials/MV_DomeBlue.mat", "MV_DomeBlue", new Color(0.30f, 0.47f, 0.80f), 0.36f),
                EnsureLit(Root + "/Materials/MV_Rose.mat", "MV_Rose", new Color(0.74f, 0.45f, 0.47f), 0.16f),
            };
        }

        static Material EnsureLit(string path, string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                throw new InvalidOperationException("URP Lit shader was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
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

        static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (!importer)
                throw new FileNotFoundException("D-ElasticPool FBX is missing.", FbxPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }

        static GameObject CreatePrefab(Scene preview, Material[] slots)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (!model)
                throw new InvalidOperationException("Imported D-ElasticPool model is missing.");
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            imported.name = "AxisCorrection";
            imported.transform.localPosition = Vector3.zero;

            var root = new GameObject("D-ElasticPool");
            SceneManager.MoveGameObjectToScene(root, preview);
            imported.transform.SetParent(root.transform, false);

            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1)
                throw new InvalidOperationException("Expected one mesh renderer on D-ElasticPool, found " + renderers.Length);
            Mesh mesh = renderers[0].GetComponent<MeshFilter>().sharedMesh;
            if (!mesh || mesh.subMeshCount != slots.Length)
                throw new InvalidOperationException("D-ElasticPool submesh count is " + (mesh ? mesh.subMeshCount : 0) + ", expected " + slots.Length);
            // The FBX importer does not keep Blender's material order. Match each submesh by its shape.
            renderers[0].sharedMaterials = MatchSlots(renderers[0], Anchor - root.transform.position, slots);
            renderers[0].shadowCastingMode = ShadowCastingMode.On;
            renderers[0].receiveShadows = true;

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Length > 0)
                throw new InvalidOperationException("The pool prefab picked up a gameplay script.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            placed.transform.SetPositionAndRotation(Anchor, Quaternion.identity);
            placed.transform.localScale = Vector3.one;
            return placed;
        }

        static Material[] MatchSlots(MeshRenderer renderer, Vector3 shift, Material[] slots)
        {
            Material ivory = slots[0];
            Material apricot = slots[1];
            Material dome = slots[2];
            Material rose = slots[3];
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices;
            var ordered = new Material[mesh.subMeshCount];
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                float minY = float.PositiveInfinity;
                float maxY = float.NegativeInfinity;
                int interior = 0;
                int count = 0;
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (int index in mesh.GetTriangles(submesh))
                {
                    if (!seen.Add(index))
                        continue;
                    Vector3 world = renderer.transform.TransformPoint(vertices[index]) + shift;
                    minY = Mathf.Min(minY, world.y);
                    maxY = Mathf.Max(maxY, world.y);
                    count++;
                    if (world.y > 0.02f && world.y < 0.08f
                        && world.x > PoolX0 + 0.05f && world.x < PoolX1 - 0.05f
                        && world.z > PoolZ0 + 0.05f && world.z < PoolZ1 - 0.05f)
                        interior++;
                }

                if (maxY < 0.05f && minY < -0.1f)
                    ordered[submesh] = rose;
                else if (maxY > 0.52f && minY > 0.1f)
                    ordered[submesh] = apricot;
                else if (count > 0 && interior * 2 > count && maxY < 0.08f)
                    ordered[submesh] = dome;
                else
                    ordered[submesh] = ivory;
            }

            if (System.Array.IndexOf(ordered, ivory) < 0
                || System.Array.IndexOf(ordered, apricot) < 0
                || System.Array.IndexOf(ordered, dome) < 0
                || System.Array.IndexOf(ordered, rose) < 0)
                throw new InvalidOperationException("Could not match the four pool materials to the imported submeshes.");
            return ordered;
        }

        static void BuildPreviewContents(Scene preview, GameObject pool)
        {
            var lighting = new GameObject("Preview Lighting");
            SceneManager.MoveGameObjectToScene(lighting, preview);
            var key = lighting.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.88f, 0.77f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.55f;
            key.cullingMask = 1 << PreviewLayer;
            key.transform.rotation = new Quaternion(0.39961103f, -0.24317658f, 0.11082195f, 0.8768664f);

            var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(foundation.GetComponent<Collider>());
            foundation.name = "Preview Foundation (not part of module)";
            SceneManager.MoveGameObjectToScene(foundation, preview);
            foundation.transform.position = new Vector3(-1.4f, -0.314f, 2.05f);
            foundation.transform.localScale = new Vector3(5.6f, 0.18f, 6.0f);
            var foundationRenderer = foundation.GetComponent<MeshRenderer>();
            foundationRenderer.sharedMaterial = EnsureLit(
                Root + "/Preview/Preview_Foundation.mat", "Preview_Foundation", new Color(0.94f, 0.88f, 0.76f), 0.16f);
            foundationRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var whiteboxMaterial = EnsureGhost(
                Root + "/Preview/Preview_Whitebox.mat", "Preview_Whitebox", new Color(0.93f, 0.90f, 0.84f, 0.22f));
            var wireMaterial = EnsureUnlit(
                Root + "/Preview/Preview_PoolWire.mat", "Preview_PoolWire", new Color(0.18f, 0.62f, 0.78f));
            var whitebox = new GameObject("Collision Whitebox");
            SceneManager.MoveGameObjectToScene(whitebox, preview);
            BuildWhitebox(whitebox.transform, whiteboxMaterial, wireMaterial);

            CreateCamera(preview, "Official Camera",
                new Vector3(-0.05f, 0.95f, 0.25f) + new Vector3(12f, 15f, -15f),
                Quaternion.LookRotation(new Vector3(-12f, -15f, 15f)));
            CreateCamera(preview, "From Goal",
                new Vector3(5f, 1.5f, 5.2f),
                Quaternion.LookRotation(new Vector3(-5.9f, 4.45f, -3.65f) - new Vector3(5f, 1.5f, 5.2f)));
            pool.transform.SetAsLastSibling();
        }

        static void CreateCamera(Scene preview, string name, Vector3 position, Quaternion rotation)
        {
            var cameraObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.orthographic = true;
            camera.orthographicSize = 9.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.71f, 0.82f, 0.87f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 160f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.cullingMask = 1 << PreviewLayer;
            var extra = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!extra)
                extra = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            extra.renderShadows = true;
            extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            extra.antialiasingQuality = AntialiasingQuality.High;
            extra.renderType = CameraRenderType.Base;
        }

        static Material EnsureUnlit(string path, string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                throw new InvalidOperationException("URP Unlit shader was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material EnsureGhost(string path, string name, Color color)
        {
            var material = EnsureLit(path, name, color, 0.08f);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", 1f);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void AssignPreviewLayer(Scene preview)
        {
            foreach (var root in preview.GetRootGameObjects())
                SetLayerRecursive(root, PreviewLayer);
        }

        static void SetLayerRecursive(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        class PausedLight
        {
            public Light Light;
            public bool Enabled;
        }

        static System.Collections.Generic.List<PausedLight> PauseForeignLights(Scene preview)
        {
            var paused = new System.Collections.Generic.List<PausedLight>();
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene == preview)
                    continue;
                paused.Add(new PausedLight { Light = light, Enabled = light.enabled });
                light.enabled = false;
            }
            return paused;
        }

        static void RestoreForeignLights(System.Collections.Generic.List<PausedLight> paused)
        {
            foreach (var state in paused)
            {
                if (state.Light)
                    state.Light.enabled = state.Enabled;
            }
        }

        static void ClearForeignDirtiness(Scene preview)
        {
            var clear = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (clear == null)
                throw new MissingMethodException("EditorSceneManager.ClearSceneDirtiness");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == preview || !scene.isDirty)
                    continue;
                clear.Invoke(null, new object[] { scene });
            }
        }

        [Serializable]
        class LayoutFile
        {
            public LayoutItem[] items;
        }

        [Serializable]
        class LayoutItem
        {
            public string name;
            public Vector3 center;
            public Vector3 size;
            public LayoutQuat rotation;
            public bool anchor;
        }

        [Serializable]
        class LayoutQuat
        {
            public float x, y, z, w;
            public Quaternion Value => new(x, y, z, w);
        }

        static void BuildWhitebox(Transform parent, Material solid, Material wire)
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "ArtSource", "Santorini Layout", "Santorini Collision Layout.json");
            var layout = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path));
            if (layout?.items == null || layout.items.Length == 0)
                throw new InvalidOperationException("Collision layout JSON did not parse.");
            foreach (LayoutItem item in layout.items)
            {
                if (item.anchor || item.size.x < 1e-4f || item.size.y < 1e-4f || item.size.z < 1e-4f)
                    continue;
                if (item.name == "Elastic Pool Surface")
                {
                    BuildWireBox(parent, item.center, Vector3.Scale(item.size, new Vector3(0.985f, 0.92f, 0.985f)), item.rotation.Value, wire);
                    continue;
                }
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.name = item.name;
                cube.transform.SetParent(parent, false);
                cube.transform.SetPositionAndRotation(item.center, item.rotation.Value);
                cube.transform.localScale = item.size;
                var renderer = cube.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = solid;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static void BuildWireBox(Transform parent, Vector3 center, Vector3 size, Quaternion rotation, Material material)
        {
            var cage = new GameObject("Elastic Pool Surface Wire");
            cage.transform.SetParent(parent, false);
            Vector3[] corners = new Vector3[8];
            int index = 0;
            for (int z = 0; z < 2; z++)
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                Vector3 local = new((x - 0.5f) * size.x, (y - 0.5f) * size.y, (z - 0.5f) * size.z);
                corners[index++] = center + rotation * local;
            }
            int[,] edges =
            {
                { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 },
                { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            for (int edge = 0; edge < 12; edge++)
            {
                Vector3 start = corners[edges[edge, 0]];
                Vector3 end = corners[edges[edge, 1]];
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.name = "Wire Edge";
                bar.transform.SetParent(cage.transform, false);
                bar.transform.position = (start + end) * 0.5f;
                Vector3 direction = end - start;
                Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
                bar.transform.rotation = Quaternion.LookRotation(direction, up);
                bar.transform.localScale = new Vector3(0.03f, 0.03f, direction.magnitude);
                var renderer = bar.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void ApplyPreviewLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7666667f, 0.7419608f, 0.8792157f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static string Measure(GameObject pool)
        {
            var builder = new StringBuilder();
            builder.AppendLine("D-ElasticPool Unity validation");
            builder.AppendLine("Anchor world: " + Anchor.ToString("F4"));
            builder.AppendLine("Root local rotation: " + pool.transform.localRotation.ToString("F5"));
            builder.AppendLine("Root local scale: " + pool.transform.localScale.ToString("F4"));
            if (pool.transform.childCount != 1)
                throw new InvalidOperationException("Axis correction child is missing.");
            Transform axis = pool.transform.GetChild(0);
            builder.AppendLine("Axis child: " + axis.name);
            builder.AppendLine("Axis local rotation euler: " + axis.localRotation.eulerAngles.ToString("F3"));
            builder.AppendLine("Axis local scale: " + axis.localScale.ToString("F4"));

            int colliders = pool.GetComponentsInChildren<Collider>(true).Length;
            int scripts = pool.GetComponentsInChildren<MonoBehaviour>(true).Length;
            builder.AppendLine("Colliders on prefab: " + colliders);
            builder.AppendLine("Scripts on prefab: " + scripts);
            if (colliders != 0 || scripts != 0)
                throw new InvalidOperationException("Prefab must stay free of colliders and scripts.");

            var renderer = pool.GetComponentInChildren<MeshRenderer>();
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            int triangles = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                triangles += (int)mesh.GetIndexCount(submesh) / 3;
            builder.AppendLine("Triangles: " + triangles);
            builder.AppendLine("Vertices: " + mesh.vertexCount);
            builder.AppendLine("Submeshes: " + mesh.subMeshCount);
            var names = new string[renderer.sharedMaterials.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = renderer.sharedMaterials[i] ? renderer.sharedMaterials[i].name : "<missing>";
            builder.AppendLine("Material slots: " + string.Join(", ", names));

            Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            float south = float.NegativeInfinity;
            float north = float.PositiveInfinity;
            float east = float.PositiveInfinity;
            float west = float.NegativeInfinity;
            int southCount = 0, northCount = 0, eastCount = 0, westCount = 0;
            float interiorMax = float.NegativeInfinity;
            int grout = 0;
            int rimTop = 0;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 world = renderer.transform.TransformPoint(vertex);
                minimum = Vector3.Min(minimum, world);
                maximum = Vector3.Max(maximum, world);
                if (world.y > 0.02f && world.y < 0.42f && world.z > PoolZ0 - 0.03f && world.z <= PoolZ0 + 0.0001f)
                {
                    south = Mathf.Max(south, world.z);
                    southCount++;
                }
                if (world.y > 0.02f && world.y < 0.42f && world.z >= PoolZ1 - 0.0001f && world.z < PoolZ1 + 0.03f)
                {
                    north = Mathf.Min(north, world.z);
                    northCount++;
                }
                if (world.y > 0.02f && world.y < 0.42f && world.x >= PoolX1 - 0.0001f && world.x < PoolX1 + 0.03f)
                {
                    east = Mathf.Min(east, world.x);
                    eastCount++;
                }
                if (world.y > 0.02f && world.y < 0.42f && world.x > PoolX0 - 0.03f && world.x <= PoolX0 + 0.0001f)
                {
                    west = Mathf.Max(west, world.x);
                    westCount++;
                }
                if (world.x > PoolX0 + 0.05f && world.x < PoolX1 - 0.05f && world.z > PoolZ0 + 0.05f && world.z < PoolZ1 - 0.05f && world.y < 0.1f)
                    interiorMax = Mathf.Max(interiorMax, world.y);
                if (Mathf.Abs(world.y - CollisionBottom) < 0.0004f)
                    grout++;
                if (Mathf.Abs(world.y - RimTop) < 0.0015f)
                    rimTop++;
            }

            float southError = Mathf.Abs(south - PoolZ0);
            float northError = Mathf.Abs(north - PoolZ1);
            float eastError = Mathf.Abs(east - PoolX1);
            float westError = Mathf.Abs(west - PoolX0);
            float reliefError = interiorMax - CollisionBottom;
            builder.AppendLine("World bounds min: " + minimum.ToString("F4"));
            builder.AppendLine("World bounds max: " + maximum.ToString("F4"));
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Inner-face samples S/N/E/W: {0}/{1}/{2}/{3}", southCount, northCount, eastCount, westCount));
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Inner-face error m S/N/E/W: {0:F6}/{1:F6}/{2:F6}/{3:F6}", southError, northError, eastError, westError));
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Floor relief max y: {0:F4}; above collision bottom: {1:F4} m", interiorMax, reliefError));
            builder.AppendLine("Grout vertices at y 0.03: " + grout);
            builder.AppendLine("Coping-top vertices at y 0.49: " + rimTop);
            builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Max vertex y: {0:F4}", maximum.y));

            float worst = Mathf.Max(Mathf.Max(southError, northError), Mathf.Max(eastError, westError));
            if (worst > 0.02f || reliefError > 0.02f || maximum.y > 0.5501f || rimTop < 8 || grout < 4)
                throw new InvalidOperationException("D-ElasticPool failed the alignment check.\n" + builder);
            return builder.ToString();
        }

        static void Render(Scene preview, string fileName, string cameraName)
        {
            Camera camera = null;
            foreach (var root in preview.GetRootGameObjects())
            {
                if (root.name == cameraName)
                    camera = root.GetComponent<Camera>();
            }
            if (!camera)
                throw new InvalidOperationException("Missing preview camera " + cameraName);
            camera.cullingMask = 1 << PreviewLayer;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            const int width = 1920;
            const int height = 1080;
            camera.aspect = width / (float)height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            bool enabled = camera.enabled;
            try
            {
                camera.enabled = true;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Root + "/Preview/" + fileName, image.EncodeToPNG());
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
    }
}
