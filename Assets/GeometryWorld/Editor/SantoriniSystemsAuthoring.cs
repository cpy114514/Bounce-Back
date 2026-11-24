using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BounceBack.GeometryWorld.Editor.SantoriniAuthoring;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class SantoriniSystemsAuthoring
    {
        internal const string SystemsFolder = "Assets/Scenes/Archive/BounceBackSystems";
        internal const string LevelPath = SystemsFolder + "/Santorini Gameplay.unity";
        internal static readonly Vector3 PoolWaterTop = new(-1.40f, 0.45f, 2.05f);
        internal static readonly Vector3 PoolWaterSize = new(3.25f, 0.42f, 3.65f);

        [Serializable]
        private struct CollisionBox
        {
            public string name;
            public Matrix4x4 localToWorld;
            public Vector3 center;
            public Vector3 size;
        }

        [Serializable]
        private sealed class CollisionLayout
        {
            public CollisionBox[] boxes;
        }

        internal static void ExportCollisionLayout()
        {
            Scene scene = EditorSceneManager.GetSceneByPath(LevelPath);
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);
            try
            {
                var level = LevelAuthoring.FindLevel(scene);
                var physics = level.transform.Find("Course Physics");
                var boxes = physics.GetComponentsInChildren<BoxCollider>(true)
                    .Concat(level.transform.GetComponentsInChildren<ElasticGeometry>(true)
                        .Where(item => item.draggable).Select(item => item.GetComponent<BoxCollider>()))
                    .Select(box => new CollisionBox
                    {
                        name = box.name,
                        localToWorld = box.transform.localToWorldMatrix,
                        center = box.center,
                        size = box.size
                    }).ToArray();
                File.WriteAllText("Santorini Collision Layout.json", JsonUtility.ToJson(new CollisionLayout { boxes = boxes }, true));
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Tools/Bounce Back/Create Gameplay Systems Scene")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(SystemsFolder))
                AssetDatabase.CreateFolder("Assets/Scenes", "BounceBackSystems");
            SaveNewScene(LevelPath, Populate, true);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != LevelPath))
            {
                int menu = scenes.FindIndex(scene => scene.path == CampaignAuthoring.MenuPath);
                scenes.Insert(menu + 1, new EditorBuildSettingsScene(LevelPath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void Populate(Scene scene)
        {
            var level = LevelAuthoring.CreateInScene(scene, "Santorini Courtyard", false);
            level.allowRuntimeBuilding = false;
            level.playHint = "Drag both spring pads and the lift gate to their alignment marks.";
            level.lockWallOnRelease = false;
            level.fallHeight = -3.2f;
            level.stoppedDuration = 1.5f;
            level.stoppedSpeed = 0.18f;

            const float diameter = 0.76f;
            const float radius = diameter * 0.5f;
            var start = new Vector3(-5.9f, 4.45f, -3.65f);
            level.spawn.name = "Tower Release";
            level.spawn.transform.SetPositionAndRotation(start + Vector3.up * radius, Quaternion.LookRotation(Vector3.right));
            level.spawn.launchSpeed = 5.9f;
            level.spawn.diameter = diameter;
            var piston = level.StarterPiston;
            piston.stroke = 0.28f;
            piston.pushTime = 0.14f;
            piston.AlignVisuals();
            level.audioDirector = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Audio, Vector3.zero, false)
                .GetComponent<AdaptivePuzzleAudio>();
            level.audioDirector.courtyardTones = true;

            var white = BlockoutMaterial("Courtyard White", new Color(0.94f, 0.88f, 0.76f));
            var stone = BlockoutMaterial("Courtyard Shadow Stone", new Color(0.56f, 0.59f, 0.65f));
            var blue = BlockoutMaterial("Water Blue", new Color(0.20f, 0.65f, 0.74f));
            var deep = BlockoutMaterial("Reflector Blue", new Color(0.12f, 0.34f, 0.52f));
            var gold = BlockoutMaterial("Plate Gold", new Color(0.96f, 0.64f, 0.25f));
            var foam = BlockoutMaterial("Water Highlight", new Color(0.57f, 0.87f, 0.86f));
            level.spawn.material = gold;

            var colliders = Child(level.transform, "Course Physics", Vector3.zero).transform;
            var art = Child(level.transform, "Courtyard Art", Vector3.zero).transform;
            var tower = Child(art, "Tower Art", start).transform;
            level.spawn.transform.SetParent(tower, true);
            var stairs = Child(art, "Staircase Art", new Vector3(-4.7f, 4.43f, -3.65f)).transform;
            var courtyard = Child(art, "Courtyard and Water Art", Vector3.zero).transform;
            var terrace = Child(art, "Goal Terrace Art", new Vector3(4.9f, 0.83f, 4.10f)).transform;
            var rails = Child(level.transform, "Rail Mechanisms", Vector3.zero).transform;
            var deck = new Vector3(0f, -0.13f, 0.05f);
            Form(courtyard, "Floating Limestone Foundation", deck - Vector3.up * 0.57f, new Vector3(12.15f, 0.72f, 9.65f), white);
            Form(courtyard, "Lower Limestone Course", deck - Vector3.up * 1.0f, new Vector3(11.68f, 0.18f, 9.20f), stone);
            Form(courtyard, "Foundation Crown", deck - Vector3.up * 0.16f, new Vector3(12.36f, 0.13f, 9.83f), white);
            Form(courtyard, "Glazed Foundation Frieze", new Vector3(0f, -0.47f, -4.795f),
                new Vector3(11.85f, 0.064f, 0.085f), deep);

            Top(colliders, tower, "Launch Coping", new Vector3(-5.91f, start.y, start.z), new Vector3(2.83f, 0.36f, 1.92f), white);
            Form(tower, "Tower Foot", new Vector3(-5.64f, 0.10f, -3.65f), new Vector3(2.17f, 0.45f, 2.20f), stone);
            Form(tower, "White Tower", new Vector3(-5.64f, 2.27f, -3.65f), new Vector3(1.77f, 4.02f, 1.79f), white);
            Form(tower, "Glazed Cornice", new Vector3(-5.64f, 3.94f, -3.65f), new Vector3(1.9f, 0.12f, 1.94f), deep);
            for (int i = 0; i < 2; i++)
                Arch(tower, new Vector3(-4.74f, 0.72f + i * 1.44f, -3.65f), Quaternion.Euler(0f, 90f, 0f), 0.31f, 0.53f, white, deep);
            Form(tower, "Start Rail", new Vector3(-5.72f, 4.55f, -4.56f), new Vector3(1.85f, 0.20f, 0.17f), white);

            var first = new Vector3(-4.50f, 4.44f, -3.65f);
            var bend = new Vector3(-3.20f, 4.27f, -2.10f);
            Vector3 last = new(-3.20f, 4.27f, -3.65f);
            Segment(colliders, null, "Straight Stair Ramp", first, last, 1.45f, white, 0.08f);
            Steps(stairs, first, last, 1.55f, 5, white);
            var guide = GuideMaterial();
            for (int i = 1; i <= 4; i++)
            {
                float angle = (-90f + i * 22.5f) * Mathf.Deg2Rad;
                Vector3 next = bend + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.55f;
                Segment(colliders, null, $"Turn Ramp {i}", last, next, 1.56f, white, 0.16f);
                Steps(stairs, last, next, 1.60f, 2, white);
                Vector3 mid = (last + next) * 0.5f;
                Vector3 radial = (mid - bend).normalized;
                foreach (int side in new[] { -1, 1 })
                {
                    var wall = Box(colliders, stairs, $"Turn Parapet {i} {side}", mid + radial * (0.74f * side) + Vector3.up * 0.21f,
                        new Vector3(0.14f, 0.65f, (next - last).magnitude * (side > 0 ? 1.57f : 0.65f)),
                        Quaternion.LookRotation(next - last), white);
                    wall.sharedMaterial = guide;
                }
                last = next;
            }
            Vector3 slopeEnd = new(-1.65f, 2.52f, 0f);
            Segment(colliders, stairs, "Pool Drop Ramp", last, slopeEnd, 1.58f, white, 0.05f);
            Form(stairs, "Slope Foundation", new Vector3(-1.65f, 0.92f, -1.6f), new Vector3(1.68f, 2.33f, 1.54f), white);
            Arch(stairs, new Vector3(-0.80f, 0.25f, -1.58f), Quaternion.Euler(0f, 90f, 0f), 0.29f, 0.66f, stone, deep);

            Vector3 water = PoolWaterTop;
            var basin = Child(courtyard, "Pool Stone Rim", water).transform;
            Form(basin, "Pool Basin", water - Vector3.up * 0.48f, new Vector3(3.6f, 0.50f, 3.95f), stone);
            foreach (float x in new[] { -3.15f, 0.35f })
                Form(basin, "Long Pool Coping", new Vector3(x, 0.36f, water.z), new Vector3(0.23f, 0.25f, 4.05f), white);
            foreach (float z in new[] { 0.13f, 3.98f })
                Form(basin, "Short Pool Coping", new Vector3(water.x, 0.36f, z), new Vector3(3.35f, 0.25f, 0.22f), white);
            Form(courtyard, "Breathing Pool Surface", water - Vector3.up * 0.08f,
                new Vector3(PoolWaterSize.x, 0.035f, PoolWaterSize.z), blue);
            var poolSpring = SpringPad(rails, courtyard, "Elastic Pool Surface", new Vector3(-0.80f, water.y + 0.035f, 1.91f),
                new Vector3(0.96f, 0.24f, 1.68f), Vector3.right, 1f, guide, deep, foam, gold, level.audioDirector);
            poolSpring.restitution = 0.79f;
            poolSpring.springSpeed = 0.30f;

            var terraceSpring = SpringPad(rails, terrace, "Terrace Spring Pad", new Vector3(1.94f, 0.66f, 4.61f),
                new Vector3(1.26f, 0.24f, 0.90f), Vector3.forward, 0.90f, guide, deep, foam, gold, level.audioDirector);
            terraceSpring.restitution = 0.50f;
            terraceSpring.springSpeed = 0.55f;
            terraceSpring.tangentRetention = 0.70f;
            Form(terrace, "Spring Rail Limestone Base", new Vector3(1.94f, 0.08f, 4.61f),
                new Vector3(1.58f, 0.55f, 2.88f), white);
            Form(terrace, "Spring Rail Bronze Bed", new Vector3(1.94f, 0.36f, 4.61f),
                new Vector3(1.42f, 0.06f, 2.68f), stone);

            var wallPosition = new Vector3(-1.55f, 2.15f, 3.95f);
            var wallRotation = Quaternion.LookRotation(new Vector3(0.704f, -0.065f, -0.707f));
            var wallSize = new Vector3(2.60f, 1.25f, 0.29f);
            var wallCollider = Box(rails, null, "Lift Wall", wallPosition, wallSize, wallRotation, deep);
            wallCollider.sharedMaterial = guide;
            var body = wallCollider.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var lift = wallCollider.gameObject.AddComponent<ElasticGeometry>();
            lift.localAxis = Quaternion.Inverse(wallRotation) * Vector3.up;
            lift.initialOffset = -1.5f;
            lift.maximumDisplacement = 1.5f;
            lift.snapStep = 0.25f;
            lift.stiffness = 140f;
            lift.damping = 23f;
            lift.returnOnRelease = false;
            lift.overshoot = lift.squashStretch = 0f;
            lift.moveCollider = true;
            lift.audioDirector = level.audioDirector;
            level.preparationWall = lift;
            var slab = Visual(PrimitiveType.Cube, lift.transform, "Glazed Stone Shutter", wallPosition, wallSize, deep);
            lift.visual = slab.transform;
            var rim = Form(lift.transform, "Shutter Cornice", wallPosition + wallRotation * Vector3.up * 0.61f,
                new Vector3(2.82f, 0.16f, 0.39f), white);
            rim.rotation = wallRotation;
            for (int tile = -1; tile <= 1; tile++)
            {
                Vector3 position = wallPosition + wallRotation * new Vector3(tile * 0.60f, -0.03f, 0.153f);
                Form(lift.transform, "Glazed Shutter Panel", position, new Vector3(0.54f, 0.76f, 0.025f), blue)
                    .rotation = wallRotation;
            }
            var shutterMark = Form(lift.transform, "Lift Height Marker", wallPosition + wallRotation * Vector3.forward * 0.23f,
                new Vector3(0.14f, 0.14f, 0.028f), gold);
            shutterMark.rotation = wallRotation * Quaternion.Euler(0f, 0f, 45f);
            var reflector = lift.gameObject.AddComponent<PassiveReflector>();
            reflector.restitution = 0.93f;
            reflector.contactPause = 0.06f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 end = wallPosition + wallRotation * Vector3.right * (1.56f * side);
                Form(courtyard, "Limestone Lift Pier", new Vector3(end.x, 1.50f, end.z),
                    new Vector3(0.36f, 3.10f, 0.36f), white).rotation = wallRotation;
                Form(courtyard, "Bronze Runner", new Vector3(end.x, 1.70f, end.z),
                    new Vector3(0.09f, 2.52f, 0.41f), gold).rotation = wallRotation;
                Form(courtyard, "Lift Guide Alignment Mark", end + wallRotation * Vector3.forward * 0.25f,
                    new Vector3(0.13f, 0.13f, 0.045f), gold).rotation = wallRotation * Quaternion.Euler(0f, 0f, 45f);
            }
            Form(courtyard, "Alignment Lintel", wallPosition + Vector3.up * 0.69f + wallRotation * Vector3.forward * 0.25f,
                new Vector3(3.43f, 0.14f, 0.31f), white).rotation = wallRotation;
            level.wallLock = Form(lift.transform, "Stone Locking Pin", wallPosition - wallRotation * Vector3.right * 1.32f,
                new Vector3(0.32f, 0.27f, 0.34f), white).gameObject;
            level.wallLock.SetActive(false);

            Vector3 lip = new(5.05f, 0.78f, 5.24f);
            Top(colliders, terrace, "Landing Terrace", new Vector3(3.375f, 0.96f, 4.72f), new Vector3(1.25f, 0.66f, 3.65f), white);
            Segment(colliders, terrace, "Cascade Lip Ramp", new Vector3(4.0f, 0.96f, lip.z), new Vector3(4.49f, lip.y, lip.z), 3.08f, white);
            var plateFloor = Top(colliders, null, "Cascade Pressure Plate Floor", lip, new Vector3(1.12f, 0.52f, 3.08f), white);
            level.goal = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Goal, lip + new Vector3(0.26f, radius, 0f), false)
                .GetComponent<PuzzleTrigger>();
            level.goal.GetComponent<BoxCollider>().size = new Vector3(0.8f, 0.35f, 2.85f);
            var plate = level.goal.gameObject.AddComponent<GoalPressurePlate>();
            plate.level = level;
            plate.surface = Visual(PrimitiveType.Cube, level.goal.transform, "Plate Surface Slot", lip - Vector3.up * 0.09f,
                new Vector3(plateFloor.size.x, 0.18f, 3.08f), deep).transform;
            var glow = BlockoutMaterial("Pressure Pattern Glow", gold.color);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", gold.color * 1.6f);
            EditorUtility.SetDirty(glow);
            plate.litPattern = Child(plate.surface, "Gold Inlay", lip + Vector3.up * 0.012f);
            for (int i = -1; i <= 1; i++)
                Form(plate.litPattern.transform, "Inlaid Diamond", lip + new Vector3(0.11f, 0.015f, i * 0.37f),
                    new Vector3(0.22f, 0.044f, 0.12f), glow).localRotation = Quaternion.Euler(0f, 45f, 0f);
            plate.pulses = PressurePulses(level.goal.transform, lip, glow);
            Form(terrace, "Waterfall Cornice", new Vector3(5.68f, 0.46f, lip.z), new Vector3(0.28f, 0.25f, 2.32f), white);
            var falls = Child(terrace, "Waterfall Slot", new Vector3(5.72f, 0.43f, lip.z)).transform;
            for (int i = 0; i < 6; i++)
                Form(falls, "Fall Ribbon", new Vector3(5.76f + (i % 3) * 0.075f, -1.15f - (i % 2) * 0.20f, lip.z - 0.92f + i * 0.36f),
                    new Vector3(0.095f, 3.14f + (i % 2) * 0.35f, 0.40f), i % 3 == 0 ? foam : blue);
            Form(terrace, "Cascade Foundation", new Vector3(4.185f, 0.04f, 4.72f), new Vector3(2.91f, 1.20f, 3.61f), white);
            for (int i = 0; i < 2; i++)
                Arch(terrace, new Vector3(3.18f + i * 1.28f, -0.48f, 2.91f), Quaternion.Euler(0f, 180f, 0f),
                    0.29f, 0.46f, white, stone);
            for (int i = 0; i < 5; i++)
                Form(terrace, "Cascade Baluster", new Vector3(0.66f + i * 0.66f, 1.2f, lip.z + 1.05f), new Vector3(0.12f, 0.48f, 0.17f), white);
            Form(terrace, "Cascade Rail", new Vector3(1.98f, 1.47f, lip.z + 1.05f), new Vector3(2.93f, 0.12f, 0.21f), white);

            level.retryEffect = Child(level.transform, "Ball Rewind", Vector3.zero).AddComponent<BallRetryEffect>();
            level.retryEffect.rewindPath = true;
            level.retryEffect.duration = 1f;
            var trace = Child(level.retryEffect.transform, "Previous Run Inlay", Vector3.zero).AddComponent<LineRenderer>();
            trace.positionCount = 0;
            trace.widthMultiplier = 0.023f;
            trace.sharedMaterial = BlockoutMaterial("Previous Run Linen", new Color(0.72f, 0.62f, 0.46f));
            trace.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            level.retryEffect.lastAttempt = trace;

            var camera = level.gameplayCamera;
            var focus = new Vector3(-0.05f, 0.95f, 0.25f);
            var direction = new Vector3(12f, 15.0f, -15f);
            camera.transform.SetPositionAndRotation(focus + direction, Quaternion.LookRotation(-direction));
            camera.orthographicSize = 9.5f;
            camera.backgroundColor = new Color(0.71f, 0.82f, 0.87f);
            var overview = Child(level.transform, "Overview Camera Anchor", camera.transform.position).transform;
            overview.rotation = camera.transform.rotation;
            var rig = level.gameObject.AddComponent<PuzzleCameraRig>();
            rig.view = camera;
            rig.level = level;
            rig.overview = overview;
            rig.overviewSize = camera.orthographicSize;
            rig.followSize = 8.55f;
            rig.followOffset = direction;
            rig.frameCourtyard = true;

            CitySystemsAuthoring.Attach(level, rig, colliders.gameObject, rails.gameObject, art, white, blue, gold);
            SantoriniArtIntegration.Apply(level);
            var issues = LevelAuthoring.Validate(level);
            if (issues.Count != 0)
                throw new InvalidOperationException(string.Join("\n", issues));
        }

        private static PhysicsMaterial GuideMaterial()
        {
            const string path = SystemsFolder + "/Guide Surface.physicMaterial";
            var guide = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (guide)
                return guide;
            guide = new PhysicsMaterial("Guide Surface") { staticFriction = 0f, dynamicFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(guide, path);
            return guide;
        }

        private static Transform Form(Transform art, string name, Vector3 center, Vector3 size, Material paint) =>
            Visual(PrimitiveType.Cube, art, name, center, size, paint).transform;

        private static LineRenderer[] PressurePulses(Transform parent, Vector3 center, Material gold)
        {
            Vector3[] shape = Enumerable.Range(0, 9)
                .Select(i => Quaternion.Euler(0f, 22.5f + i * 45f, 0f) * Vector3.forward).ToArray();
            var rings = new LineRenderer[2];
            for (int i = 0; i < rings.Length; i++)
            {
                var ring = Child(parent, "Pressure Plate Ripple", center + Vector3.up * (0.21f + i * 0.14f)).transform;
                CampaignAuthoring.AddLine(ring, "Gold Wave", shape, gold, 0.044f);
                rings[i] = ring.GetComponentInChildren<LineRenderer>();
                rings[i].enabled = false;
            }
            return rings;
        }

        private static ElasticGeometry SpringPad(Transform rails, Transform art, string name, Vector3 top,
            Vector3 size, Vector3 axis, float travel, PhysicsMaterial guide, Material ceramic, Material inlay,
            Material gold, AdaptivePuzzleAudio audio)
        {
            var surface = Top(rails, null, name, top, size, ceramic);
            surface.sharedMaterial = guide;
            var body = surface.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var spring = surface.gameObject.AddComponent<ElasticGeometry>();
            spring.localAxis = axis;
            spring.initialOffset = -travel;
            spring.maximumDisplacement = travel;
            spring.snapStep = 0.15f;
            spring.stiffness = 140f;
            spring.damping = 23f;
            spring.returnOnRelease = false;
            spring.overshoot = spring.squashStretch = 0f;
            spring.moveCollider = spring.bounceBall = true;
            spring.chargeTime = 0.22f;
            spring.edgeBias = 0.01f;
            spring.audioDirector = audio;
            spring.impactCue = Cue.Slime;

            spring.visual = Form(surface.transform, "Glazed Spring Pad", surface.transform.position, size, ceramic);
            var diamond = Form(spring.visual, "Alignment Diamond", top + Vector3.up * 0.013f,
                new Vector3(0.23f / size.x, 0.025f / size.y, 0.23f / size.z), gold);
            diamond.rotation = Quaternion.Euler(0f, 45f, 0f);
            for (int step = -1; step <= 1; step += 2)
            {
                Vector3 sideways = Vector3.Cross(axis, Vector3.up) * step;
                float halfWidth = Vector3.Dot(size, new Vector3(Mathf.Abs(sideways.x), 0f, Mathf.Abs(sideways.z))) * 0.5f;
                Vector3 railTop = top + sideways * (halfWidth + 0.06f) - Vector3.up * 0.055f;
                Vector3 railSize = Vector3.one * 0.04f + axis * (travel * 2f + Vector3.Dot(size, axis));
                Form(art, "Spring Guide Rail", railTop, railSize, ceramic);
                Form(art, "Spring Guide Alignment Mark", railTop + Vector3.up * 0.03f,
                    Vector3.one * 0.08f + new Vector3(Mathf.Abs(sideways.x), 0f, Mathf.Abs(sideways.z)) * 0.07f, gold);
                Form(spring.visual, "Spring Pad Inlay", top + sideways * (halfWidth - 0.10f) + Vector3.up * 0.006f,
                    new Vector3((axis.x * (size.x - 0.18f) + axis.z * 0.045f) / size.x, 0.013f / size.y,
                        (axis.z * (size.z - 0.18f) + axis.x * 0.045f) / size.z), inlay);
            }
            return spring;
        }

        private static void Steps(Transform art, Vector3 start, Vector3 end, float width, int count, Material paint)
        {
            Vector3 travel = end - start;
            Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(travel, Vector3.up));
            for (int i = 0; i < count; i++)
            {
                Vector3 center = start + travel * ((i + 0.5f) / count) - Vector3.up * 0.15f;
                Form(art, "Limestone Tread", center,
                    new Vector3(width, 0.30f, travel.magnitude / count + 0.025f), paint).rotation = rotation;
            }
        }

        private static void Arch(Transform parent, Vector3 basePoint, Quaternion facing, float radius, float spring, Material stone, Material recess)
        {
            var root = Child(parent, "Recessed Arch", basePoint).transform;
            root.rotation = facing;
            Form(root, "Dark Inset", root.TransformPoint(new Vector3(0f, spring * 0.5f, 0.016f)),
                new Vector3(radius * 1.75f, spring, 0.024f), recess).rotation = facing;
            var cap = Visual(PrimitiveType.Cylinder, root, "Arched Inset", root.TransformPoint(new Vector3(0f, spring, 0.017f)),
                new Vector3(radius * 1.75f, 0.012f, radius * 1.75f), recess);
            cap.transform.rotation = facing * Quaternion.Euler(90f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
                Form(root, "Arch Jamb", root.TransformPoint(new Vector3(side * radius, spring * 0.5f, 0.045f)),
                    new Vector3(0.11f, spring, 0.09f), stone).rotation = facing;
            for (int i = 0; i <= 8; i++)
            {
                float angle = i * 22.5f;
                Vector3 arch = Quaternion.AngleAxis(angle, Vector3.forward) * (Vector3.right * radius);
                Form(root, "Arch Stone", root.TransformPoint(new Vector3(arch.x, spring + arch.y, 0.045f)),
                    new Vector3(0.115f, 0.139f, 0.09f), stone).rotation = facing * Quaternion.Euler(0f, 0f, angle);
            }
        }

        internal static BoxCollider Top(Transform physics, Transform visual, string name, Vector3 point, Vector3 size, Material paint) =>
            Box(physics, visual, name, point - Vector3.up * (size.y * 0.5f), size, Quaternion.identity, paint);

        internal static BoxCollider Segment(Transform physics, Transform art, string name, Vector3 start, Vector3 end, float width,
            Material paint, float overlap = 0f)
        {
            var rotation = Quaternion.LookRotation(end - start);
            var size = new Vector3(width, 0.35f, Vector3.Distance(start, end) + overlap);
            return Box(physics, art, name, (start + end) * 0.5f - rotation * Vector3.up * (size.y * 0.5f), size, rotation, paint);
        }

        private static BoxCollider Box(Transform physics, Transform art, string name, Vector3 center, Vector3 size,
            Quaternion rotation, Material paint)
        {
            var solid = Child(physics, name, center);
            solid.transform.rotation = rotation;
            var collider = solid.AddComponent<BoxCollider>();
            collider.size = size;
            if (art)
            {
                var mesh = Visual(PrimitiveType.Cube, art, name + " Visual", center, size, paint);
                mesh.transform.rotation = rotation;
            }
            return collider;
        }

        internal static Material BlockoutMaterial(string name, Color rgb)
        {
            string assetPath = $"{SystemsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = rgb };
            material.SetFloat("_Smoothness", 0.22f);
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }

    // Authoring defaults only. The generated CourtyardFinale keeps these values editable in the Inspector.
    internal static class FinaleChoreographyData
    {
        internal static readonly Vector3 Dock = new(9.7f, 0.86f, 3.83f);
        internal const float PlateHold = 0.30f;
        internal const float WaterfallPause = 0.20f;
        internal const float SplashDuration = 0.90f;
        internal const float AnchorFoldDuration = 1.40f;
        internal const float AnchorStagger = 0.18f;
        internal const float Overshoot = 0.055f;
        internal const float CameraSize = 14.2f;
        internal const float RoadRevealDuration = 1.30f;
        internal const float FollowTransitionDuration = 1.80f;
        internal const float RolloutDuration = 0.65f;
        internal const float SeamDuration = 0.15f;
        internal const float SeamLength = 0.18f;
        internal const float SeamDegrees = 10f;
    }
}
