using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BounceBack.GeometryWorld.Editor.SantoriniAuthoring;
using static BounceBack.GeometryWorld.Editor.SantoriniSystemsAuthoring;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class CitySystemsAuthoring
    {
        private static readonly Vector3[] Path =
        {
            FinaleChoreographyData.Dock, new(12.03f, 0.86f, 3.83f), new(14.6f, 1.54f, 4.50f),
            new(15.7f, 1.54f, 4.50f), new(14.74f, 0.58f, 5.46f)
        };
        private const int GeometricGap = 3;
        private const float Radius = 0.38f;
        private const float PerspectiveSeamLength = FinaleChoreographyData.SeamLength;
        private const float PerspectiveSeamDegrees = FinaleChoreographyData.SeamDegrees;

        internal static void Attach(GeometryLevel level, PuzzleCameraRig rig, GameObject physics, GameObject mechanisms,
            Transform art, Material white, Material blue, Material gold)
        {
            var flow = level.gameObject.AddComponent<CampaignPlayground>();
            flow.level = level;
            flow.rig = rig;
            Vector3 dock = Path[0];
            flow.startingCarrier = Child(level.transform, "Starting Platform Carrier", Vector3.zero).transform;
            Visual(PrimitiveType.Cube, flow.startingCarrier, "Carrier Visual Slot", new Vector3(0f, -0.16f, 0f),
                new Vector3(1.20f, 0.32f, 1.34f), white);
            flow.startingCarrier.gameObject.SetActive(false);
            flow.carrierOverlook = Child(level.transform, "Carrier Windward Anchor", new Vector3(3.9f, 3.7f, -3.2f)).transform;
            flow.carrierTower = Child(level.transform, "Carrier Tower Anchor", new Vector3(-4.45f, 6.15f, -4.1f)).transform;

            var city = Child(level.transform, "Level Selection World", Vector3.zero).AddComponent<CityRail>();
            flow.city = city;
            city.firstDistrictOnly = true;
            city.crossingSegment = GeometricGap;
            city.crossingDuration = FinaleChoreographyData.SeamDuration;
            city.cameraSize = 4.4f;
            city.cameraOffset = Quaternion.AngleAxis(11f, Vector3.up) * new Vector3(15f, 15f, -15f);
            // Short, nearly axial crossing: about 10 degrees to the city sight axis and under 4 px at 1080p.
            Path[GeometricGap] = new Vector3(15.7f, 1.54f, 4.50f);
            Path[GeometricGap + 1] = new Vector3(14.74f, 0.58f, 5.46f);
            Vector3 sight = city.cameraOffset.normalized;
            Vector3 oldDelta = Path[GeometricGap + 1] - Path[GeometricGap];
            Vector3 across = Vector3.ProjectOnPlane(oldDelta, sight).normalized;
            Path[GeometricGap + 1] = Path[GeometricGap] + PerspectiveSeamLength *
                (-sight * Mathf.Cos(PerspectiveSeamDegrees * Mathf.Deg2Rad) + across * Mathf.Sin(PerspectiveSeamDegrees * Mathf.Deg2Rad));
            var route = Child(city.transform, "Road Waypoints", Vector3.zero).transform;
            city.waypoints = Path.Select((point, i) => Child(route, $"Road Node {i:00}", point).transform).ToArray();
            city.roadAssembly = Road(city.transform, dock, white, blue, gold);

            var districts = Child(city.transform, "District Model Anchors", Vector3.zero).transform;
            var wire = WireMaterial();
            city.districts = new[]
            {
                Entrance(districts, 0, dock, city.cameraOffset, white, blue, gold, wire),
                Entrance(districts, 1, Path[^1], city.cameraOffset, white, blue, gold, wire)
            };
            var gate = city.districts[1].entrance;
            gate.districtName = "Level 2";
            gate.transform.Find("Carved Name").GetComponent<TextMesh>().text = gate.districtName;
            Transform door = Child(gate.transform, "Locked Door Outline", Path[^1] + new Vector3(-0.30f, -Radius, 0.69f)).transform;
            Vector3[] outline = { new(-0.74f, 0f, 0f), new(-0.74f, 1.28f, 0f), new(-0.52f, 1.73f, 0f),
                new(0f, 1.97f, 0f), new(0.52f, 1.73f, 0f), new(0.74f, 1.28f, 0f), new(0.74f, 0f, 0f) };
            door.rotation = Quaternion.LookRotation(-city.cameraOffset);
            CampaignAuthoring.AddLine(door, "Sealed Gate", outline, wire, 0.085f);
            city.music = CampaignAuthoring.Music(city.transform, System.Array.Empty<PoseTrack>());

            Transform goal = level.goal.transform;
            mechanisms.transform.SetParent(art, true);
            goal.SetParent(art, true);
            var plate = goal.GetComponent<GoalPressurePlate>();
            flow.completionPlate = plate;
            float miniScale = 0.16f;
            Vector3 miniPlatePosition = dock - Vector3.up * Radius;
            Vector3 plateCenter = plate.surface.position - Vector3.up * plate.depression;
            var fold = new PoseTrack
            {
                target = art,
                translation = miniPlatePosition - plateCenter * miniScale,
                scale = Vector3.one * miniScale
            };
            string[] anchorNames = { "Tower Art", "Staircase Art", "Courtyard and Water Art", "Goal Terrace Art" };
            Vector3[] miniatureOffsets = { new(-1.5f, 0.1f, -0.8f), new(0.2f, 0.2f, -1.25f),
                new(-1.1f, 0f, 1.15f), Vector3.zero };
            var tracks = new List<PoseTrack> { fold };
            for (int i = 0; i < anchorNames.Length; i++)
            {
                Transform anchor = art.Find(anchorNames[i]);
                if (!anchor)
                    throw new System.InvalidOperationException("Missing finale anchor: " + anchorNames[i]);
                tracks.Add(new PoseTrack
                {
                    target = anchor, translation = miniatureOffsets[i], rotation = new Vector3(0f, i % 2 == 0 ? 18f : -18f, 0f),
                    scale = Vector3.one, phaseStart = i * 0.10f, phaseEnd = 0.72f + i * 0.093f, overshoot = 0.055f
                });
            }
            var folding = Pose(art.gameObject, tracks);
            flow.santorini = new CampaignPlayStage
            {
                root = level.transform, physics = physics, mechanisms = mechanisms, goalDisplay = goal.gameObject,
                spawn = level.spawn, goal = level.goal, overview = rig.overview, overviewSize = rig.overviewSize,
                followSize = rig.followSize, folding = folding
            };
            flow.finale = level.gameObject.AddComponent<CourtyardFinale>();
            flow.finale.folding = folding;
            flow.finale.plateHold = FinaleChoreographyData.PlateHold;
            flow.finale.waterfallPause = FinaleChoreographyData.WaterfallPause;
            flow.finale.splashDuration = FinaleChoreographyData.SplashDuration;
            flow.finale.anchorFoldDuration = FinaleChoreographyData.AnchorFoldDuration;
            flow.finale.anchorStagger = FinaleChoreographyData.AnchorStagger;
            flow.finale.overshoot = FinaleChoreographyData.Overshoot;
            flow.finale.foldingCameraSize = FinaleChoreographyData.CameraSize;
            flow.finale.roadRevealDuration = FinaleChoreographyData.RoadRevealDuration;
            flow.finale.followTransitionDuration = FinaleChoreographyData.FollowTransitionDuration;
            flow.finale.rolloutDuration = FinaleChoreographyData.RolloutDuration;
            flow.finale.waterfall = art.Find("Goal Terrace Art/Waterfall Slot");
            flow.finale.splashMaterial = AssetDatabase.LoadAssetAtPath<Material>(SystemsFolder + "/Water Highlight.mat");
            city.gameObject.SetActive(false);
        }
        private static GeometryPose Road(Transform parent, Vector3 join, Material white, Material blue, Material gold)
        {
            var assembly = Child(parent, "Assembling Road", Vector3.zero);
            var pieces = new List<Transform>();
            AddPieces(join - Vector3.right * 0.75f, join + Vector3.right * 0.1f, pieces, assembly.transform, white);
            for (int i = 0; i < Path.Length - 1; i++)
                if (i != GeometricGap)
                    AddPieces(Path[i], Path[i + 1], pieces, assembly.transform, i % 3 == 0 ? blue : white);
            for (int i = GeometricGap; i <= GeometricGap + 1; i++)
                pieces.Add(Visual(PrimitiveType.Cylinder, assembly.transform, $"Perspective Rim {i}", Path[i] - Vector3.up * 0.345f,
                    new Vector3(1.58f, 0.075f, 1.58f), gold).transform);
            var tracks = new List<PoseTrack>();
            for (int i = 0; i < pieces.Count; i++)
            {
                Transform piece = pieces[i];
                Vector3 offset = new Vector3(i % 2 == 0 ? 1.75f : -1.85f, -3.2f - (i % 3) * 0.4f, -1.85f);
                piece.localPosition += offset;
                piece.Rotate(0f, -36f, 0f, Space.Self);
                piece.localScale *= 0.22f;
                float begin = i * 0.78f / pieces.Count;
                tracks.Add(new PoseTrack { target = piece, translation = -offset, rotation = new Vector3(0f, 36f, 0f),
                    scale = Vector3.one / 0.22f, phaseStart = begin, phaseEnd = begin + 0.19f, lift = 0.9f });
            }
            return Pose(assembly, tracks);
        }

        private static void AddPieces(Vector3 start, Vector3 end, List<Transform> pieces, Transform parent, Material material)
        {
            int count = Mathf.CeilToInt(Vector3.Distance(start, end) / 2.25f);
            Vector3 delta = (end - start) / count;
            Quaternion direction = Quaternion.LookRotation(delta);
            for (int i = 0; i < count; i++)
            {
                Vector3 center = start + delta * (i + 0.5f) - Vector3.up * Radius - direction * Vector3.up * 0.20f;
                Transform piece = Visual(PrimitiveType.Cube, parent, $"Road Block {pieces.Count + 1:00}", center,
                    new Vector3(1.9f, 0.40f, delta.magnitude + 0.06f), material).transform;
                piece.rotation = direction;
                pieces.Add(piece);
            }
        }

        private static DistrictMaterialization Entrance(Transform parent, int index, Vector3 location, Vector3 cameraOffset,
            Material white, Material blue, Material gold, Material wire)
        {
            var entrance = CampaignAuthoring.District(parent, index, location, white, blue, gold, true, wire);
            var presentation = entrance.gameObject.AddComponent<DistrictMaterialization>();
            presentation.entrance = entrance;
            presentation.carrierAnchor = Child(entrance.transform, "Ball Dock Anchor", location).transform;
            var light = Child(entrance.transform, "Materialization Light", location - Vector3.right * 2.1f + Vector3.up * 2.6f)
                .AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.79f, 0.35f);
            light.range = 6f;
            light.intensity = 0f;
            presentation.revealLight = light;

            Transform solid = entrance.solid;
            Transform[] layers = solid.Cast<Transform>().ToArray();
            presentation.layers = new Transform[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                Transform mesh = layers[i];
                Vector3 basePoint = mesh.position - Vector3.up * (mesh.localScale.y * 0.5f);
                Transform wrapper = Child(solid, $"Building Layer {i + 1:00}", basePoint).transform;
                mesh.SetParent(wrapper, true);
                presentation.layers[i] = wrapper;
            }
            presentation.unfolding = Pose(entrance.gameObject, presentation.layers.Select((layer, i) => new PoseTrack
            {
                target = layer, translation = Vector3.up * (0.65f + i * 0.65f) + Vector3.left * (0.35f * i),
                rotation = new Vector3(0f, 24f + i * 15f, 0f), phaseStart = 0.08f * i, phaseEnd = 1f
            }));
            var plaquePosition = location + new Vector3(0.38f, 0.08f, -0.72f);
            var plaque = Visual(PrimitiveType.Cube, entrance.transform, "Architectural Name Plaque", plaquePosition,
                new Vector3(1.95f, 0.45f, 0.10f), blue);
            Quaternion plaqueRotation = Quaternion.LookRotation(-cameraOffset);
            plaque.transform.rotation = plaqueRotation;
            var letter = Child(entrance.transform, "Carved Name", plaquePosition - plaqueRotation * Vector3.forward * 0.07f)
                .AddComponent<TextMesh>();
            letter.transform.rotation = plaqueRotation;
            letter.text = entrance.districtName;
            letter.anchor = TextAnchor.MiddleCenter;
            letter.characterSize = 0.11f;
            letter.fontSize = 64;
            letter.color = new Color(0.99f, 0.92f, 0.72f);
            return presentation;
        }

        private static Material WireMaterial()
        {
            const string path = SystemsFolder + "/Locked District Wire.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                { name = "Locked District Wire", color = new Color(0.65f, 0.89f, 1f, 0.58f) };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GeometryPose Pose(GameObject owner, IEnumerable<PoseTrack> tracks)
        {
            var pose = owner.AddComponent<GeometryPose>();
            pose.tracks = tracks.ToArray();
            return pose;
        }
    }
}
