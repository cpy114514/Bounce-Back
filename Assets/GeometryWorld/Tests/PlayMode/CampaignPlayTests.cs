#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class CampaignPlayTests
    {
        private int? originalProgress;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousRunInBackground;
        private bool previousReducedMotion;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;

        [Test]
        public void SaveRestoresSelectionAndOnlyAcceptsUnlockedMaterialization()
        {
            var progress = new CampaignProgress(0b1100);
            Assert.That(progress.CompletedCount, Is.Zero);
            Assert.That(progress.Complete(2), Is.False);
            Assert.That(progress.Materialize(1), Is.False);
            Assert.That(progress.MoveTo(4), Is.False);
            for (int level = 0; level < CampaignProgress.LevelCount; level++)
            {
                Assert.That(progress.Complete(level), Is.True);
                Assert.That(progress.Materialize(level), Is.True);
                Assert.That(progress.Materialize(level), Is.False);
                Assert.That(progress.Complete(level), Is.False);
            }
            Assert.That(progress.MoveTo(2), Is.True);
            progress.MarkFinaleSeen();
            var restored = new CampaignProgress(progress.Encode());
            Assert.That(restored.CompletedCount, Is.EqualTo(4));
            Assert.That(restored.FinaleSeen, Is.True);
            Assert.That(restored.Position, Is.EqualTo(2));
            Assert.That(restored.IsMaterialized(3), Is.True);
            Assert.That(restored.IsUnlocked(4), Is.False);
        }

        [UnityTest]
        public IEnumerator FirstLaunchFoldsTheCourtyardAndResumesFromTheSavedDistrict()
        {
            originalProgress = PlayerPrefs.GetInt(CampaignProgress.SaveKey, -1);
            previousReducedMotion = CampaignSettings.ReducedMotion;
            previousRunInBackground = Application.runInBackground;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            CampaignSettings.ReducedMotion = false;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            new CampaignProgress(0).Save();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            SceneManager.LoadSceneAsync(CampaignScenes.Menu);

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == CampaignScenes.SystemsScene, 6f, "tutorial scene");
            var flow = Object.FindFirstObjectByType<CampaignPlayground>();
            yield return WaitUntil(() => flow.InPuzzle, 3f, "preparation");
            var level = flow.level;
            var ball = level.Ball;
            var city = flow.city;
            Assert.That(ball.Body.isKinematic && !ball.Released, Is.True);

            Quaternion overviewAngle = flow.rig.overview.rotation;
            yield return OrbitWithMouse();
            Assert.That(Quaternion.Angle(flow.rig.view.transform.rotation, overviewAngle), Is.GreaterThan(8f));
            yield return Press(Key.Home);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(Quaternion.Angle(flow.rig.view.transform.rotation, overviewAngle), Is.LessThan(1f));
            yield return ZoomAndReset(flow.rig.view, flow.rig.overviewSize, 120f);

            yield return Press(Key.Escape);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(flow.ReturnToCity(), Is.True);
            yield return CheckCarrier(flow, 4f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(city.Active && city.Selected == 0, Is.True);
            yield return WaitUntil(() => CampaignProgress.Load().IsMaterialized(0), 4f, "first district materialization saved");
            Assert.That(city.districts[0].entrance.wire.activeSelf, Is.False);
            yield return ZoomAndReset(flow.rig.view, city.cameraSize, -120f);

            yield return Press(Key.E);
            yield return CheckCarrier(flow, 5f);
            Assert.That(flow.InPuzzle && !ball.Released, Is.True);
            Assert.That(ball, Is.SameAs(level.Ball));

            foreach (var mechanism in level.GetComponentsInChildren<ElasticGeometry>().Where(item => item.draggable))
                mechanism.SetTargetOffset(0f);
            yield return new WaitForSeconds(0.9f);
            int blocks = 0;
            city.RoadBlockPlaced += _ => blocks++;
            var wall = level.preparationWall.transform;
            var plate = level.goal.GetComponent<GoalPressurePlate>();
            var wallSize = wall.lossyScale;
            var plateSize = plate.surface.lossyScale;
            yield return Press(Key.Space);
            yield return WaitUntil(() => ball.Released, 1.5f, "starter piston release");
            yield return WaitUntil(() => flow.CarryingPlatform, 11f, "plate carrying the ball");
            Vector3 seat = CarrierSeat(flow);
            float deadline = Time.realtimeSinceStartup + 8f;
            bool folded = false;
            while (flow.CarryingPlatform && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Vector3.Distance(CarrierSeat(flow), seat), Is.LessThan(0.005f));
                Assert.That(wall.gameObject.activeInHierarchy && plate.surface.gameObject.activeInHierarchy, Is.True);
                if (wall.lossyScale.x < wallSize.x * 0.8f)
                {
                    folded = true;
                    Assert.That(plate.surface.lossyScale.x, Is.LessThan(plateSize.x * 0.8f));
                }
                yield return null;
            }
            Assert.That(folded, Is.True);
            yield return WaitUntil(() => city.Active, 3f, "rollout onto the assembled road");
            Assert.That(blocks, Is.EqualTo(city.roadAssembly.tracks.Length));
            Assert.That(flow.finale.BellCount, Is.EqualTo(1));
            Assert.That(wall.gameObject.activeInHierarchy && plate.surface.gameObject.activeInHierarchy, Is.True);
            Assert.That(level.Ball, Is.SameAs(ball));
            Assert.That(CampaignProgress.Load().CompletedCount, Is.EqualTo(1));
            Assert.That(CampaignScenes.Enter(1), Is.False);
            Assert.That(city.districts[1].entrance.wire.activeSelf, Is.True);

            yield return Press(Key.D);
            yield return WaitUntil(() => city.Velocity > 2f, 2f, "forward acceleration");
            yield return Press(Key.A);
            Assert.That(city.Velocity, Is.GreaterThan(0f));
            Assert.That(city.Enter(), Is.False);
            yield return WaitUntil(() => city.Velocity < -0.1f, 2f, "reverse acceleration");
            yield return WaitUntil(() => city.AtEntrance && city.Selected == 0, 3f, "first dock");

            yield return Press(Key.RightArrow);
            yield return WaitUntil(() => city.Crossing, 7f, "perspective seam");
            Vector2 prior = flow.rig.view.WorldToViewportPoint(ball.Body.position);
            float gapStart = Time.unscaledTime;
            int frames = 0;
            float maximumScreenStep = 0f;
            while (city.Crossing && Time.unscaledTime - gapStart < 0.50f)
            {
                yield return null;
                Vector2 next = flow.rig.view.WorldToViewportPoint(ball.Body.position);
                maximumScreenStep = Mathf.Max(maximumScreenStep, Vector2.Distance(next, prior));
                prior = next;
                frames++;
            }
            Assert.That(frames, Is.GreaterThan(0));
            Assert.That(city.Crossing, Is.False, "The seam crossing must finish rather than time out.");
            Assert.That(maximumScreenStep * 1080f, Is.LessThanOrEqualTo(4f), "No frame may jump across the perspective seam.");
            Assert.That(city.crossingDuration, Is.EqualTo(0.15f).Within(0.001f));
            // This is only a sampling budget for the headless runner; the authored crossing duration is asserted above.
            Assert.That(Time.unscaledTime - gapStart, Is.LessThan(0.50f));
            yield return WaitUntil(() => city.AtEntrance && city.Selected == 1, 4f, "locked second gate");
            Assert.That(city.Enter(), Is.False);
            Assert.That(CampaignProgress.Load().Position, Is.EqualTo(1));

            Assert.That(CampaignScenes.OpenMenu(), Is.True);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == CampaignScenes.Menu, 5f, "return to menu");
            yield return new WaitForSeconds(1.45f);
            yield return Press(Key.Tab);
            yield return Press(Key.Enter);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == CampaignScenes.SystemsScene, 6f, "continue scene");
            flow = Object.FindFirstObjectByType<CampaignPlayground>();
            city = flow.city;
            yield return WaitUntil(() => city.Active && city.Selected == 1, 3f, "continue at the saved gate");
            Assert.That(city.districts[1].entrance.wire.activeSelf, Is.True);
            yield return Press(Key.LeftArrow);
            yield return WaitUntil(() => city.AtEntrance && city.Selected == 0, 8f, "completed district");
            yield return Press(Key.E);
            yield return CheckCarrier(flow, 5f);
            Assert.That(flow.InPuzzle && !flow.level.Ball.Released, Is.True);
        }

        private static IEnumerator CheckCarrier(CampaignPlayground flow, float limit)
        {
            yield return WaitUntil(() => flow.CarryingPlatform, limit, "moving platform");
            Vector3 seat = CarrierSeat(flow);
            float deadline = Time.realtimeSinceStartup + limit;
            while (flow.InTransition && Time.realtimeSinceStartup < deadline)
            {
                if (flow.CarryingPlatform)
                {
                    Assert.That(flow.level.Ball.Body.isKinematic, Is.True);
                    Assert.That(Vector3.Distance(CarrierSeat(flow), seat), Is.LessThan(0.005f));
                }
                yield return null;
            }
            Assert.That(flow.InTransition, Is.False);
        }

        private static Vector3 CarrierSeat(CampaignPlayground flow) => Quaternion.Inverse(flow.CarryingPlatform.rotation) *
            (flow.level.Ball.Body.position - flow.CarryingPlatform.position);
        private IEnumerator OrbitWithMouse()
        {
            mouse.MakeCurrent();
            Vector2 point = new Vector2(340, 220);
            Vector2 motion = new Vector2(145, 35);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(mouse.rightButton.isPressed, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + motion, delta = motion }.WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(0.35f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + motion });
            yield return null;
        }

        private IEnumerator Press(Key key)
        {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(0.075f);
            Assert.That(keyboard[key].isPressed, Is.True, $"Virtual key {key} was not delivered.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.045f);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float seconds, string operation)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(predicate(), Is.True, $"Timed out waiting for {operation}.");
        }

        private IEnumerator ZoomAndReset(Camera view, float defaultSize, float scroll)
        {
            InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0f, scroll));
            yield return null;
            yield return WaitUntil(() => (defaultSize - view.orthographicSize) * Mathf.Sign(scroll) > defaultSize * 0.06f,
                2f, "wheel zoom");
            yield return Press(Key.Home);
            yield return WaitUntil(() => Mathf.Abs(view.orthographicSize - defaultSize) < 0.07f, 2f, "reset zoom");
        }

        [UnityTearDown]
        public IEnumerator RestoreEnvironment()
        {
            if (originalProgress == null)
                yield break;
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (mouse != null)
                InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            Application.runInBackground = previousRunInBackground;
            CampaignSettings.ReducedMotion = previousReducedMotion;
            if (originalProgress == -1)
                PlayerPrefs.DeleteKey(CampaignProgress.SaveKey);
            else
                PlayerPrefs.SetInt(CampaignProgress.SaveKey, originalProgress.Value);
            PlayerPrefs.Save();
            Scene previous = SceneManager.GetActiveScene();
            if (previous.name == CampaignScenes.Menu || previous.name == CampaignScenes.SystemsScene)
            {
                Scene empty = SceneManager.CreateScene("Campaign test cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(previous);
            }
            originalProgress = null;
        }
    }
}
#endif
