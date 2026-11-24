using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BounceBack.Background.Editor
{
    /// <summary>
    /// Builds the level-1 distant backdrop and an isolated preview. Does not open or save Santorini Gameplay.
    /// </summary>
    public static class SantoriniBackdropBuild
    {
        public const string Folder = "Assets/Scenes/Archive/BounceBack/Art/Background";
        public const string PrefabPath = Folder + "/Santorini Backdrop.prefab";
        public const string PreviewScenePath = Folder + "/Preview/Santorini Backdrop Preview.unity";

        // Copied from Santorini Gameplay / Isometric Camera. The builder only reads these constants.
        static readonly Vector3 PreparationPosition = new(25.5f, 24.3f, -23.35f);
        static readonly Quaternion PreparationRotation = new(0.27419263f, -0.32562947f, 0.099271424f, 0.8994048f);
        const float PreparationSize = 16.8f;
        const int PreviewLayer = 31;
        const float PullbackSize = 21.8f;
        const float PullbackTurn = 0.12f;

        [MenuItem("Tools/Bounce Back/Build Santorini Backdrop")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder + "/Shaders");
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Folder + "/Textures");
            Directory.CreateDirectory(Folder + "/Preview");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var cloud = SaveTexture(Folder + "/Textures/Santorini Cloud Soft.png", PaintClouds(512), true);
            var isle = SaveTexture(Folder + "/Textures/Santorini Isle Soft.png", PaintIsle(256), false);
            var sky = Material(Folder + "/Materials/Santorini Sky.mat", "BounceBack/Santorini Sky",
                new Color(0.63f, 0.79f, 0.90f), 1000);
            var far = DistanceMaterial("Cloud Sea Far", cloud, new Color(0.62f, 0.75f, 0.86f, 1f), 0.72f, 0.20f, 0f, 3000, new Vector2(0.05f, 0f));
            var mid = DistanceMaterial("Cloud Sea Mid", cloud, new Color(0.94f, 0.89f, 0.76f, 1f), 0.66f, 0.18f, 0f, 3010, new Vector2(-0.12f, 0.03f));
            var near = DistanceMaterial("Cloud Sea Near", cloud, new Color(0.98f, 0.96f, 0.92f, 1f), 0.58f, 0.20f, 0f, 3020, new Vector2(0.18f, -0.02f));
            var west = DistanceMaterial("Distant Isle West", isle, new Color(0.50f, 0.58f, 0.66f, 1f), 0.42f, 0.4f, 1f, 3030, Vector2.zero);
            var east = DistanceMaterial("Distant Isle East", isle, new Color(0.54f, 0.62f, 0.70f, 1f), 0.38f, 0.4f, 1f, 3040, new Vector2(0.12f, 0.06f));
            var south = DistanceMaterial("Distant Isle South", isle, new Color(0.58f, 0.66f, 0.72f, 1f), 0.22f, 0.4f, 1f, 3050, new Vector2(0f, 0.1f));
            sky.SetColor("_Mist", new Color(0.63f, 0.79f, 0.90f));
            sky.SetColor("_Air", new Color(0.78f, 0.87f, 0.93f));
            sky.SetColor("_Ivory", new Color(0.96f, 0.92f, 0.84f));
            sky.SetColor("_Sea", new Color(0.84f, 0.88f, 0.90f));

            Scene previous = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            var root = new GameObject("Santorini Backdrop");
            try
            {
                Shell(root.transform, "Sky Shell", PrimitiveType.Sphere, sky, new Vector3(18f, 16f, -4f), Quaternion.identity, Vector3.one * 640f);
                Card(root.transform, "Cloud Sea Far", far, 0.50f, 0.32f, 260f, new Vector2(340f, 34f));
                Card(root.transform, "Cloud Sea Mid", mid, 0.50f, 0.16f, 180f, new Vector2(360f, 30f));
                Card(root.transform, "Cloud Sea Near", near, 0.50f, 0.02f, 120f, new Vector2(380f, 28f));
                Card(root.transform, "Distant Isle West", west, -0.02f, 0.24f, 230f, new Vector2(18f, 8f));
                Card(root.transform, "Distant Isle East", east, 1.02f, 0.20f, 250f, new Vector2(16f, 7f));
                Card(root.transform, "Distant Isle South", south, 0.94f, 0.04f, 310f, new Vector2(14f, 5f));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (root)
                    UnityEngine.Object.DestroyImmediate(root);
                if (scratch.IsValid() && scratch.isLoaded)
                    EditorSceneManager.CloseScene(scratch, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }

            BuildPreview();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SANTORINI_BACKDROP_COMPLETE " + PrefabPath);
        }

        static void BuildPreview()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            try
            {
                var backdrop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), preview);
                backdrop.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var cameraObject = new GameObject("Preparation Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = PreparationSize;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.70f, 0.84f, 0.94f, 1f);
                camera.transform.SetPositionAndRotation(PreparationPosition, PreparationRotation);
                camera.cullingMask = 1 << PreviewLayer;
                cameraObject.tag = "MainCamera";
                CreateProxies(preview);
                SetLayer(backdrop, PreviewLayer);
                foreach (var rootObject in preview.GetRootGameObjects())
                    SetLayer(rootObject, PreviewLayer);

                EditorSceneManager.SaveScene(preview, PreviewScenePath);
                var report = new List<string>
                {
                    "Santorini backdrop preview. Official Santorini Gameplay scene was not modified.",
                    "Game View size 1920x1080, aspect 16:9."
                };
                RenderShot(camera, PreparationPosition, PreparationRotation, PreparationSize,
                    Folder + "/Preview/Santorini Backdrop Preparation.png", report, "Preparation");
                Quaternion pullback = Quaternion.Slerp(PreparationRotation,
                    Quaternion.LookRotation(new Vector3(-1f, -1f, 1f)), PullbackTurn);
                RenderShot(camera, PreparationPosition, pullback, PullbackSize,
                    Folder + "/Preview/Santorini Backdrop Pullback.png", report, "Pullback");
                camera.transform.SetPositionAndRotation(PreparationPosition, PreparationRotation);
                camera.orthographicSize = PreparationSize;
                EditorSceneManager.SaveScene(preview);
                File.WriteAllLines(Folder + "/Preview/Santorini Backdrop Visibility.txt", report);
            }
            finally
            {
                if (preview.IsValid() && preview.isLoaded)
                    EditorSceneManager.CloseScene(preview, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        static void RenderShot(Camera camera, Vector3 position, Quaternion rotation, float size, string path,
            List<string> report, string label)
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.orthographicSize = size;
            camera.aspect = 16f / 9f;
            const int width = 1920;
            const int height = 1080;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(image);
            }

            byte[] png = File.ReadAllBytes(path);
            var check = new Texture2D(2, 2, TextureFormat.RGB24, false);
            check.LoadImage(png);
            Vector3 ball = camera.WorldToViewportPoint(new Vector3(-14.55f, 8.95f, -9.55f));
            Vector3 tower = camera.WorldToViewportPoint(new Vector3(-15f, 8.3f, -9f));
            Color ballPixel = Sample(check, ball);
            Color towerPixel = Sample(check, tower);
            report.Add($"{label}: ortho {size:0.0}, ball viewport ({ball.x:0.000},{ball.y:0.000}) pixel {ballPixel}, tower-top viewport ({tower.x:0.000},{tower.y:0.000}) pixel {towerPixel}");
            report.Add($"  corners TL {Sample(check, new Vector3(0.02f, 0.98f))} TR {Sample(check, new Vector3(0.98f, 0.98f))} BL {Sample(check, new Vector3(0.02f, 0.02f))} BR {Sample(check, new Vector3(0.98f, 0.02f))}");
            UnityEngine.Object.DestroyImmediate(check);
        }

        static Color Sample(Texture2D image, Vector3 viewport)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(viewport.x * (image.width - 1)), 0, image.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(viewport.y * (image.height - 1)), 0, image.height - 1);
            return image.GetPixel(x, y);
        }

        static void CreateProxies(Scene scene)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var plaster = new Material(unlit) { name = "Preview Plaster", color = new Color(0.94f, 0.92f, 0.86f) };
            var gold = new Material(unlit) { name = "Preview Gold", color = new Color(0.95f, 0.64f, 0.24f) };
            var blue = new Material(unlit) { name = "Preview Dome", color = new Color(0.42f, 0.58f, 0.74f) };
            Proxy(scene, "Preview Tower", PrimitiveType.Cube, plaster, new Vector3(-15f, 4.15f, -9f), new Vector3(2.2f, 8.3f, 2.3f));
            Proxy(scene, "Preview Dome", PrimitiveType.Sphere, blue, new Vector3(-15f, 7.95f, -9f), new Vector3(1.45f, 0.36f, 1.45f));
            Proxy(scene, "Preview Ball", PrimitiveType.Sphere, gold, new Vector3(-14.55f, 8.95f, -9.55f), Vector3.one * 0.64f);
            Proxy(scene, "Preview Courtyard", PrimitiveType.Cube, plaster, new Vector3(2f, 0.3f, -1.5f), new Vector3(16f, 0.7f, 9f));
            Proxy(scene, "Preview Goal", PrimitiveType.Cube, plaster, new Vector3(24f, 1.2f, 6.8f), new Vector3(8f, 0.8f, 4.2f));
        }

        static void Proxy(Scene scene, string name, PrimitiveType shape, Material material, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(shape);
            SceneManager.MoveGameObjectToScene(obj, scene);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.name = name;
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            obj.transform.localScale = scale;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void Shell(Transform parent, string name, PrimitiveType shape, Material material, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(shape);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetLocalPositionAndRotation(position, rotation);
            obj.transform.localScale = scale;
            Quiet(obj.GetComponent<MeshRenderer>(), material);
        }

        static void Card(Transform parent, string name, Material material, float viewportX, float viewportY, float depth,
            Vector2 size)
        {
            Vector3 right = PreparationRotation * Vector3.right;
            Vector3 up = PreparationRotation * Vector3.up;
            Vector3 forward = PreparationRotation * Vector3.forward;
            const float aspect = 16f / 9f;
            Vector3 position = PreparationPosition + forward * depth
                + right * ((viewportX - 0.5f) * 2f * PreparationSize * aspect)
                + up * ((viewportY - 0.5f) * 2f * PreparationSize);
            var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.SetPositionAndRotation(position, PreparationRotation * Quaternion.Euler(0f, 180f, 0f));
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);
            Quiet(obj.GetComponent<MeshRenderer>(), material);
        }

        static void SetLayer(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayer(child.gameObject, layer);
        }

        static void Quiet(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static Material DistanceMaterial(string name, Texture2D texture, Color color, float opacity, float rise,
            float maskAmount, int queue, Vector2 offset)
        {
            var material = Material(Folder + "/Materials/" + name + ".mat", "BounceBack/Santorini Distance", color, queue);
            material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Rise", rise);
            material.SetFloat("_MaskAmount", maskAmount);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", offset);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Material(string path, string shaderName, Color color, int queue)
        {
            Shader shader = Shader.Find(shaderName);
            if (!shader)
                throw new InvalidOperationException("Missing shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D SaveTexture(string path, Texture2D texture, bool repeat)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = 512;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D PaintClouds(int size)
        {
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float swell = 0.78f + 0.22f * Mathf.Sin((u * 2f + v) * Mathf.PI * 2f);
                    byte alpha = (byte)Mathf.Clamp(swell * 255f, 0f, 255f);
                    pixels[y * size + x] = new Color32(255, 252, 247, alpha);
                }
            }
            image.SetPixels32(Blur(pixels, size, 7));
            image.Apply(false);
            return image;
        }

        static Texture2D PaintIsle(int size)
        {
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float mass = Gaussian(u, v, 0.38f, 0.42f, 0.20f, 0.16f)
                        + 0.72f * Gaussian(u, v, 0.62f, 0.48f, 0.16f, 0.13f)
                        + 0.40f * Gaussian(u, v, 0.50f, 0.30f, 0.24f, 0.08f);
                    byte alpha = (byte)Mathf.Clamp(Mathf.SmoothStep(0.12f, 0.55f, mass) * 255f, 0f, 255f);
                    pixels[y * size + x] = new Color32(244, 246, 248, alpha);
                }
            }
            image.SetPixels32(Blur(pixels, size, 5));
            image.Apply(false);
            return image;
        }

        static float Gaussian(float u, float v, float cx, float cy, float sx, float sy)
        {
            float dx = (u - cx) / sx;
            float dy = (v - cy) / sy;
            return Mathf.Exp(-(dx * dx + dy * dy));
        }

        static Color32[] Blur(Color32[] source, int size, int radius)
        {
            var horizontal = new Color32[source.Length];
            var result = new Color32[source.Length];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int a = 0, count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sample = Mathf.Clamp(x + k, 0, size - 1);
                        a += source[y * size + sample].a;
                        count++;
                    }
                    horizontal[y * size + x] = new Color32(255, 252, 247, (byte)(a / count));
                }
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int a = 0, count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sample = Mathf.Clamp(y + k, 0, size - 1);
                        a += horizontal[sample * size + x].a;
                        count++;
                    }
                    result[y * size + x] = new Color32(255, 252, 247, (byte)(a / count));
                }
            }
            return result;
        }
    }
}
