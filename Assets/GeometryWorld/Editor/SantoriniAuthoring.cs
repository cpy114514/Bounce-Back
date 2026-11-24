using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class SantoriniAuthoring
    {
        internal const string Folder = "Assets/Scenes/Archive/BounceBack";
        internal const string ScenePath = Folder + "/Santorini.unity";

        [MenuItem("Tools/Bounce Back/Create Santorini Blockout")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                Debug.Log($"Santorini already exists at {ScenePath}.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Scenes", "BounceBack");

            SaveNewScene(ScenePath, CreateLevel);
        }

        internal static void SaveNewScene(string path, Action<Scene> populate, bool replaceExisting = false)
        {
            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (existing && !replaceExisting)
                return;
            Scene open = EditorSceneManager.GetSceneByPath(path);
            if (open.IsValid() && open.isLoaded && open.isDirty)
                throw new InvalidOperationException($"Refusing to regenerate dirty scene {path}.");

            Scene previous = SceneManager.GetActiveScene();
            bool replacingOpen = open.IsValid() && open.isLoaded;
            bool isolatedBatch = Application.isBatchMode && string.IsNullOrEmpty(previous.path);
            bool single = isolatedBatch || (replacingOpen && SceneManager.sceneCount == 1);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                single ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (!single && replacingOpen && open.IsValid() && open.isLoaded && open != scene)
                EditorSceneManager.CloseScene(open, true);
            try
            {
                populate(scene);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException($"Could not save {path}.");
                AssetDatabase.SaveAssets();
                Debug.Log("SANTORINI_SCENE_SAVED " + path + " replace=" + replaceExisting);
            }
            finally
            {
                bool keepOpen = isolatedBatch || previous.path == path || string.IsNullOrEmpty(previous.path);
                if (!keepOpen && previous.IsValid() && previous.isLoaded && scene.IsValid() && scene.isLoaded && scene != previous)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void CreateLevel(Scene scene)
        {
            var level = LevelAuthoring.CreateInScene(scene, "Santorini", false);
            UnityEngine.Object.DestroyImmediate(level.spawn.gameObject);
            level.spawn = Child(level.transform, "Release Point", new Vector3(0f, 5.32f, -10.5f)).AddComponent<BallSpawn>();
            level.spawn.launchSpeed = 4.8f;
            level.spawn.material = Material("Ball Amber", new Color(0.93f, 0.43f, 0.16f));
            level.allowRuntimeBuilding = false;
            level.requireStarterPiston = false;
            level.attemptTimeLimit = 15f;
            level.fallHeight = -3f;
            level.playHint = "Drag the two green blocks and the blue deflector to their rail markers. Click Release or press Space.";
            level.retryEffect = Child(level.transform, "Retry Fragments", Vector3.zero).AddComponent<BallRetryEffect>();
            level.audioDirector = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Audio, Vector3.zero, false)
                .GetComponent<AdaptivePuzzleAudio>();

            Camera camera = level.gameplayCamera;
            camera.transform.position = new Vector3(20f, 22f, -6f);
            camera.transform.LookAt(new Vector3(-1f, 1.5f, 3.5f));
            camera.orthographicSize = 10.6f;
            camera.backgroundColor = new Color(0.66f, 0.82f, 0.91f);

            var plaster = Material("Lime Plaster", new Color(0.94f, 0.92f, 0.82f));
            var blue = Material("Aegean Blue", new Color(0.20f, 0.45f, 0.73f));
            var green = Material("Slime Green", new Color(0.32f, 0.73f, 0.43f));
            var gold = Material("Rail Marker", new Color(0.98f, 0.63f, 0.20f));
            var slide = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Folder + "/Mechanism Surface.physicMaterial");
            if (!slide)
            {
                slide = new PhysicsMaterial("Mechanism Surface") { frictionCombine = PhysicsMaterialCombine.Minimum };
                slide.staticFriction = slide.dynamicFriction = 0f;
                AssetDatabase.CreateAsset(slide, Folder + "/Mechanism Surface.physicMaterial");
            }

            Transform course = Child(level.transform, "Course", Vector3.zero).transform;
            Deck(course, "Launch Terrace", new Vector3(0f, 5f, -10f), new Vector3(2.4f, 1.4f, 2f), plaster);
            for (int i = 0; i < 3; i++)
                Deck(course, $"Stair {i + 1}", new Vector3(0f, 4.94f - i * 0.06f, -8.5f + i),
                    new Vector3(2.4f, 0.45f, 1f), plaster);
            Slope(course, "Smooth Slope", new Vector3(0f, 4.82f, -6f), new Vector3(0f, 3.5f, -2f), 2.4f, plaster);
            Deck(course, "Ramp Approach", new Vector3(0f, 4.82f, -6.25f), new Vector3(2.4f, 0.42f, 0.5f), plaster);
            Deck(course, "Landing Walk", new Vector3(0f, -0.35f, 14.2f), new Vector3(2.8f, 0.65f, 8.2f), plaster);
            Deck(course, "Goal Approach", new Vector3(-3.0f, -0.35f, 18.4f), new Vector3(3.3f, 0.65f, 6.4f), plaster);
            Slope(course, "Goal Steps Ramp", new Vector3(-4.65f, -0.35f, 18.4f), new Vector3(-5.85f, -0.12f, 18.4f), 6.4f, blue);
            Deck(course, "Goal Terrace", new Vector3(-6.95f, -0.12f, 18.4f), new Vector3(2.2f, 0.75f, 6.4f), plaster);

            Transform mechanics = Child(level.transform, "Rail Mechanisms", Vector3.zero).transform;
            Rail(mechanics, "Slime Block 1", new Vector3(0f, 0.8f, 1.2f), new Vector3(1.35f, 0.45f, 3.6f),
                Quaternion.identity, 1.5f, 0.87f, green, gold, slide, level.audioDirector, Cue.Slime);
            Rail(mechanics, "Slime Block 2", new Vector3(0f, -0.20f, 8.8f), new Vector3(1.35f, 0.45f, 3.6f),
                Quaternion.identity, -1.5f, 0.91f, green, gold, slide, level.audioDirector, Cue.Slime);
            Rail(mechanics, "Deflector", new Vector3(0f, 0.4f, 16.8f), new Vector3(1.35f, 1.45f, 0.28f),
                Quaternion.Euler(0f, 32f, 0f), 1.6f, 0.55f, blue, gold, slide, level.audioDirector, Cue.Rotation);

            var goalPosition = new Vector3(-6.65f, 0.43f, 18.4f);
            level.goal = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Goal, goalPosition, false).GetComponent<PuzzleTrigger>();
            level.goal.GetComponent<BoxCollider>().size = new Vector3(1.5f, 1.2f, 5.5f);
            Visual(PrimitiveType.Cylinder, level.goal.transform, "Goal Marker", new Vector3(-6.65f, -0.09f, 18.4f),
                new Vector3(1.35f, 0.03f, 1.35f), gold);

            Transform scenery = Child(level.transform, "Architecture Visuals", Vector3.zero).transform;
            Visual(PrimitiveType.Cube, scenery, "Terrace Foundation", new Vector3(0f, 1.2f, -9.9f),
                new Vector3(2.15f, 5.9f, 1.8f), plaster);
            Visual(PrimitiveType.Cylinder, scenery, "Blue Cupola", new Vector3(-2.4f, 4.9f, -9.9f),
                new Vector3(1.65f, 0.5f, 1.65f), blue);
            Visual(PrimitiveType.Cube, scenery, "Cupola Tower", new Vector3(-2.4f, 1.9f, -9.9f),
                new Vector3(1.6f, 5.2f, 1.6f), plaster);
            CampaignAuthoring.AddTutorialCompletion(level, scenery, gold, blue);

            var issues = LevelAuthoring.Validate(level);
            if (issues.Count > 0)
                throw new InvalidOperationException(string.Join("\n", issues));
        }

        private static void Rail(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation,
            float initialOffset, float restitution, Material surface, Material marker, PhysicsMaterial physics,
            AdaptivePuzzleAudio audio, Cue cue)
        {
            var root = Child(parent, name, center);
            root.transform.rotation = rotation;
            var box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = physics;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var visual = Visual(PrimitiveType.Cube, root.transform, "VisualRoot", center, size, surface);
            var elastic = root.AddComponent<ElasticGeometry>();
            elastic.visual = visual.transform;
            elastic.localAxis = Quaternion.Inverse(rotation) * Vector3.right;
            elastic.maximumDisplacement = 1.8f;
            elastic.initialOffset = initialOffset;
            elastic.stiffness = 165f;
            elastic.damping = 25f;
            elastic.overshoot = 0f;
            elastic.squashStretch = 0f;
            elastic.returnOnRelease = false;
            elastic.moveCollider = true;
            elastic.bounceBall = true;
            elastic.restitution = restitution;
            elastic.audioDirector = audio;
            elastic.impactCue = cue;

            float guideHeight = center.y - size.y * 0.5f - 0.05f;
            float frontEdge = center.z - (cue == Cue.Slime ? size.z * 0.5f + 0.12f : 0.75f);
            var guide = new Vector3(center.x, guideHeight, frontEdge);
            Visual(PrimitiveType.Cube, parent, name + " Rail Guide", guide, new Vector3(3.9f, 0.07f, 0.15f), surface);
            Visual(PrimitiveType.Cube, parent, name + " Alignment Marker", guide + Vector3.up * 0.05f,
                new Vector3(0.26f, 0.08f, 0.36f), marker);
        }

        internal static BoxCollider Deck(Transform parent, string name, Vector3 top, Vector3 size, Material material) =>
            Solid(parent, name, top - Vector3.up * (size.y * 0.5f), size, Quaternion.identity, material);

        internal static void Slope(Transform parent, string name, Vector3 start, Vector3 end, float width, Material material)
        {
            Vector3 travel = end - start;
            Quaternion rotation = Quaternion.LookRotation(travel, Vector3.up);
            const float height = 0.4f;
            Solid(parent, name, (start + end) * 0.5f - rotation * Vector3.up * (height * 0.5f),
                new Vector3(width, height, travel.magnitude), rotation, material);
        }

        private static BoxCollider Solid(Transform parent, string name, Vector3 center, Vector3 size, Quaternion rotation, Material material)
        {
            var root = Child(parent, name, center);
            root.transform.rotation = rotation;
            var box = root.AddComponent<BoxCollider>();
            box.size = size;
            Visual(PrimitiveType.Cube, root.transform, "VisualRoot", center, size, material);
            return box;
        }

        internal static GameObject Child(Transform parent, string name, Vector3 position) =>
            LevelAuthoring.NewObject(name, parent, parent.gameObject.scene, position, false);

        internal static GameObject Visual(PrimitiveType shape, Transform parent, string name, Vector3 position,
            Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.transform.SetParent(parent, false);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        internal static Material Material(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.22f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
