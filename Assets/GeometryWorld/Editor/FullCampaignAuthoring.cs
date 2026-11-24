using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static BounceBack.GeometryWorld.Editor.SantoriniAuthoring;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class FullCampaignAuthoring
    {
        private const string FolderPath = "Assets/Scenes/Archive/BounceBackCampaign";
        private const string BootPath = FolderPath + "/Campaign Boot.unity";
        private const string HubPath = FolderPath + "/Campaign City.unity";
        private const string SunlessPath = "Assets/Scenes/Archive/BounceBack/The Land Without the Sun.unity";
        private const float Radius = 0.32f;
        private static readonly Vector3 CameraOffset = new(10f, 12f, -15f);

        [MenuItem("Tools/Bounce Back/Create Missing Campaign Shell")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath))
                AssetDatabase.CreateFolder("Assets/Scenes", "BounceBackCampaign");
            CreateBoot();
            SaveNewScene(HubPath, PopulateCity);
            RegisterScenes();
        }

        private static void CreateBoot()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath))
                return;
            if (!AssetDatabase.CopyAsset(CampaignAuthoring.MenuPath, BootPath))
                throw new InvalidOperationException("Could not copy the geometric menu into the campaign boot scene.");
            Scene scene = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Additive);
            var menu = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<GeometricMenu>()).First(item => item);
            menu.campaignBoot = true;
            menu.devices[1].action = MenuAction.Start;
            if (menu.music)
                menu.music.beatsPerMinute = 90f;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the campaign boot scene.");
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void RegisterScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != BootPath && scene.path != HubPath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootPath, true));
            scenes.Add(new EditorBuildSettingsScene(HubPath, true));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SunlessPath))
            {
                scenes.RemoveAll(scene => scene.path == SunlessPath);
                scenes.Add(new EditorBuildSettingsScene(SunlessPath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void PopulateCity(Scene scene)
        {
            var root = LevelAuthoring.NewObject("Campaign City", null, scene, Vector3.zero, false).transform;
            var flow = root.gameObject.AddComponent<CampaignHub>();
            var plaster = Surface("Ivory Stone", new Color(0.93f, 0.91f, 0.85f));
            var gold = Surface("Amber Metal", new Color(0.86f, 0.54f, 0.22f));
            var blue = Surface("Harbor Blue", new Color(0.28f, 0.51f, 0.65f));
            var pink = Surface("Rose Clay", new Color(0.76f, 0.57f, 0.57f));
            var cameraObject = Child(root, "Isometric Camera", CameraOffset);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.60f, 0.71f, 0.80f);
            camera.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<AudioListener>();
            flow.cameraRig = root.gameObject.AddComponent<PuzzleCameraRig>();
            flow.cameraRig.view = camera;
            var sun = Child(root, "Key Light", Vector3.zero).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.28f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.73f, 0.77f, 0.84f);

            var rail = Child(root, "Level Selection", Vector3.zero).AddComponent<CityRail>();
            flow.city = rail;
            flow.audioDirector = root.gameObject.AddComponent<AdaptivePuzzleAudio>();
            rail.cameraOffset = CameraOffset;
            rail.cameraSize = 6.3f;
            rail.requireUnlockedDestination = true;
            rail.persistNavigationHint = true;
            var road = Child(rail.transform, "Route Anchors", Vector3.zero).transform;
            Vector3[] path =
            {
                new(0f, Radius, 0f), new(0f, Radius, 3f), new(2f, 1.1f, 5.2f),
                new(2f, 1.1f, 8f), new(4.2f, 1.7f, 10.7f), new(4.2f, 1.7f, 14f),
                new(1.9f, 0.8f, 16.5f), new(1.9f, 0.8f, 19.8f)
            };
            rail.waypoints = path.Select((point, i) => Child(road, $"Road Node {i + 1}", point).transform).ToArray();
            rail.roadAssembly = CreateRoad(rail.transform, path, plaster, blue);
            rail.districts = new DistrictMaterialization[CampaignProgress.LevelCount];
            flow.destinations = new CampaignLevelDestination[rail.districts.Length];
            string[] names = { "Santorini", "The Land Without the Sun", "The Ivory Fold", "The Golden Terrace" };
            string[] levelScenes = { "Assets/Scenes/Archive/BounceBackSystems/Santorini Gameplay.unity", SunlessPath };
            var docks = new[] { 0, 2, 4, 7 };
            for (int i = 0; i < rail.districts.Length; i++)
            {
                rail.districts[i] = CreateDistrict(rail.transform, i, path[docks[i]], names[i], plaster, i % 2 == 0 ? blue : pink, gold);
                flow.destinations[i] = new CampaignLevelDestination
                {
                    scenePath = i < levelScenes.Length ? levelScenes[i] : string.Empty
                };
            }

            var globe = Visual(PrimitiveType.Sphere, root, "Ball", path[0], Vector3.one * (Radius * 2f), gold);
            globe.AddComponent<Rigidbody>().isKinematic = true;
            flow.ball = globe.AddComponent<BallBody>();
            Visual(PrimitiveType.Cube, globe.transform, "Enamel Inlay", path[0] + Vector3.up * (Radius * 0.985f),
                new Vector3(0.16f, 0.035f, 0.16f), blue);
            flow.carrier = Child(root, "Ball Carrier", Vector3.zero).transform;
            Visual(PrimitiveType.Cube, flow.carrier, "Platform Visual", Vector3.down * 0.12f, new Vector3(1.14f, 0.24f, 1.14f), blue);
            flow.carrier.gameObject.SetActive(false);
            CreateDemonstration(flow, root, plaster, pink, gold);
        }

        private static DistrictMaterialization CreateDistrict(Transform parent, int index, Vector3 dock, string title,
            Material stone, Material accent, Material gold)
        {
            var entrance = CampaignAuthoring.District(parent, index, dock, stone, accent, gold, true);
            entrance.districtName = title;
            var district = entrance.gameObject.AddComponent<DistrictMaterialization>();
            district.entrance = entrance;
            district.carrierAnchor = Child(entrance.transform, "Dock", dock).transform;
            Vector3 foundation = dock + new Vector3(-1.85f, -0.53f, 0f);
            Visual(PrimitiveType.Cube, entrance.solid, "Foundation", foundation, new Vector3(2.65f, 0.37f, 2.5f), stone).transform.SetAsFirstSibling();
            Vector3 awning = foundation + new Vector3(0f, 1.85f + index * 0.42f, 0.96f);
            Visual(PrimitiveType.Cube, entrance.solid, "Cantilever", awning, new Vector3(1.55f + index * 0.23f, 0.27f, 0.8f), accent);
            var meshes = entrance.solid.Cast<Transform>().ToArray();
            district.layers = new Transform[meshes.Length];
            var tracks = new PoseTrack[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                Transform mesh = meshes[i];
                Vector3 pivot = mesh.position - Vector3.up * (mesh.localScale.y * 0.5f);
                Transform layer = Child(entrance.solid, $"Layer {i + 1:00}", pivot).transform;
                mesh.SetParent(layer, true);
                district.layers[i] = layer;
                tracks[i] = new PoseTrack
                {
                    target = layer, translation = new Vector3(-(i % 2 == 0 ? 0.7f : -0.45f), 0.45f * i, 0.12f * i),
                    rotation = new Vector3(0f, (i % 2 == 0 ? -1f : 1f) * 24f, i * 7f),
                    phaseStart = i * 0.06f
                };
            }
            district.unfolding = Pose(entrance.transform, tracks);
            Light reveal = Child(entrance.transform, "Reveal Light", foundation + Vector3.up * 1.5f).AddComponent<Light>();
            reveal.type = LightType.Point;
            reveal.color = new Color(1f, 0.84f, 0.59f);
            reveal.range = 4.5f;
            reveal.intensity = 0f;
            district.revealLight = reveal;
            var plaque = Child(entrance.transform, "Name Plaque", dock + new Vector3(-1.71f, 0.28f, -1.0f)).transform;
            plaque.rotation = Quaternion.LookRotation(-CameraOffset);
            Visual(PrimitiveType.Cube, plaque, "Plaque Face", plaque.position, new Vector3(2.4f, 0.41f, 0.09f), accent).transform.rotation = plaque.rotation;
            var label = Child(plaque, "Carved Name", plaque.position - plaque.forward * 0.055f).AddComponent<TextMesh>();
            label.transform.rotation = plaque.rotation;
            label.text = title;
            label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 72;
            label.characterSize = 0.084f;
            label.color = new Color(1f, 0.95f, 0.83f);
            return district;
        }

        private static GeometryPose CreateRoad(Transform parent, Vector3[] path, Material stone, Material accent)
        {
            var road = Child(parent, "Road Assembly", Vector3.zero).transform;
            var pieces = new List<Transform>();
            for (int i = 1; i < path.Length; i++)
            {
                Vector3 a = path[i - 1] - Vector3.up * Radius;
                Vector3 b = path[i] - Vector3.up * Radius;
                int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 1.4f);
                Vector3 delta = (b - a) / count;
                Quaternion angle = Quaternion.LookRotation(delta);
                for (int j = 0; j < count; j++)
                {
                    var tile = Visual(PrimitiveType.Cube, road, $"Road Tile {pieces.Count + 1:00}",
                        a + delta * (j + 0.5f) - angle * Vector3.up * 0.15f,
                        new Vector3(1.65f, 0.3f, delta.magnitude + 0.045f), i % 3 == 0 ? accent : stone).transform;
                    tile.rotation = angle;
                    pieces.Add(tile);
                }
                if (i < path.Length - 1)
                    pieces.Add(Visual(PrimitiveType.Cylinder, road, $"Landing {i:00}", b - Vector3.up * 0.1f,
                        new Vector3(1.72f, 0.10f, 1.72f), accent).transform);
            }
            var tracks = new PoseTrack[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                Transform piece = pieces[i];
                Vector3 offset = new(i % 2 == 0 ? -1.9f : 1.55f, -2.3f - i % 3 * 0.42f, -0.9f);
                piece.localPosition += offset;
                piece.localRotation *= Quaternion.Euler(0f, -24f, 0f);
                piece.localScale *= 0.07f;
                float start = i * 0.71f / pieces.Count;
                tracks[i] = new PoseTrack
                {
                    target = piece, translation = -offset, rotation = new Vector3(0f, 24f, 0f), scale = Vector3.one / 0.07f,
                    phaseStart = start, phaseEnd = start + 0.26f, lift = 0.43f,
                    easing = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.77f, 1.035f), new Keyframe(1f, 1f))
                };
            }
            return Pose(road, tracks);
        }

        private static void CreateDemonstration(CampaignHub flow, Transform parent, Material stone, Material accent, Material gold)
        {
            Vector3 origin = new Vector3(6.8f, 2.05f, -3.6f);
            var stage = Child(parent, "Systems Demonstration", origin).transform;
            flow.demonstration = stage;
            flow.trialStart = Child(stage, "Preparation Anchor", origin + new Vector3(0f, Radius, -2.4f)).transform;
            flow.trialEnd = Child(stage, "Completion Anchor", origin + new Vector3(0f, Radius, 2.3f)).transform;
            flow.trialView = Child(stage, "Overview Shot", origin + new Vector3(9f, 11f, -13f)).transform;
            flow.trialView.LookAt(origin + Vector3.up * 0.8f);
            var pieces = new[]
            {
                Visual(PrimitiveType.Cube, stage, "Trial Deck", origin - Vector3.up * 0.23f, new Vector3(3.35f, 0.46f, 6.35f), stone).transform,
                Visual(PrimitiveType.Cube, stage, "Left Pier", origin + new Vector3(-1.65f, 1.55f, 0.55f), new Vector3(0.62f, 3.1f, 0.75f), stone).transform,
                Visual(PrimitiveType.Cube, stage, "Right Pier", origin + new Vector3(1.65f, 1.55f, 0.55f), new Vector3(0.62f, 3.1f, 0.75f), stone).transform,
                Visual(PrimitiveType.Cube, stage, "Lintel", origin + new Vector3(0f, 2.94f, 0.55f), new Vector3(4.05f, 0.44f, 0.91f), accent).transform,
                Visual(PrimitiveType.Cylinder, stage, "Demonstration Marker", flow.trialEnd.position - Vector3.up * (Radius - 0.025f),
                    new Vector3(1.35f, 0.025f, 1.35f), gold).transform
            };
            var tracks = new PoseTrack[pieces.Length];
            for (int i = 0; i < tracks.Length; i++)
            {
                Vector3 offset = new(i % 2 == 0 ? 1.2f : -1.5f, -2.6f, 0f);
                pieces[i].localPosition += offset;
                pieces[i].localScale *= 0.1f;
                tracks[i] = new PoseTrack { target = pieces[i], translation = -offset, scale = Vector3.one * 10f, phaseStart = i * 0.09f };
            }
            flow.trialArchitecture = Pose(stage, tracks);
            var ripple = Child(stage, "Completion Ripple", flow.trialEnd.position - Vector3.up * (Radius - 0.037f)).transform;
            Vector3[] ring = Enumerable.Range(0, 9).Select(i => Quaternion.AngleAxis(i * 45f, Vector3.up) * Vector3.forward).ToArray();
            CampaignAuthoring.AddLine(ripple, "Octagon", ring, gold, 0.044f);
            ripple.localScale = Vector3.one * 0.04f;
            flow.completionRipple = Pose(ripple, new[]
            {
                new PoseTrack { target = ripple, translation = Vector3.up * 0.55f, scale = Vector3.one * 36f }
            });
            ripple.gameObject.SetActive(false);
            stage.gameObject.SetActive(false);
        }

        private static GeometryPose Pose(Transform owner, PoseTrack[] tracks)
        {
            var pose = owner.gameObject.AddComponent<GeometryPose>();
            pose.tracks = tracks;
            return pose;
        }

        private static Material Surface(string name, Color color)
        {
            string path = $"{FolderPath}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
                return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
