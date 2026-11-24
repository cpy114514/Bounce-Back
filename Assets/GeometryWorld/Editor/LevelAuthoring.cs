using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    internal enum PuzzleItem
    {
        Spawn, Goal, BouncePad, Trigger, Checkpoint, ResetZone, Switch,
        MovingPlatform, RotatingPlatform, ElasticObject, Anchor, Connection, Sequence, CollisionEvent, Audio
    }

    internal enum Atmosphere { Daybreak, Mist, Dusk }

    internal static class LevelAuthoring
    {
        internal const string LevelFolder = GeometryAssets.Library + "/Levels";
        internal const float SpawnDiameter = 0.64f;
        internal static readonly Vector3 ZoneSize = new(1.3f, 1.2f, 1.3f);
        internal const float AnchorRadius = 0.33f;

        internal static bool TrySolidGadget(PuzzleItem item, out (GeometryShape Shape, Vector3 Size, int Palette) model)
        {
            model = item switch
            {
                PuzzleItem.BouncePad => (GeometryShape.Platform, new Vector3(2.5f, 0.3f, 1.8f), 3),
                PuzzleItem.MovingPlatform => (GeometryShape.Platform, new Vector3(2.5f, 0.3f, 1.8f), 4),
                PuzzleItem.RotatingPlatform => (GeometryShape.Cylinder, new Vector3(2.5f, 0.3f, 1.8f), 4),
                PuzzleItem.ElasticObject => (GeometryShape.Platform, new Vector3(2.5f, 0.3f, 1.8f), 2),
                PuzzleItem.Switch => (GeometryShape.Cylinder, new Vector3(0.9f, 0.34f, 0.9f), 1),
                _ => default
            };
            return model.Size != Vector3.zero;
        }

        internal static GeometryLevel FindLevel(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out GeometryLevel level))
                    return level;
            return null;
        }

        internal static GeometryLevel CreateInScene(Scene scene, string name, bool undo)
        {
            if (FindLevel(scene))
                throw new InvalidOperationException("This scene already has a Geometry Level root.");
            var root = NewObject(name, null, scene, Vector3.zero, undo);
            var level = root.AddComponent<GeometryLevel>();
            level.levelTitle = name;
            var cameraObject = NewObject("Isometric Camera", root.transform, scene, new Vector3(8f, 9f, -12f), false);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1f, 0f) - cameraObject.transform.position);
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            level.gameplayCamera = camera;
            var lightObject = NewObject("Key Light", root.transform, scene, Vector3.zero, false);
            lightObject.AddComponent<Light>().type = LightType.Directional;
            LevelStarterAuthoring.Ensure(level, false);
            ApplyAtmosphere(level, Atmosphere.Daybreak, false);
            EditorSceneManager.MarkSceneDirty(scene);
            return level;
        }

        internal static GameObject CreateItem(GeometryLevel level, Transform parent, PuzzleItem item, Vector3 position, bool undo = true)
        {
            if (item == PuzzleItem.Spawn)
                return LevelStarterAuthoring.Move(level, parent, position, undo);
            if (TrySolidGadget(item, out var spec))
            {
                var topology = GeometryGenerators.Generate(spec.Shape, spec.Size, 16, 0);
                var asset = GeometryAssets.CreateModel(ObjectName(item), topology, new[] { GeometryAssets.Preset(spec.Palette) });
                var geometry = GeometryAssets.Instantiate(asset, parent, position, false, true, false);
                AddBehavior(geometry.gameObject, item, level, false);
                if (undo)
                    Undo.RegisterCreatedObjectUndo(geometry.gameObject, "Create Puzzle Object");
                return geometry.gameObject;
            }

            var obj = NewObject(ObjectName(item), parent, level.gameObject.scene, position, false);
            switch (item)
            {
                case PuzzleItem.Goal:
                case PuzzleItem.Trigger:
                case PuzzleItem.Checkpoint:
                case PuzzleItem.ResetZone:
                    var zone = obj.AddComponent<PuzzleTrigger>();
                    zone.GetComponent<BoxCollider>().isTrigger = true;
                    zone.GetComponent<BoxCollider>().size = ZoneSize;
                    zone.role = item switch
                    {
                        PuzzleItem.Goal => TriggerRole.Goal,
                        PuzzleItem.Checkpoint => TriggerRole.Checkpoint,
                        PuzzleItem.ResetZone => TriggerRole.Reset,
                        _ => TriggerRole.EventZone
                    };
                    break;
                case PuzzleItem.Anchor:
                    obj.AddComponent<SpatialAnchor>();
                    var sphere = obj.GetComponent<SphereCollider>();
                    sphere.radius = AnchorRadius;
                    sphere.isTrigger = true;
                    break;
                case PuzzleItem.Connection:
                    obj.AddComponent<SpaceConnection>().alignmentCamera = level.gameplayCamera;
                    break;
                case PuzzleItem.Sequence:
                    obj.AddComponent<PuzzleSequence>();
                    break;
                case PuzzleItem.CollisionEvent:
                    obj.AddComponent<BallCollisionEvent>();
                    break;
                case PuzzleItem.Audio:
                    var audio = obj.AddComponent<AdaptivePuzzleAudio>();
                    audio.level = level;
                    break;
            }
            if (undo)
                Undo.RegisterCreatedObjectUndo(obj, "Create Puzzle Object");
            AssignItem(level, obj, item, undo);
            return obj;
        }

        internal static GameObject PlaceStarter(GeometryLevel level, Transform parent, GameObject prefab,
            PuzzleItem? item, Vector3 position, Quaternion localRotation, bool undo)
        {
            if (item == PuzzleItem.Spawn)
                return LevelStarterAuthoring.Move(level, parent, position, undo);
            const string change = "Place Starter Prefab";
            if (undo)
                Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.gameObject.scene);
            if (!instance)
                throw new InvalidOperationException($"Could not place {prefab.name}.");

            Transform target = instance.transform;
            target.SetParent(parent, false);
            target.localScale = prefab.transform.localScale;
            target.localRotation = localRotation;
            target.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            if (level.audioDirector && instance.TryGetComponent(out PlatformMotion motion) && !motion.audioDirector)
            {
                motion.audioDirector = level.audioDirector;
                PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
            }
            if (level.audioDirector && instance.TryGetComponent(out ElasticGeometry elastic) && !elastic.audioDirector)
            {
                elastic.audioDirector = level.audioDirector;
                PrefabUtility.RecordPrefabInstancePropertyModifications(elastic);
            }

            if (undo)
                Undo.RegisterCreatedObjectUndo(instance, change);
            if (item.HasValue)
                AssignItem(level, instance, item.Value, undo);
            if (undo)
            {
                Undo.FlushUndoRecordObjects();
                Undo.SetCurrentGroupName(change);
                Undo.CollapseUndoOperations(group);
            }
            return instance;
        }

        private static void AssignItem(GeometryLevel level, GameObject obj, PuzzleItem item, bool undo)
        {
            if (item == PuzzleItem.Goal && !level.goal)
            {
                if (undo)
                    Undo.RecordObject(level, "Set Level Goal");
                level.goal = obj.GetComponent<PuzzleTrigger>();
            }
            else if (item == PuzzleItem.Audio)
            {
                if (undo)
                    Undo.RecordObject(level, "Assign Level Audio");
                level.audioDirector = obj.GetComponent<AdaptivePuzzleAudio>();
            }
            if (undo)
                PrefabUtility.RecordPrefabInstancePropertyModifications(level);
        }

        internal static void AddBehavior(GameObject obj, PuzzleItem item, GeometryLevel level, bool undo)
        {
            switch (item)
            {
                case PuzzleItem.BouncePad:
                    RequireInteractionCollider(obj, undo);
                    if (!obj.GetComponent<BouncePad>())
                        Add<BouncePad>(obj, undo);
                    break;
                case PuzzleItem.Switch:
                    RequireInteractionCollider(obj, undo);
                    if (!obj.GetComponent<PuzzleSwitch>())
                        Add<PuzzleSwitch>(obj, undo);
                    break;
                case PuzzleItem.MovingPlatform:
                case PuzzleItem.RotatingPlatform:
                    if (obj.GetComponent<PlatformMotion>())
                        return;
                    EnsureKinematic(obj, undo);
                    var motion = Add<PlatformMotion>(obj, undo);
                    motion.audioDirector = level.audioDirector;
                    motion.localTravel = item == PuzzleItem.MovingPlatform ? new Vector3(2f, 0f, 0f) : Vector3.zero;
                    motion.localEulerTravel = item == PuzzleItem.RotatingPlatform ? new Vector3(0f, 90f, 0f) : Vector3.zero;
                    break;
                case PuzzleItem.ElasticObject:
                    if (obj.GetComponent<ElasticGeometry>())
                        return;
                    if (!obj.TryGetComponent(out GeometryObject geometry) || !geometry.surface)
                        throw new InvalidOperationException("Select a geometry model with a separate visual surface.");
                    EnsureKinematic(obj, undo);
                    var elasticity = Add<ElasticGeometry>(obj, undo);
                    elasticity.visual = geometry.surface.transform;
                    elasticity.audioDirector = level.audioDirector;
                    SlimeArtwork.Tint(geometry, undo);
                    break;
                case PuzzleItem.CollisionEvent:
                    RequireInteractionCollider(obj, undo);
                    if (!obj.GetComponent<BallCollisionEvent>())
                        Add<BallCollisionEvent>(obj, undo);
                    break;
            }
        }

        private static void RequireInteractionCollider(GameObject obj, bool undo)
        {
            if (obj.GetComponentsInChildren<Collider>().Any(collider => collider.enabled && !collider.isTrigger))
                return;
            if (!obj.TryGetComponent(out GeometryObject geometry) || !geometry.model)
                throw new InvalidOperationException("Select an editable model or a scene object with a solid collider.");
            if (undo)
                Undo.RecordObject(geometry, "Enable Gameplay Geometry");
            geometry.decorative = false;
            GeometryAssets.SetCollider(geometry, true, undo);
        }

        private static void EnsureKinematic(GameObject obj, bool undo)
        {
            if (!obj.TryGetComponent(out Rigidbody body))
                body = Add<Rigidbody>(obj, undo);
            else if (undo)
                Undo.RecordObject(body, "Configure Platform Physics");
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private static T Add<T>(GameObject obj, bool undo) where T : Component =>
            undo ? Undo.AddComponent<T>(obj) : obj.AddComponent<T>();

        private static string ObjectName(PuzzleItem item) => ObjectNames.NicifyVariableName(item.ToString());

        internal static GameObject NewObject(string name, Transform parent, Scene scene, Vector3 position, bool undo)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            if (parent)
                obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            if (undo)
                Undo.RegisterCreatedObjectUndo(obj, "Create Level Object");
            return obj;
        }

        internal static bool Save(GeometryLevel level, string path = null, bool copy = false)
        {
            Scene scene = level.gameObject.scene;
            if (string.IsNullOrEmpty(path))
                path = scene.path;
            if (string.IsNullOrEmpty(path))
                return false;

            foreach (var model in level.GetComponentsInChildren<GeometryObject>(true).Select(obj => obj.model).Distinct())
                if (model)
                    AssetDatabase.SaveAssetIfDirty(model);
            return EditorSceneManager.SaveScene(scene, path, copy);
        }

        internal static List<string> Validate(GeometryLevel level)
        {
            var issues = new List<string>();
            if (!level)
            {
                issues.Add("Add a Geometry Level root to the scene.");
                return issues;
            }
            if (!level.gameplayCamera || !level.gameplayCamera.isActiveAndEnabled ||
                level.gameplayCamera.gameObject.scene != level.gameObject.scene)
                issues.Add("Assign an enabled gameplay camera from this scene.");
            else if (!level.gameplayCamera.orthographic)
                issues.Add("The gameplay camera is perspective. Switch to Orthographic for isometric levels.");
            if (!level.spawn || !level.spawn.isActiveAndEnabled || !level.spawn.transform.IsChildOf(level.transform))
                issues.Add("Assign an enabled ball spawn under the level root.");
            else if (level.requireStarterPiston &&
                (!level.StarterPiston || !level.StarterPiston.isActiveAndEnabled || !level.StarterPiston.HasVisuals))
                issues.Add("Set up an enabled piston at the ball spawn.");
            if (LevelStarterAuthoring.CountStarts(level) > 1)
                issues.Add("This level has multiple ball starts. Keep one Ball and Piston.");
            if (!level.goal || !level.goal.isActiveAndEnabled || level.goal.role != TriggerRole.Goal ||
                level.goal.gameObject.scene != level.gameObject.scene)
                issues.Add("Assign an enabled trigger with the Goal role.");
            foreach (var zone in level.GetComponentsInChildren<PuzzleTrigger>(true))
                if (!zone.GetComponent<BoxCollider>().isTrigger)
                    issues.Add($"{zone.name} needs Is Trigger enabled.");
            foreach (var connection in level.GetComponentsInChildren<SpaceConnection>(true))
            {
                if (!connection.source || !connection.destination || connection.source == connection.destination)
                    issues.Add($"{connection.name} needs two distinct anchors.");
                if (connection.activation == ConnectionActivation.CameraAligned && !connection.alignmentCamera)
                    issues.Add($"{connection.name} needs an alignment camera.");
                if (connection.source && !connection.source.GetComponent<SphereCollider>().isTrigger ||
                    connection.destination && !connection.destination.GetComponent<SphereCollider>().isTrigger)
                    issues.Add($"{connection.name} uses an anchor with Is Trigger disabled.");
            }
            foreach (var geometry in level.GetComponentsInChildren<GeometryObject>(true))
            {
                if (!geometry.model || !geometry.model.mesh || !geometry.surface || !geometry.surfaceRenderer)
                    issues.Add($"{geometry.name} has an incomplete model or surface.");
                if (geometry.decorative && geometry.gameplayCollider && geometry.gameplayCollider.enabled)
                    issues.Add($"{geometry.name} is decorative and has an enabled gameplay collider.");
            }
            foreach (var elastic in level.GetComponentsInChildren<ElasticGeometry>(true))
                if (!elastic.moveCollider && (!elastic.visual || elastic.visual.GetComponentInChildren<Collider>()))
                    issues.Add($"{elastic.name} needs a separate collider-free child for cosmetic deformation.");
            foreach (var sequence in level.GetComponentsInChildren<PuzzleSequence>(true))
                for (int i = 0; i < sequence.actions.Count; i++)
                {
                    var action = sequence.actions[i];
                    if (action == null || action.type != PuzzleActionType.Wait && !ValidTarget(action))
                        issues.Add($"{sequence.name} · Action {i + 1} needs a valid target.");
                }
            return issues;
        }

        private static bool ValidTarget(PuzzleAction action)
        {
            if (!action.target)
                return false;
            return action.type switch
            {
                PuzzleActionType.SetConnection => action.target.GetComponent<SpaceConnection>(),
                PuzzleActionType.ElasticPulse => action.target.GetComponent<ElasticGeometry>(),
                PuzzleActionType.PlayAudio => action.target.GetComponent<AdaptivePuzzleAudio>(),
                PuzzleActionType.StartSequence => action.target.GetComponent<PuzzleSequence>(),
                PuzzleActionType.SetMaterial => action.material,
                _ => true
            };
        }

        internal static void ApplyAtmosphere(GeometryLevel level, Atmosphere atmosphere, bool undo = true)
        {
            Scene scene = level.gameObject.scene;
            if (SceneManager.GetActiveScene() != scene)
                throw new InvalidOperationException("Make this level’s scene active before changing its atmosphere.");

            var light = level.GetComponentsInChildren<Light>(true).FirstOrDefault(item => item.type == LightType.Directional);
            Camera camera = level.gameplayCamera;
            Color sky = atmosphere switch
            {
                Atmosphere.Mist => new Color32(163, 185, 203, 255),
                Atmosphere.Dusk => new Color32(105, 107, 163, 255),
                _ => new Color32(170, 161, 211, 255)
            };
            if (camera)
            {
                if (undo)
                    Undo.RecordObject(camera, "Apply Geometry Atmosphere");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = sky;
            }
            if (light)
            {
                if (undo)
                    Undo.RecordObjects(new UnityEngine.Object[] { light, light.transform }, "Apply Geometry Lighting");
                light.color = atmosphere == Atmosphere.Dusk ? new Color(1f, 0.76f, 0.74f) : new Color(1f, 0.88f, 0.77f);
                light.intensity = atmosphere == Atmosphere.Mist ? 0.95f : 1.2f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.55f;
                light.transform.rotation = Quaternion.Euler(49f, -31f, 0f);
            }

            var settings = UnityEditor.Unsupported.GetRenderSettings();
            if (undo && settings)
                Undo.RecordObject(settings, "Apply Geometry Atmosphere");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(sky, Color.white, 0.3f);
            RenderSettings.fog = atmosphere == Atmosphere.Mist;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 55f;
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    [InitializeOnLoad]
    internal static class LevelPlaytest
    {
        private const string RestoreKey = "GeometryWorld.RestorePlaytestStart";
        private const string ActiveKey = "GeometryWorld.PlaytestActive";

        static LevelPlaytest()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false))
                    return;
                string path = SessionState.GetString(RestoreKey, "");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                SessionState.EraseBool(ActiveKey);
                SessionState.EraseString(RestoreKey);
            };
            GeometryLevel.EditorRestartRequested += Restart;
        }

        internal static void Start(GeometryLevel level)
        {
            if (!LevelAuthoring.Save(level))
                throw new InvalidOperationException("Save the level scene before starting a playtest.");
            string path = level.gameObject.scene.path;
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetString(RestoreKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }

        internal static void Restart(GeometryLevel level) => EditorSceneManager.LoadSceneInPlayMode(level.gameObject.scene.path,
            new LoadSceneParameters(LoadSceneMode.Single));
    }
}
