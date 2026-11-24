using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BounceBack.Geometry.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class GeometryWorldDemo
    {
        internal const string Root = "Assets/GeometryWorldDemo";
        internal const string ScenePath = Root + "/Echo Court.unity";
        private const string TriggerRunPath = Root + "/Trigger Run.unity";
        private const string Models = Root + "/Models";
        private const string Audio = Root + "/Audio";

        [MenuItem("Tools/Open Geometry Trigger Run")]
        internal static void OpenTriggerRun()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(TriggerRunPath, OpenSceneMode.Single);
            Selection.activeGameObject = LevelAuthoring.FindLevel(scene)?.gameObject;
        }

        internal static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : Build();
            var level = LevelAuthoring.FindLevel(scene);
            if (level)
                Selection.activeGameObject = level.gameObject;
        }

        private static void BuildBatch()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                Build();
            Debug.Log("Geometry demo ready at " + ScenePath);
        }

        private static Scene Build()
        {
            GeometryAssets.EnsureFolder(Root);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = LevelAuthoring.CreateInScene(scene, "Echo Court", false);
            level.designerNotes = "Activate the crossing with the mint switch, then roll through the paired anchors. The apricot pad lifts the ball toward the final arch.";
            level.playHint = "Click the mint switch to open the crossing. Drag the green fin to feel its spring.";
            level.gameplayCamera.transform.position = new Vector3(10.5f, 11.8f, -16.3f);
            level.gameplayCamera.transform.LookAt(new Vector3(0.6f, 1.75f, 0f));
            level.gameplayCamera.orthographicSize = 6.2f;
            level.gameplayCamera.farClipPlane = 85f;
            level.fallHeight = -6f;
            LevelAuthoring.ApplyAtmosphere(level, Atmosphere.Daybreak, false);
            var root = level.transform;
            Transform architecture = LevelAuthoring.NewObject("Floating Courts", root, scene, Vector3.zero, false).transform;
            Transform logic = LevelAuthoring.NewObject("Puzzle Objects", root, scene, Vector3.zero, false).transform;
            var matte = Shader.Find("BounceBack/Geometry Matte");
            Material[] colors = GeometryAssets.Pastels.Select(preset => GeometryAssets.CreateMaterial(preset.Name,
                preset.Color, 0f, 0.38f, 1f, true, Root + "/Materials", matte)).ToArray();

            var west = Place("West Runway", GeometryShape.Platform, new(6.65f, 0.42f, 2.35f), new(-5.5f, 1.29f, 0),
                architecture, colors[1], true);
            var east = Place("East Runway", GeometryShape.Platform, new(7.05f, 0.42f, 2.35f), new(5.85f, 1.29f, 0),
                architecture, colors[2], true);
            Place("West Stone", GeometryShape.Platform, new(6.45f, 0.45f, 2.1f), new(-5.5f, 0.86f, 0), architecture, colors[4]);
            Place("East Stone", GeometryShape.Platform, new(6.85f, 0.45f, 2.1f), new(5.85f, 0.86f, 0), architecture, colors[6]);

            var rail = Model("Ivory Runway Rail", GeometryGenerators.Generate(GeometryShape.Cube, new Vector3(6.65f, 0.11f, 0.11f), 3, 0), colors[0]);
            foreach (float z in new[] { -1.09f, 1.09f })
            {
                GeometryAssets.Instantiate(rail, west.transform, new Vector3(-5.5f, 1.55f, z), true, false, false);
                var eastRail = GeometryAssets.Instantiate(rail, east.transform, new Vector3(5.85f, 1.55f, z), true, false, false);
                eastRail.transform.localScale = new Vector3(1.05f, 1f, 1f);
            }

            var support = Model("Fluted Floating Pier", GeometryGenerators.Generate(GeometryShape.Column, new(1.25f, 2.15f, 1.25f), 10, 0), colors[2]);
            foreach (float x in new[] { -7.4f, -4.1f, 4.0f, 7.7f })
                GeometryAssets.Instantiate(support, architecture, new Vector3(x, -0.35f, 0.16f), true, false, false);
            Place("Lower West Terrace", GeometryShape.Stairs, new(3.2f, 0.8f, 1.75f), new(-6.3f, -0.7f, -2.2f),
                architecture, colors[5], segments: 5);
            Place("Lower East Terrace", GeometryShape.Stairs, new(3.1f, 0.82f, 1.5f), new(6.25f, -0.9f, 2.12f),
                architecture, colors[4], segments: 6);
            var towardTrack = Quaternion.Euler(0f, 90f, 0f);
            var arch = Place("Arrival Arch", GeometryShape.Archway, new(2.5f, 2.65f, 0.42f), new(8.55f, 2.8f, 0),
                architecture, colors[0], segments: 16);
            arch.transform.rotation = towardTrack;
            var startArch = GeometryAssets.Instantiate(arch.model, architecture, new Vector3(-7.7f, 2.8f, 0), true, false, false);
            startArch.name = "Departure Arch";
            startArch.transform.rotation = towardTrack;
            startArch.transform.localScale = new Vector3(1f, 0.84f, 0.82f);

            var crownSource = GeometryDrawingAuthoring.CreateOutline(new[]
            {
                new Vector2(-1.6f, -0.5f), new Vector2(-0.7f, -0.5f), new Vector2(-0.7f, -0.96f),
                new Vector2(0.78f, -0.96f), new Vector2(0.78f, -0.5f), new Vector2(1.65f, -0.5f),
                new Vector2(1.65f, 0.52f), new Vector2(-1.6f, 0.52f)
            }, GeometryAssets.Pastels[3].Color, 0.16f);
            AssetDatabase.CreateAsset(crownSource, Root + "/Court Outline.asset");
            var inks = new List<Color>();
            var crownTopology = GeometryGenerators.Convert(crownSource, DrawingConversion.Solid, 0.25f, 1f, true, inks, out var drawingIssues);
            if (drawingIssues.Count != 0)
                throw new InvalidOperationException(string.Join(" ", drawingIssues));
            var crown = GeometryAssets.CreateModel("Extruded Court Outline", crownTopology, new[] { colors[3] }, crownSource, Models);
            GeometryAssets.Instantiate(crown, architecture, new Vector3(5.8f, 0.78f, -1.34f), true, false, false);

            var vane = GeometryGenerators.Generate(GeometryShape.Cube, new(1.05f, 1.8f, 0.94f), 3, 0f);
            TopologyEdits.Bevel(vane, new TopologyEdge(4, 7), 0.18f, 1);
            var finModel = GeometryAssets.CreateModel("Chiseled Rose Fin", vane, new[] { colors[5], colors[0] }, folder: Models);
            var fin = GeometryAssets.Instantiate(finModel, logic, new Vector3(-3.25f, 1.95f, -1.83f), false, true, false);
            LevelAuthoring.AddBehavior(fin.gameObject, PuzzleItem.ElasticObject, level, false);
            var elasticity = fin.GetComponent<ElasticGeometry>();
            elasticity.localAxis = Vector3.up;
            elasticity.maximumDisplacement = 0.75f;
            elasticity.squashStretch = 0.22f;
            var rotor = Place("Crossing Dial", GeometryShape.Prism, new(1.3f, 0.3f, 1.3f), new(2.95f, 1.95f, 1.8f),
                logic, colors[4], segments: 3);
            var motion = rotor.gameObject.AddComponent<PlatformMotion>();
            motion.automatic = false;

            var switchObj = Place("Mint Switch", GeometryShape.Cylinder, new(1.15f, 0.28f, 1.15f), new(-5.75f, 1.63f, -1.47f),
                logic, colors[1], true, 24);
            var switchCap = Place("Switch Inlay", GeometryShape.Cylinder, new(0.75f, 0.09f, 0.75f), new(-5.75f, 1.79f, -1.47f),
                switchObj.transform, colors[0], false, 24);
            switchCap.name = "Ivory Switch Inlay";
            LevelAuthoring.AddBehavior(switchObj.gameObject, PuzzleItem.Switch, level, false);

            var spawn = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Spawn, new(-7.88f, 1.84f, 0), false).GetComponent<BallSpawn>();
            spawn.transform.rotation = towardTrack;
            spawn.material = colors[3];
            spawn.launchSpeed = 4.1f;
            level.spawn = spawn;
            var goal = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Goal, new(8.62f, 1.95f, 0), false).GetComponent<PuzzleTrigger>();
            level.goal = goal;
            var goalDisc = Place("Goal Inlay", GeometryShape.CircularPlatform, new(1.35f, 0.08f, 1.35f), new(8.55f, 1.53f, 0),
                architecture, colors[3], false, 24);
            goalDisc.name = "Goal Inlay";

            var source = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Anchor, new(-2.73f, 1.84f, 0), false).GetComponent<SpatialAnchor>();
            var destination = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Anchor, new(2.8f, 1.84f, 0), false).GetComponent<SpatialAnchor>();
            source.name = "West Crossing Anchor";
            destination.name = "East Crossing Anchor";
            source.transform.rotation = destination.transform.rotation = towardTrack;
            var connection = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Connection, Vector3.zero, false).GetComponent<SpaceConnection>();
            connection.name = "Echo Crossing";
            connection.source = source;
            connection.destination = destination;
            connection.enabledByLogic = false;
            connection.exitClearance = 0.38f;
            connection.travelTime = 0.28f;
            connection.velocityMode = ConnectionVelocity.AlignToExit;

            Place("West Anchor Inlay", GeometryShape.CircularPlatform, new(0.7f, 0.05f, 1.5f), new(-2.73f, 1.52f, 0),
                architecture, colors[4], segments: 18);
            Place("East Anchor Inlay", GeometryShape.CircularPlatform, new(0.7f, 0.05f, 1.5f), new(2.8f, 1.52f, 0),
                architecture, colors[4], segments: 18);
            var padSurface = Place("Apricot Bounce Pad", GeometryShape.Platform, new(0.88f, 0.1f, 1.55f), new(4.36f, 1.51f, 0),
                logic, colors[3]);
            var pad = padSurface.gameObject.AddComponent<BouncePad>();
            pad.speed = 3.15f;
            var padVolume = padSurface.gameObject.AddComponent<BoxCollider>();
            padVolume.isTrigger = true;
            padVolume.center = new Vector3(0f, 0.31f, 0f);
            padVolume.size = new Vector3(0.85f, 0.85f, 1.55f);

            var orb = Model("Floating Signal", GeometryGenerators.Generate(GeometryShape.Sphere, new(0.33f, 0.33f, 0.33f), 12, 0), colors[0]);
            foreach (Vector3 point in new[] { new Vector3(-1.55f, 1.16f, 0.04f), new(-0.45f, 1.12f, 0.04f),
                new(0.65f, 1.12f, 0.04f), new(1.75f, 1.16f, 0.04f), new(-5.1f, 3.9f, 1.6f), new(6.7f, 4.05f, 1.4f) })
                GeometryAssets.Instantiate(orb, architecture, point, true, false, false);
            Place("Suspended Bridge", GeometryShape.Bridge, new(4.7f, 0.5f, 1.1f), new(0.1f, -0.42f, 0.2f),
                architecture, colors[2]);

            var audio = LevelAuthoring.CreateItem(level, logic, PuzzleItem.Audio, Vector3.zero, false).GetComponent<AdaptivePuzzleAudio>();
            audio.ambient = Clip("Court Air", 4f, t => 0.18f * Tone(110, t) + 0.09f * Tone(165, t) + 0.05f * Tone(220, t));
            audio.activityMusic = Clip("Court Motion", 4f, t => (0.5f + 0.5f * Tone(1, t)) *
                (0.18f * Tone(220, t) + 0.10f * Tone(275, t) + 0.05f * Tone(330, t)));
            audio.rolling = Clip("Rolling Texture", 1f, t => (0.56f + 0.44f * Tone(14, t)) *
                (0.16f * Tone(57, t) + 0.08f * Tone(91, t) + 0.06f * Tone(131, t)));
            AddCue(audio, Cue.Bounce, "Bounce Tone", 0.34f, 230, 365);
            AddCue(audio, Cue.Drag, "Spring Tone", 0.44f, 196, 294);
            AddCue(audio, Cue.Rotation, "Dial Tone", 0.65f, 330, 440);
            AddCue(audio, Cue.Connection, "Crossing Tone", 0.75f, 440, 660);
            AddCue(audio, Cue.Goal, "Arrival Tone", 1.2f, 440, 554.37f, 659.25f);
            motion.audioDirector = elasticity.audioDirector = audio;
            audio.musicVolume = 0.35f;

            var sequence = switchObj.gameObject.AddComponent<PuzzleSequence>();
            sequence.oneShot = true;
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.PlayAudio, target = audio.gameObject, cue = Cue.Rotation });
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.Rotate, target = rotor.gameObject, amount = new(0, 90, 0), duration = 0.55f });
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.Wait, duration = 0.12f });
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.SetConnection, target = connection.gameObject, state = true });
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.SetColor, target = switchCap.gameObject, color = GeometryAssets.Pastels[4].Color });
            sequence.actions.Add(new PuzzleAction { type = PuzzleActionType.ElasticPulse, target = fin.gameObject, intensity = 0.8f });
            var puzzleSwitch = switchObj.GetComponent<PuzzleSwitch>();
            puzzleSwitch.onInteract = sequence;
            puzzleSwitch.oneShot = true;

            if (!LevelAuthoring.Save(level, ScenePath))
                throw new InvalidOperationException("Unity could not save Echo Court.");
            AssetDatabase.SaveAssets();
            return scene;
        }

        private static GeometryObject Place(string name, GeometryShape shape, Vector3 dimensions, Vector3 position, Transform parent,
            Material material, bool collider = false, int segments = 12)
        {
            var model = Model(name, GeometryGenerators.Generate(shape, dimensions, segments, 110f), material);
            return GeometryAssets.Instantiate(model, parent, position, !collider, collider, false);
        }

        private static GeometryMeshAsset Model(string name, MeshTopology mesh, Material material) =>
            GeometryAssets.CreateModel(name, mesh, new[] { material }, folder: Models);

        private static float Tone(float frequency, float time) => Mathf.Sin(2f * Mathf.PI * frequency * time);

        private static void AddCue(AdaptivePuzzleAudio audio, Cue cue, string name, float duration, params float[] frequencies)
        {
            var clip = Clip(name, duration, time =>
            {
                float attack = Mathf.Clamp01(time / 0.028f);
                float release = Mathf.Pow(Mathf.Max(0f, 1f - time / duration), 2.5f);
                float signal = 0f;
                foreach (float frequency in frequencies)
                    signal += Tone(frequency, time) + 0.2f * Tone(frequency * 2f, time);
                return signal * attack * release * 0.32f / frequencies.Length;
            });
            audio.sounds.Add(new AudioCue { kind = cue, clip = clip });
        }

        private static AudioClip Clip(string name, float duration, Func<float, float> sample)
        {
            string path = GeometryAssets.EnsureFolder(Audio) + "/" + name + ".wav";
            var previous = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (previous)
                return previous;
            const int rate = 22050;
            int count = Mathf.RoundToInt(duration * rate);
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                stream.Write(new[] { 'R', 'I', 'F', 'F' });
                stream.Write(36 + count * 2);
                stream.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                stream.Write(16);
                stream.Write((short)1);
                stream.Write((short)1);
                stream.Write(rate);
                stream.Write(rate * 2);
                stream.Write((short)2);
                stream.Write((short)16);
                stream.Write(new[] { 'd', 'a', 't', 'a' });
                stream.Write(count * 2);
                for (int i = 0; i < count; i++)
                    stream.Write((short)(Mathf.Clamp(sample(i / (float)rate), -1f, 1f) * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
