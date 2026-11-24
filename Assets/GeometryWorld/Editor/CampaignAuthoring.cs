using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static BounceBack.GeometryWorld.Editor.SantoriniAuthoring;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class CampaignAuthoring
    {
        internal const string MenuPath = Folder + "/Geometric Menu.unity";
        internal const string WorldPath = Folder + "/Floating City.unity";
        internal const string PrototypePath = Folder + "/Prototype Trial.unity";
        private const float BallRadius = 0.35f;
        private static readonly string[] DistrictNames = { "Santorini", "District 2", "District 3", "District 4" };

        [MenuItem("Tools/Bounce Back/Create Campaign Blockout")]
        public static void CreateCampaign()
        {
            Create();
            SaveNewScene(MenuPath, CreateMenu);
            SaveNewScene(WorldPath, CreateWorld);
            SaveNewScene(PrototypePath, CreatePrototype);
            var campaign = new[] { MenuPath, ScenePath, WorldPath, PrototypePath };
            var previous = EditorBuildSettings.scenes.Where(item => !campaign.Contains(item.path));
            EditorBuildSettings.scenes = campaign.Select(path => new EditorBuildSettingsScene(path, true)).Concat(previous).ToArray();
        }

        private static Transform Stage(Scene scene, string name, Vector3 focus, Vector3 offset, float size, out Camera camera)
        {
            var root = LevelAuthoring.NewObject(name, null, scene, Vector3.zero, false).transform;
            var lens = Child(root, "Isometric Camera", focus + offset);
            camera = lens.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.61f, 0.75f, 0.86f);
            lens.tag = "MainCamera";
            lens.transform.LookAt(focus);
            lens.AddComponent<AudioListener>();
            var sun = Child(root, "Key Light", Vector3.zero).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.68f, 0.74f, 0.85f);
            return root;
        }

        private static void CreateMenu(Scene scene)
        {
            var root = Stage(scene, "Menu", new Vector3(0f, 1f, 0f), new Vector3(8f, 10f, -16f), 7.8f, out var camera);
            var menu = root.gameObject.AddComponent<GeometricMenu>();
            menu.view = camera;
            var limestone = Plaster();
            var blue = Blue();
            var marker = Gold();
            var glow = Material("Menu Gold", new Color(0.91f, 0.49f, 0.16f));
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", Color.black);
            var stage = Child(root, "Menu Architecture", Vector3.zero).transform;
            var floor = Visual(PrimitiveType.Cube, stage, "Plaza", new Vector3(0f, -0.6f, -1.8f),
                new Vector3(12.4f, 0.95f, 5.1f), limestone).transform;

            var district = new WorldEntrance[CampaignProgress.LevelCount];
            for (int i = 0; i < district.Length; i++)
                district[i] = District(stage, i, new Vector3(-5.4f + 3.5f * i, 0f, 3.4f), limestone, blue, marker, false);
            menu.city = district;
            menu.devices = new MenuDevice[4];
            var rearrange = new List<PoseTrack> { Track(floor, new Vector3(0f, -0.7f, 0.8f)) };
            for (int i = 0; i < menu.devices.Length; i++)
            {
                float x = (i - 1.5f) * 2.7f;
                var position = new Vector3(x, 0.7f, -2.25f);
                var item = Child(stage, ((MenuAction)i).ToString(), position);
                item.AddComponent<BoxCollider>().size = new Vector3(2.05f, 2.2f, 1.9f);
                var device = item.AddComponent<MenuDevice>();
                device.action = (MenuAction)i;
                var baseMesh = Visual(PrimitiveType.Cylinder, item.transform, "Plinth", position - Vector3.up * 0.58f,
                    new Vector3(1.8f, 0.18f, 1.8f), blue);
                var form = Child(item.transform, "VisualRoot", position).transform;
                var center = Visual(i == 1 ? PrimitiveType.Cube : PrimitiveType.Sphere, form, "Core", position,
                    Vector3.one * (i == 3 ? 0.72f : 1f), glow);
                device.glowRenderer = center.GetComponent<Renderer>();
                var tracks = new List<PoseTrack> { Track(form, Vector3.up * 0.28f, new Vector3(0f, 30f, 0f), Vector3.one * 1.12f) };
                int pieces = i == 0 ? 6 : i == 3 ? 3 : 4;
                for (int piece = 0; piece < pieces; piece++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, piece * 360f / pieces, 0f);
                    Vector3 radial = rotation * Vector3.forward;
                    var blade = Visual(i == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, form, $"Panel {piece + 1}",
                        position + radial * 0.49f, new Vector3(0.28f, 0.22f, 0.73f), limestone).transform;
                    blade.rotation = rotation;
                    tracks.Add(Track(blade, radial * 0.33f + Vector3.up * 0.16f, new Vector3(-38f, 0f, 0f)));
                }
                device.unfolding = Pose(item, tracks);
                menu.devices[i] = device;
                rearrange.Add(Track(item.transform, new Vector3(x * 0.4f, i % 2 == 0 ? -0.6f : 0.5f, 1.3f),
                    new Vector3(0f, x * 8f, 0f)));
                baseMesh.name = "Plinth Visual";
            }
            menu.transition = Pose(root.gameObject, rearrange);
            menu.music = Music(stage, district.Select(item => Track(item.transform, Vector3.up * 0.38f,
                new Vector3(0f, 9f, 0f))).ToArray());

            var logo = Child(root, "Opening Logo", camera.transform.position + camera.transform.forward * 8f);
            logo.transform.rotation = camera.transform.rotation;
            var logoTracks = new List<PoseTrack>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 radial = Quaternion.AngleAxis(i * 72f, logo.transform.forward) * logo.transform.up;
                var tile = Visual(PrimitiveType.Cube, logo.transform, $"Logo Tile {i + 1}", logo.transform.position + radial * 0.46f,
                    new Vector3(0.42f, 0.42f, 0.32f), i % 2 == 0 ? marker : limestone).transform;
                tile.rotation = logo.transform.rotation * Quaternion.Euler(0f, 0f, i * 72f + 45f);
                logoTracks.Add(Track(tile, tile.localPosition.normalized * 1.35f, new Vector3(54f, 38f, 76f), Vector3.one * 0.12f));
            }
            menu.logo = logo;
            menu.logoMotion = Pose(logo, logoTracks);
        }

        private static void CreateWorld(Scene scene)
        {
            Vector3[] path =
            {
                new(0f, 0.35f, 0f), new(4f, 0.35f, 0f), new(4f, 1.55f, 5f), new(4f, 1.55f, 8f),
                new(1f, -1.45f, 11f), new(-3f, -1.45f, 11f), new(-3f, -0.20f, 16f), new(-3f, -0.20f, 19f)
            };
            var root = Stage(scene, "Floating City", path[0], new Vector3(10f, 10f, -10f), 7f, out var camera);
            var city = root.gameObject.AddComponent<FloatingCity>();
            city.view = camera;
            city.perspectiveSpan = 3;
            var plaster = Plaster();
            var blue = Blue();
            var gold = Gold();
            var walkways = Child(root, "Projecting Walkways", Vector3.zero).transform;
            city.waypoints = new Transform[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                city.waypoints[i] = Child(walkways, $"Waypoint {i:00}", path[i]).transform;
                if (i == 0 || i - 1 == city.perspectiveSpan)
                    continue;
                Slope(walkways, $"Walkway {i:00}", path[i - 1] - Vector3.up * BallRadius,
                    path[i] - Vector3.up * BallRadius, 1.65f, plaster);
            }
            foreach (int i in new[] { 0, 1, 2, 5, 6, 7 })
                Deck(walkways, $"Landing {i:00}", path[i] - Vector3.up * BallRadius, new Vector3(1.85f, 0.35f, 1.85f), blue);
            var portalRims = Child(root, "Perspective Crossing", Vector3.zero).transform;
            foreach (int i in new[] { 3, 4 })
                Visual(PrimitiveType.Cylinder, portalRims, $"Portal Rim {i}", path[i] - Vector3.up * 0.31f,
                    new Vector3(1.38f, 0.04f, 1.38f), gold);

            city.entrances = new WorldEntrance[CampaignProgress.LevelCount];
            int[] entryPoints = { 0, 2, 5, 7 };
            var buildings = Child(root, "Districts", Vector3.zero).transform;
            for (int i = 0; i < city.entrances.Length; i++)
                city.entrances[i] = District(buildings, i, path[entryPoints[i]], plaster, blue, gold, true);
            city.ball = Child(root, "Map Ball", path[0]).transform;
            city.ballVisual = Visual(PrimitiveType.Sphere, city.ball, "VisualRoot", path[0], Vector3.one * 0.7f, gold).transform;
            Visual(PrimitiveType.Cube, city.ballVisual, "Inset", path[0] + Vector3.up * 0.32f,
                new Vector3(0.26f, 0.09f, 0.26f), blue);
            city.ambienceMotion = Music(portalRims, new[] { Track(portalRims, new Vector3(0f, 0.12f, 0f)) });

            var finale = Child(root, "Floating Garden", new Vector3(-3f, -0.55f, 23f));
            city.finaleGarden = finale;
            city.gardenFloor = Deck(finale.transform, "Garden Deck", finale.transform.position,
                new Vector3(8f, 0.8f, 8f), plaster);
            var art = Child(finale.transform, "Interactive Sculpture", new Vector3(-3f, 0.6f, 23.8f));
            var leaves = Flower(art.transform, art.transform.position, gold);
            var finaleTracks = new List<PoseTrack> { Track(art.transform, Vector3.up * 1.1f, new Vector3(0f, 110f, 0f)) };
            var petals = new List<PoseTrack>();
            foreach (var leaf in leaves)
                petals.Add(Track(leaf, leaf.localPosition.normalized * 0.65f, new Vector3(50f, 0f, 0f)));
            Vector3 crossing = Vector3.Lerp(path[3], path[4], 0.5f) - Vector3.up * BallRadius;
            var finalBridge = Child(root, "Finale Bridge Visual", crossing);
            var bridgeMesh = Visual(PrimitiveType.Cube, finalBridge.transform, "Bridge", crossing - Vector3.up * 0.44f,
                new Vector3(1.55f, 0.46f, 0.02f), blue).transform;
            finalBridge.transform.rotation = Quaternion.LookRotation(path[4] - path[3]);
            finaleTracks.Add(Track(bridgeMesh, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 255f)));
            city.finaleTransform = Pose(finale, finaleTracks);
            city.gardenInteraction = Pose(art, petals);
            city.finaleSound = AddVoice(finale, 0.6f);
        }

        internal static WorldEntrance District(Transform parent, int index, Vector3 point, Material plaster, Material blue,
            Material gold, bool hasEntrance, Material wireMaterial = null)
        {
            var root = Child(parent, $"District {index + 1} - {DistrictNames[index]}", point);
            var region = root.AddComponent<WorldEntrance>();
            region.levelIndex = index;
            region.districtName = DistrictNames[index];
            region.ambientLight = Color.Lerp(new Color(0.72f, 0.78f, 0.92f), new Color(0.92f, 0.74f, 0.79f), index / 3f);
            Vector3 basePoint = point + (hasEntrance ? Vector3.left * 1.85f : Vector3.zero);
            var solid = Child(root.transform, "Solid Architecture", basePoint + Vector3.down * 0.35f).transform;
            var wire = Child(root.transform, "Locked Wireframe", solid.position);
            region.solid = solid;
            region.wire = wire;
            float height = 1.8f + index * 0.55f;
            var tower = Visual(PrimitiveType.Cube, solid, "Tower", solid.position + Vector3.up * height * 0.5f,
                new Vector3(1.9f, height, 1.8f), plaster).transform;
            var cap = Visual(index % 2 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube, solid, "Crown", solid.position + Vector3.up * height,
                new Vector3(2.04f, 0.38f, 1.9f), blue).transform;
            var beacon = Visual(PrimitiveType.Sphere, solid, "Completed Beacon", solid.position + Vector3.up * (height - 0.35f),
                Vector3.one * 0.28f, gold).transform;
            region.completedPose = Pose(solid.gameObject, new[] { Track(cap, Vector3.up * 0.34f, new Vector3(0f, 45f, 0f)),
                Track(beacon, Vector3.up * 1.21f, Vector3.zero, Vector3.one * 2.3f) });
            AddWireBox(wire.transform, height, tower.localScale, index, wireMaterial);
            if (hasEntrance)
            {
                Visual(PrimitiveType.Cylinder, root.transform, "Entry Marker", point - Vector3.up * 0.31f,
                    new Vector3(1.55f, 0.045f, 1.55f), gold);
                region.ambience = AddVoice(root, 0.42f);
            }
            return region;
        }

        private static void AddWireBox(Transform parent, float height, Vector3 size, int index, Material wireMaterial)
        {
            var ghost = wireMaterial ? wireMaterial : AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Wireframe.mat");
            if (!ghost)
            {
                ghost = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                    { name = "Wireframe", color = new Color(0.79f, 0.94f, 1f, 0.62f) };
                ghost.SetFloat("_Surface", 1f);
                ghost.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ghost.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                ghost.SetFloat("_ZWrite", 0f);
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ghost.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(ghost, Folder + "/Wireframe.mat");
            }
            Vector3 x = Vector3.right * size.x * 0.5f;
            Vector3 z = Vector3.forward * size.z * 0.5f;
            Vector3[] outline = { -x - z, -x + z, x + z, x - z, -x - z };
            AddLine(parent, "Bottom Outline", outline, ghost, 0.037f);
            AddLine(parent, "Top Outline", outline.Select(p => p + Vector3.up * height).ToArray(), ghost, 0.037f);
            for (int i = 0; i < 4; i++)
                AddLine(parent, $"Vertical {i + 1}", new[] { outline[i], outline[i] + Vector3.up * height }, ghost, 0.037f);
            var crown = new[] { -x - z, -x + z, x + z, x - z }
                .Select(p => p * 0.6f + Vector3.up * (height + 0.25f + index * 0.04f)).ToList();
            crown.Add(crown[0]);
            AddLine(parent, "Crown Outline", crown.ToArray(), ghost, 0.037f);
        }

        private static void CreatePrototype(Scene scene)
        {
            var level = LevelAuthoring.CreateInScene(scene, "Prototype Trial", false);
            level.allowRuntimeBuilding = false;
            level.playHint = "Shared placeholder for Levels 2–4. Press Space to roll to the marker.";
            level.spawn.transform.position = new Vector3(0f, 0.32f, -3.1f);
            level.spawn.launchSpeed = 4.5f;
            level.spawn.material = Gold();
            var platform = Child(level.transform, "Prototype Geometry", Vector3.zero).transform;
            Deck(platform, "Walkway", Vector3.zero, new Vector3(3f, 0.6f, 10f), Plaster());
            level.goal = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Goal, new Vector3(0f, 0.55f, 3.4f), false)
                .GetComponent<PuzzleTrigger>();
            Visual(PrimitiveType.Cylinder, level.goal.transform, "Goal Marker", new Vector3(0f, 0.08f, 3.4f),
                new Vector3(1.2f, 0.08f, 1.2f), Gold());
            var result = level.gameObject.AddComponent<CampaignLevelExit>();
            result.duration = 1.2f;
            result.useSelectedEntrance = true;
            result.completion = Pose(platform.gameObject, new[] { Track(level.goal.transform.GetChild(0), Vector3.up * 0.75f) });
        }

        internal static void AddTutorialCompletion(GeometryLevel level, Transform architecture, Material gold, Material blue)
        {
            Vector3 point = level.goal.transform.position;
            var presentation = Child(level.transform, "Completion Visuals", point);
            var bud = Child(presentation.transform, "Bloom", point + Vector3.down * 0.65f);
            var leaves = Flower(bud.transform, bud.transform.position, gold);
            var tracks = new List<PoseTrack>
            {
                Track(bud.transform, Vector3.up * 2.5f, new Vector3(0f, 55f, 0f)),
                Track(architecture, new Vector3(-0.7f, 0.9f, 0.6f), new Vector3(0f, 12f, 0f)),
                Track(level.gameplayCamera.transform, new Vector3(3.4f, 3.7f, -1.2f))
            };
            foreach (var leaf in leaves)
                tracks.Add(Track(leaf, leaf.localPosition.normalized * 0.75f, new Vector3(35f, 0f, 0f)));
            for (int i = 0; i < 3; i++)
            {
                var ring = Child(presentation.transform, $"Geometric Wave {i + 1}", point + Vector3.down * 0.63f);
                var vertices = Enumerable.Range(0, 13).Select(j => Quaternion.AngleAxis(j * 30f, Vector3.up) * Vector3.forward).ToArray();
                AddLine(ring.transform, "Ring Visual", vertices, i % 2 == 0 ? gold : blue, 0.04f);
                ring.transform.localScale = Vector3.one * 0.05f;
                tracks.Add(Track(ring.transform, Vector3.up * (0.35f + i * 0.25f),
                    new Vector3(0f, i * 5f, 0f), Vector3.one * (55f + i * 42f)));
            }
            var exit = level.gameObject.AddComponent<CampaignLevelExit>();
            exit.completion = Pose(presentation, tracks);
        }

        private static Transform[] Flower(Transform parent, Vector3 center, Material material)
        {
            var leaves = new Transform[6];
            for (int i = 0; i < leaves.Length; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, i * 60f, 0f);
                leaves[i] = Visual(PrimitiveType.Sphere, parent, $"Petal {i + 1}", center + rotation * Vector3.forward * 0.32f,
                    new Vector3(0.28f, 0.18f, 0.9f), material).transform;
                leaves[i].rotation = rotation;
            }
            return leaves;
        }

        internal static GeometryMusicLoop Music(Transform parent, PoseTrack[] tracks)
        {
            var obj = Child(parent, "Music and Markers", parent.position);
            var loop = obj.AddComponent<GeometryMusicLoop>();
            loop.duration = 40f;
            loop.layers = Enumerable.Range(0, 5).Select(i => AddVoice(Child(obj.transform, $"Music Layer {i + 1}", obj.transform.position), 0.28f)).ToArray();
            loop.visualMotion = Pose(obj, tracks);
            loop.markers = new[] { Marker(0f, "Opening", 0.45f), Marker(8f, "First Theme", 0.8f), Marker(16f, "Variation", 0.55f),
                Marker(20f, "Variation", 0.65f), Marker(24f, "Full Theme", 1f), Marker(32f, "Return", 0.68f) };
            return loop;
        }

        private static MusicMarker Marker(float time, string section, float intensity) => new()
            { time = time, section = section, intensity = intensity };

        private static AudioSource AddVoice(GameObject owner, float volume)
        {
            var voice = owner.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.volume = volume;
            return voice;
        }

        internal static void AddLine(Transform parent, string name, Vector3[] points, Material material, float width)
        {
            var line = Child(parent, name, parent.position).AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static GeometryPose Pose(GameObject owner, IEnumerable<PoseTrack> tracks)
        {
            var pose = owner.AddComponent<GeometryPose>();
            pose.tracks = tracks.ToArray();
            return pose;
        }

        private static PoseTrack Track(Transform target, Vector3 move, Vector3 turn = default, Vector3? scale = null) => new()
            { target = target, translation = move, rotation = turn, scale = scale ?? Vector3.one };

        private static Material Plaster() => Material("Lime Plaster", new Color(0.94f, 0.92f, 0.82f));
        private static Material Blue() => Material("Aegean Blue", new Color(0.20f, 0.45f, 0.73f));
        private static Material Gold() => Material("Rail Marker", new Color(0.98f, 0.63f, 0.20f));
    }
}
