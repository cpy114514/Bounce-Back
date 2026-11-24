using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MainMenuAuthoring
    {
        internal const string AssetRoot = "Assets/_Project/MainMenu";
        private const string SceneFolder = "Assets/Scenes";
        private const string MenuFontPath = AssetRoot + "/UI/Fonts/Marcellus-Regular.ttf";
        private const string LabelFontPath = AssetRoot + "/UI/Fonts/SourceSans3-Regular.ttf";
        private static readonly Color Pearl = new Color32(225, 211, 187, 255);
        private static readonly Color Brass = new Color32(179, 129, 68, 255);
        private static readonly Color Clay = new Color32(153, 136, 135, 255);
        private static readonly (Color Edging, Color Strata, MenuArtPalette.District District)[] Places =
        {
            (new Color32(160, 180, 174, 255), new Color32(103, 134, 143, 255), MenuArtPalette.District.Santorini),
            (new Color32(178, 164, 150, 255), new Color32(131, 117, 119, 255), MenuArtPalette.District.ClockTower),
            (new Color32(157, 170, 143, 255), new Color32(107, 134, 120, 255), MenuArtPalette.District.Pavilion),
            (new Color32(165, 179, 174, 255), new Color32(111, 135, 143, 255), MenuArtPalette.District.Habitat)
        };
        private static readonly string[] Names = { "Santorini", "Clock Tower", "Pavilion", "Habitat 67" };
        private static readonly Vector3[] Positions =
        {
            new(-5.06f, -3.58f, -2.70f),
            new(2.24f, -3.65f, 2.91f),
            new(-8.78f, 1.67f, -2.90f),
            new(8.60f, 3.79f, 2.94f)
        };

        [MenuItem("Tools/Bounce Back/Main Menu/Rebuild Generated Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Return to Edit Mode before rebuilding the main menu.");
            Scene original = SceneManager.GetActiveScene();
            Scene oldMenu = SceneManager.GetSceneByPath(CampaignScenes.MainMenuPath);
            Scene oldPreview = SceneManager.GetSceneByPath(CampaignScenes.ArrivalPreviewPath);
            if (string.IsNullOrEmpty(original.path) && original.isDirty || oldMenu.isDirty || oldPreview.isDirty)
                throw new InvalidOperationException("Save the unsaved scene changes before rebuilding the main menu.");

            GeometryAssets.EnsureFolder(SceneFolder);
            GeometryAssets.EnsureFolder(AssetRoot + "/Prefabs");
            GeometryAssets.EnsureFolder(AssetRoot + "/UI");
            MenuModel.LoadPalette();
            BuildQuality();
            var added = new List<Scene>();
            try
            {
                GenerateScene<MainMenu>(oldMenu, CampaignScenes.MainMenuPath, BuildMenu);
                GenerateScene<ArrivalPreview>(oldPreview, CampaignScenes.ArrivalPreviewPath, BuildPreview);
                RegisterBuildScenes();
                AssetDatabase.SaveAssets();
                Debug.Log("Main menu, arrival preview, five island prefabs, and graphics presets generated.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded)
                    SceneManager.SetActiveScene(original);
                for (int i = added.Count - 1; i >= 0; i--)
                    EditorSceneManager.CloseScene(added[i], true);
            }

            void GenerateScene<T>(Scene scene, string path, Action create) where T : Component
            {
                if (!scene.isLoaded)
                {
                    bool additive = !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                        additive ? NewSceneMode.Additive : NewSceneMode.Single);
                    if (additive) added.Add(scene);
                }
                SceneManager.SetActiveScene(scene);
                GameObject[] previous = scene.GetRootGameObjects().Where(root => root.TryGetComponent<T>(out _)).ToArray();
                create();
                foreach (var root in previous)
                    Object.DestroyImmediate(root);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException($"Could not save {path}.");
            }
        }

        private static void BuildMenu()
        {
            var root = new GameObject("Bounce Back Valley");
            var menu = root.AddComponent<MainMenu>();
            menu.overview = new MenuCameraPose(new(-.14f, .80f, .12f), new(30, -31, 0), 7.34f, 12.95f);
            menu.opening = new MenuCameraPose(new(-.42f, 1.23f, .17f), new(27, -36, 0), 8.32f, 14.30f);
            menu.general = new MenuCameraPose(Positions[2] + new Vector3(.04f, .49f, -1.67f), new(46, -10, 0), 4.73f, 4.85f);
            menu.audio = new MenuCameraPose(Positions[3] + new Vector3(.08f, .47f, -1.49f), new(45, -5, 0), 4.86f, 4.70f);
            menu.newGame = new MenuCameraPose(Positions[0] + new Vector3(.14f, 1.38f, -.15f), new(18, -36, 0), 1.61f, 2.04f);
            menu.continueGame = new MenuCameraPose(Positions[1] + new Vector3(.03f, 3.11f, -.20f), new(11, -18, 0), 1.40f, 1.79f);
            menu.menuCamera = BuildLighting(root.transform, menu.overview);
            AddSwallows(menu.menuCamera);

            var architecture = MenuModel.Child(root.transform, "Floating architecture");
            menu.islands = new MenuIsland[Names.Length];
            Action<MenuModel>[] models = { MenuSantorini.Build, MenuClockTower.Build, MenuPavilion.Build, MenuHabitat67.Build };
            for (int i = 0; i < Names.Length; i++)
                menu.islands[i] = BuildIsland(architecture, i, models[i]);
            menu.titleIsland = BuildTitleIsland(architecture);
            menu.generalDeck = MenuSettingsStage.Build(menu.islands[2].transform, false);
            menu.audioDeck = MenuSettingsStage.Build(menu.islands[3].transform, true);
            menu.generalControls = menu.generalDeck.controls;
            menu.audioControls = menu.audioDeck.controls;

            menu.atmosphere = AddDust(root.transform, menu.islands);
            Transform sound = MenuModel.Child(root.transform, "Audio routing");
            menu.music = NewAudioSource(MenuModel.Child(sound, "Music"), true);
            menu.effects = NewAudioSource(MenuModel.Child(sound, "Sound Effects"), false);
            menu.interactionCue = Resources.Load<AudioClip>("BounceBack/Audio/District Reveal Chime");
            var ui = MenuModel.Child(root.transform, "Menu captions and prompts").gameObject.AddComponent<MainMenuUI>();
            ui.menu = menu;
            ui.artwork = LoadArtwork();
            menu.presentation = ui;
        }

        private static MenuArtwork LoadArtwork()
        {
            string path = AssetRoot + "/UI/Menu Artwork.asset";
            var artwork = AssetDatabase.LoadAssetAtPath<MenuArtwork>(path);
            if (artwork)
                return artwork;
            artwork = ScriptableObject.CreateInstance<MenuArtwork>();
            artwork.displayFont = AssetDatabase.LoadAssetAtPath<Font>(MenuFontPath);
            artwork.labelFont = AssetDatabase.LoadAssetAtPath<Font>(LabelFontPath);
            AssetDatabase.CreateAsset(artwork, path);
            return artwork;
        }

        private static Transform BuildTitleIsland(Transform parent)
        {
            const string name = "Title Island";
            string path = $"{AssetRoot}/Prefabs/{name}.prefab";
            var root = new GameObject(name);
            var meshes = new MenuAssetMeshes(name);
            var model = new MenuModel(root.transform, meshes, 0, MenuArtPalette.District.Title);
            model.UseModel($"{AssetRoot}/Models/{name}.fbx");
            model.Baked(root.transform, name);
            root.AddComponent<MeshCollider>().sharedMesh = root.GetComponent<MeshFilter>().sharedMesh;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (!prefab)
                throw new InvalidOperationException($"Could not save {path}.");
            meshes.RemoveObsolete();
            var instance = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform;
            instance.SetLocalPositionAndRotation(new Vector3(-.61f, 3.61f, -.15f), Quaternion.Euler(0f, 162f, 0f));
            instance.localScale = Vector3.one * 1.2f;
            return instance;
        }

        private static Camera BuildLighting(Transform root, MenuCameraPose shot)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.73f, .76f, .75f);
            RenderSettings.ambientEquatorColor = new Color(.64f, .69f, .69f);
            RenderSettings.ambientGroundColor = new Color(.57f, .63f, .65f);
            RenderSettings.fog = false;
            Transform cameraObject = MenuModel.Child(root, "Orthographic camera");
            cameraObject.gameObject.tag = "MainCamera";
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            cameraObject.gameObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(195, 213, 212, 255);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            shot.Apply(camera);

            var assets = new MenuAssetMeshes("Environment");
            var m = new MenuModel(root, assets, 0);
            var backdrop = m.Box(cameraObject, "Paper sky backdrop", Vector3.forward * 75f,
                new(180, 120, .12f), Pearl).GetComponent<MeshRenderer>();
            backdrop.shadowCastingMode = ShadowCastingMode.Off;
            backdrop.receiveShadows = false;
            backdrop.sharedMaterial = ToolMaterial("Atmosphere", "BounceBack/Menu Sky");
            backdrop.sharedMaterial.SetColor("_Zenith", new Color32(176, 203, 210, 255));
            backdrop.sharedMaterial.SetColor("_Horizon", new Color32(214, 224, 209, 255));
            EditorUtility.SetDirty(backdrop.sharedMaterial);
            AddClouds(camera);

            var key = MenuModel.Child(root, "Warm key light").gameObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.transform.localRotation = Quaternion.Euler(48f, -39f, 0f);
            key.color = new Color(1f, .973f, .943f);
            key.intensity = .77f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .56f;
            key.shadowBias = .081f;
            key.shadowNormalBias = .31f;
            RenderSettings.sun = key;
            return camera;
        }

        private static void AddClouds(Camera camera)
        {
            const string path = "Assets/ThirdParty/PolyPizza/Menu Clouds.fbx";
            var models = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(mesh => mesh.name);
            var layer = MenuModel.Child(camera.transform, "Cloud layer").gameObject.AddComponent<MenuClouds>();
            layer.view = camera;
            var near = CloudMaterial("Cloud Ivory", new Color32(242, 241, 232, 255));
            var far = CloudMaterial("Cloud Mist", new Color32(211, 222, 223, 255));
            layer.clouds = new[]
            {
                Place("High cumulus", "Cumulus", new(.150f, .917f), .32f, .67f, new(7f, -16f, 0f), near),
                Place("Eastern cloud bank", "Cloud Bank", new(.866f, .813f), .27f, 1.6f, new(-6f, 12f, -3f), far),
                Place("Distant cloud bank", "Cloud Bank", new(.106f, .378f), .16f, 1.35f, new(2f, 164f, -5f), far)
            };
            layer.Frame();

            MenuClouds.Cloud Place(string name, string mesh, Vector2 viewport, float width, float verticalScale,
                Vector3 orientation, Material material)
            {
                var cloud = MenuModel.Child(layer.transform, name);
                cloud.localRotation = Quaternion.Euler(orientation);
                cloud.gameObject.AddComponent<MeshFilter>().sharedMesh = models[mesh];
                var renderer = cloud.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return new MenuClouds.Cloud { model = cloud, viewport = viewport, width = width, verticalScale = verticalScale };
            }

            Material CloudMaterial(string name, Color color)
            {
                var material = ToolMaterial(name, "Universal Render Pipeline/Simple Lit", color);
                material.SetColor("_SpecColor", Color.black);
                material.SetFloat("_Smoothness", 0f);
                EditorUtility.SetDirty(material);
                return material;
            }
        }

        private static void AddSwallows(Camera view)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "/Models/Swallow.fbx");
            if (!model)
                throw new InvalidOperationException("The swallow's Blender export is missing.");
            var flock = MenuModel.Child(view.transform, "Sky swallows").gameObject.AddComponent<MenuSwallows>();
            flock.view = view;
            flock.birds = new MenuSwallows.Bird[3];
            var ink = ToolMaterial("Swallow Ink", "Universal Render Pipeline/Unlit", new Color32(74, 102, 109, 255));
            for (int i = 0; i < flock.birds.Length; i++)
            {
                var bird = MenuModel.Child(flock.transform, $"Swallow {i + 1}");
                var silhouette = ((GameObject)PrefabUtility.InstantiatePrefab(model, bird)).transform;
                bird.localPosition = new Vector3(5.7f - i * 1.1f, 6.05f + .26f * i, 60f);
                bird.localScale = Vector3.one * (.27f - .035f * i);
                flock.birds[i] = new MenuSwallows.Bird
                {
                    root = bird,
                    farWing = silhouette.Find("Far wing"),
                    nearWing = silhouette.Find("Near wing")
                };
                if (!flock.birds[i].farWing || !flock.birds[i].nearWing)
                    throw new InvalidOperationException("The swallow export must include two independent wings.");
                foreach (var renderer in bird.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = ink;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }

        private static MenuIsland BuildIsland(Transform parent, int index, Action<MenuModel> architecture)
        {
            string name = Names[index];
            var root = new GameObject(name).transform;
            var island = root.gameObject.AddComponent<MenuIsland>();
            var meshes = new MenuAssetMeshes(name);
            var colors = Places[index];
            var model = new MenuModel(root, meshes, 164 + index * 193, colors.District);
            Platform(model, root, colors.Edging, colors.Strata, index);
            model.BeginArchitecture(index == 3 ? 180f : 0f);
            architecture(model);
            model.GatherStaticDetails();
            float top = index == 1 ? 5.34f : 3.97f;
            island.hitbox = MenuModel.Hitbox(root, new Vector3(0, (top - 1.25f) * .5f, 0),
                new Vector3(5.76f, top + 1.25f, 4.28f));
            island.label = MenuModel.Child(root, "Hover caption anchor", new Vector3(.18f, -.98f, -2.82f));
            model.Finish(island);
            string path = $"{AssetRoot}/Prefabs/{name} Island.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            if (!prefab)
                throw new InvalidOperationException($"Could not save {path}.");
            meshes.RemoveObsolete();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Positions[index];
            if (index == 0)
                instance.transform.localScale = Vector3.one * 1.1f;
            return instance.GetComponent<MenuIsland>();
        }

        private static void Platform(MenuModel model, Transform root, Color edging, Color strata, int index)
        {
            Vector2[] footprint = { new(-.79f, -1), new(.69f, -1), new(1, -.67f), new(1, .64f),
                new(.69f, 1), new(-.80f, 1), new(-1, .67f), new(-1, -.70f) };
            model.Mesh(root, "Upper carved limestone", Vector3.zero,
                Rock(footprint, (0f, 2.75f, 2.24f), (-.10f, 2.87f, 2.32f), (-.32f, 2.87f, 2.32f)), Pearl);
            model.Mesh(root, "Recessed stone shelf", Vector3.zero,
                Rock(footprint, (-.32f, 2.80f, 2.26f), (-.55f, 2.70f, 2.17f)), edging);
            model.Mesh(root, "Tapered floating strata", new Vector3(0f, -.55f, 0f),
                Rock(footprint, (0f, 2.70f, 2.17f), (-.47f, 2.50f, 1.99f),
                    (-1.23f - index * .07f, 1.54f, 1.18f)), strata);
        }

        private static Mesh Rock(IReadOnlyList<Vector2> footprint, params (float y, float x, float z)[] sections)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 Point(int section, int edge) =>
                new(footprint[edge].x * sections[section].x, sections[section].y, footprint[edge].y * sections[section].z);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int offset = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 });
            }
            for (int ring = 0; ring < sections.Length - 1; ring++)
            for (int edge = 0; edge < footprint.Count; edge++)
            {
                int next = (edge + 1) % footprint.Count;
                Face(Point(ring, edge), Point(ring, next), Point(ring + 1, next), Point(ring + 1, edge));
            }
            for (int edge = 1; edge < footprint.Count - 1; edge++)
            {
                int offset = vertices.Count;
                int last = sections.Length - 1;
                vertices.AddRange(new[] { Point(0, 0), Point(0, edge + 1), Point(0, edge),
                    Point(last, 0), Point(last, edge), Point(last, edge + 1) });
                for (int i = 0; i < 6; i++) triangles.Add(offset + i);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static MenuAtmosphere AddDust(Transform root, MenuIsland[] islands)
        {
            var target = MenuModel.Child(root, "Mineral dust").gameObject;
            var atmosphere = target.AddComponent<MenuAtmosphere>();
            var dust = target.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.maxParticles = MenuAtmosphere.Capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var emission = dust.emission;
            emission.enabled = false;
            var renderer = target.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var mesh = new Mesh();
            GeometryGenerators.Generate(GeometryShape.Sphere, Vector3.one, 5, 0).WriteMesh(mesh, 1);
            renderer.mesh = new MenuAssetMeshes("Particles").Store(mesh, "Faceted speck");
            renderer.sharedMaterial = ToolMaterial("Mineral Dust", "Universal Render Pipeline/Unlit",
                new Color32(192, 161, 124, 255));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            atmosphere.dust = dust;
            atmosphere.islands = islands;
            return atmosphere;
        }

        private static Material ToolMaterial(string name, string shader, Color? color = null)
        {
            string path = $"{GeometryAssets.EnsureFolder(AssetRoot + "/Materials")}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (color.HasValue) material.color = color.Value;
            return material;
        }

        private static AudioSource NewAudioSource(Transform node, bool looping)
        {
            var source = node.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = looping;
            source.spatialBlend = 0f;
            return source;
        }

        private static void BuildPreview()
        {
            var root = new GameObject("Arrival Preview");
            BuildLighting(root.transform, new MenuCameraPose(new(0, .6f, 0), new(29, -29, 0), 4.2f, 4.6f));
            var preview = root.AddComponent<ArrivalPreview>();
            var artwork = LoadArtwork();
            preview.displayFont = artwork.displayFont;
            preview.labelFont = artwork.labelFont;
            var m = new MenuModel(root.transform, new MenuAssetMeshes("Arrival Preview"), 145);
            m.Cylinder(root.transform, "Stone base", new(0, -.71f, 0), 1.65f, .47f, Clay, 8);
            m.Cylinder(root.transform, "Ivory terrace", new(0, -.39f, 0), 1.62f, .18f, Pearl, 8);
            m.Cylinder(root.transform, "Central medallion", new(0, -.26f, 0), .62f, .065f, Brass, 20);
            preview.sphere = m.Sphere(root.transform, "The timekeeper", new(0, .52f, 0), Vector3.one * .55f, Brass);
        }

        private static void RegisterBuildScenes()
        {
            string[] incoming = { CampaignScenes.MainMenuPath, CampaignScenes.ArrivalPreviewPath };
            var entries = incoming.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (incoming.Contains(entry.path))
                    continue;
                string resolved = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrEmpty(resolved))
                {
                    bool archived = resolved.StartsWith("Assets/Scenes/Archive/", StringComparison.Ordinal);
                    entries.Add(new EditorBuildSettingsScene(resolved, entry.enabled && !archived));
                }
                else
                    entries.Add(entry);
            }
            EditorBuildSettings.scenes = entries.ToArray();
        }

        private static void BuildQuality()
        {
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = settings.FindProperty("m_QualitySettings");
            foreach (var spec in new (string name, UniversalRenderPipelineAsset src, float scale, int samples, int shadow, int distance, bool soft)[]
            { ("Low", mobile, .76f, 1, 512, 35, false),
                ("Medium", mobile, .90f, 2, 1024, 43, true), ("High", pc, 1f, 4, 2048, 58, true) })
            {
                string name = "Menu " + spec.name;
                string path = $"{GeometryAssets.EnsureFolder(AssetRoot + "/Settings")}/{name}.asset";
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!pipeline)
                {
                    pipeline = Object.Instantiate(spec.src);
                    pipeline.name = name;
                    AssetDatabase.CreateAsset(pipeline, path);
                }
                var asset = new SerializedObject(pipeline);
                asset.FindProperty("m_RenderScale").floatValue = spec.scale;
                asset.FindProperty("m_MSAA").intValue = spec.samples;
                asset.FindProperty("m_ShadowDistance").floatValue = spec.distance;
                asset.FindProperty("m_MainLightShadowmapResolution").intValue = spec.shadow;
                asset.FindProperty("m_SoftShadowsSupported").boolValue = spec.soft;
                asset.FindProperty("m_MainLightRenderingMode").intValue = 1;
                asset.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                asset.FindProperty("m_AdditionalLightsRenderingMode").intValue = 0;
                asset.ApplyModifiedPropertiesWithoutUndo();
                int index = 0;
                while (index < levels.arraySize && levels.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue != name)
                    index++;
                if (index == levels.arraySize)
                    levels.InsertArrayElementAtIndex(index);
                SerializedProperty level = levels.GetArrayElementAtIndex(index);
                level.FindPropertyRelative("name").stringValue = name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                level.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
                level.FindPropertyRelative("antiAliasing").intValue = spec.samples == 1 ? 0 : spec.samples;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
