using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class SunlessCourtyardPlayTests
    {
#if UNITY_EDITOR
        private const string ScenePath = "Assets/Scenes/Archive/BounceBack/The Land Without the Sun.unity";

        [UnityTest]
        public IEnumerator SunlessCourtyardOrbitsWithPointerScrollAndController()
        {
            bool runInBackground = Application.runInBackground;
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            var editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                var puzzle = Object.FindFirstObjectByType<SunlessCourtyard>();
                Transform camera = puzzle.view.transform;
                Quaternion start = camera.rotation;
                Quaternion seamView = Quaternion.Euler(9.462f, 90f, 0f);
                mouse.MakeCurrent();
                var origin = new Vector2(420f, 260f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin }.WithButton(MouseButton.Right));
                yield return null;
                var movement = new Vector2(100f, -60f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + movement, delta = movement }.WithButton(MouseButton.Right));
                yield return new WaitForSecondsRealtime(0.45f);
                Assert.That(Quaternion.Angle(camera.rotation, start), Is.GreaterThan(18f), "Right-drag should orbit the camera.");
                Assert.That(camera.eulerAngles.x, Is.GreaterThan(36f), "Vertical drag should change the viewing elevation.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + movement });
                yield return new WaitForSecondsRealtime(0.65f);
                Assert.That(Quaternion.Angle(camera.rotation, Quaternion.Euler(42.2f, 70f, 0f)), Is.LessThan(1f),
                    "Releasing the mouse away from the seam should preserve the chosen view.");

                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin }.WithButton(MouseButton.Right));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin + new Vector2(89f, 140f), delta = new Vector2(89f, 140f) }
                    .WithButton(MouseButton.Right));
                yield return new WaitForSecondsRealtime(0.4f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin });
                yield return new WaitForSecondsRealtime(0.7f);
                Assert.That(Quaternion.Angle(camera.rotation, seamView), Is.LessThan(1f), "Near the seam, releasing the mouse must align the projection.");

                float scroll = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                    Application.platform == RuntimePlatform.WindowsEditor ? 120f : 1f;
                float beforeZoom = puzzle.view.orthographicSize;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin, scroll = new Vector2(0f, scroll) });
                yield return new WaitForSecondsRealtime(0.65f);
                Assert.That(puzzle.view.orthographicSize, Is.LessThan(beforeZoom * 0.94f), "Vertical scroll should zoom like Santorini.");
                Assert.That(Quaternion.Angle(camera.rotation, seamView), Is.LessThan(1f), "Zoom should retain the viewing angle.");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = origin, scroll = new Vector2(scroll, 0f) });
                yield return new WaitForSecondsRealtime(0.9f);
                Assert.That(Quaternion.Angle(camera.rotation, Quaternion.Euler(9.462f, 128f, 0f)), Is.LessThan(1f),
                    "A sideways touchpad swipe should rotate without snapping to a fixed view.");

                puzzle.SetViewInstantly(0);
                pad.MakeCurrent();
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right });
                yield return new WaitForSecondsRealtime(0.32f);
                Assert.That(Quaternion.Angle(camera.rotation, start), Is.GreaterThan(16f), "The right stick should orbit the camera.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return new WaitForSecondsRealtime(0.85f);
                Assert.That(Quaternion.Angle(camera.rotation, seamView), Is.LessThan(1f), "Centering the stick must restore precise alignment.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(pad);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
                Application.runInBackground = runInBackground;
            }
        }
#endif

        [UnityTest]
        public IEnumerator SunlessCourtyardRequiresAlignmentAndRollsToTheExit()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var puzzle = Object.FindFirstObjectByType<SunlessCourtyard>();
            Assert.That(puzzle, Is.Not.Null);
            var level = puzzle.GetComponent<GeometryLevel>();
            yield return null;
            Assert.That(level.Ball, Is.Not.Null);
            Assert.That(puzzle.PlayPrompt, Does.Contain("Turn the camera"));
            foreach (var tablet in new[] { puzzle.tablets[0], puzzle.tablets[1], puzzle.tablets[2], puzzle.controls[0], puzzle.controls[1] })
            {
                Vector3 screenPoint = puzzle.view.WorldToScreenPoint(tablet.position + Vector3.up * 0.05f);
                Assert.That(Physics.Raycast(puzzle.view.ScreenPointToRay(screenPoint), out var hit, 60f) && hit.transform == tablet,
                    Is.True, tablet.name + " must be clickable from the opening view.");
            }
            puzzle.TryLaunch();
            Assert.That(level.Ball.Released, Is.False);
            Assert.That(puzzle.PlayPrompt, Does.Contain("A gap still blocks"));

            puzzle.SeatTablet(3);
            Assert.That(puzzle.tablets[3].gameObject.activeSelf, Is.False);
            Vector3 sunHome = puzzle.tablets[0].position;
            DragTablet(puzzle, 0, 1, Vector2.zero);
            Assert.That(puzzle.tablets[0].position, Is.EqualTo(sunHome), "A wrong symbol must return the tablet to its cradle.");
            Assert.That(puzzle.PlayPrompt, Does.Contain("returned to its cradle"));
            for (int i = 0; i < 3; i++)
            {
                Vector2 nearEdge = Vector2.up * (puzzle.view.pixelHeight * 0.026f);
                DragTablet(puzzle, i, i, nearEdge);
                Assert.That(Vector3.Distance(puzzle.tablets[i].position, puzzle.sockets[i].position + Vector3.up * 0.14f),
                    Is.LessThan(0.001f), puzzle.tablets[i].name + " should seat after dragging and releasing near its socket.");
                Assert.That(puzzle.PlayPrompt, Is.EqualTo("Tablet seated."));
            }
            puzzle.Operate(0);
            yield return new WaitForSeconds(0.75f);
            Assert.That(Quaternion.Angle(puzzle.westTurntable.localRotation, Quaternion.identity), Is.LessThan(1f));
            puzzle.Operate(1);
            yield return new WaitForSeconds(1f);
            puzzle.Operate(0);
            yield return new WaitForSeconds(1.4f);
            Assert.That(puzzle.CanCross, Is.False, "The entrance view must leave the physical gap open.");
            puzzle.SetViewInstantly(1);
            yield return null;
            Assert.That(puzzle.bridge.activeSelf, Is.True, "The correct view and mechanism state must enable a physical span.");

            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.position = Vector3.Lerp(puzzle.seamStart.position, puzzle.seamEnd.position, 0.5f) + Vector3.up * 0.3f;
            obstruction.transform.localScale = Vector3.one * 0.17f;
            yield return new WaitForFixedUpdate();
            Assert.That(puzzle.CanCross, Is.False, "A physical obstruction must invalidate the seam.");
            Object.Destroy(obstruction);
            yield return null;
            puzzle.TryLaunch();
            yield return WaitFor(() => !level.Ball.Released && level.Ball.Body.position.x > -0.3f,
                "The ball must roll across the first span to the center checkpoint.", level);

            puzzle.Operate(2);
            yield return new WaitForSeconds(0.95f);
            Assert.That(puzzle.rearRoad.activeSelf);
            puzzle.TryLaunch();
            yield return WaitFor(() => puzzle.tablets[3].gameObject.activeSelf, "The ball must reach the star gallery.", level);
            puzzle.SeatTablet(3);
            yield return new WaitForSeconds(1.05f);
            Assert.That(puzzle.foldRoad.activeSelf);
            Assert.That(puzzle.CanFinish, Is.False, "The rear bridge must return before finishing.");
            puzzle.Operate(2);
            yield return new WaitForSeconds(0.95f);
            Assert.That(puzzle.CanFinish);
            puzzle.TryLaunch();
            yield return WaitFor(() => level.Completed, "The ball must cross the unfolded route and touch the goal.", level);
            Assert.That(puzzle.beacon.activeSelf);
            Assert.That(puzzle.PlayPrompt, Is.EqualTo("The ball reached the light."));
#else
            yield break;
#endif
        }

        private static IEnumerator WaitFor(System.Func<bool> predicate, string message, GeometryLevel level)
        {
            float deadline = Time.time + 7f;
            while (!predicate() && Time.time < deadline)
                yield return null;
            Assert.That(predicate(), Is.True, message + " Ball at " + level.Ball.Body.position);
        }

        private static void DragTablet(SunlessCourtyard puzzle, int tablet, int socket, Vector2 dropOffset)
        {
            Physics.SyncTransforms();
            Vector2 pickup = puzzle.view.WorldToScreenPoint(puzzle.tablets[tablet].position + Vector3.up * 0.05f);
            Vector2 drop = (Vector2)puzzle.view.WorldToScreenPoint(puzzle.sockets[socket].position + Vector3.up * 0.04f) + dropOffset;
            Assert.That(puzzle.HandlePointer(EventType.MouseDown, pickup), Is.True, puzzle.tablets[tablet].name + " must pick up.");
            Assert.That(puzzle.HandlePointer(EventType.MouseDrag, drop), Is.True);
            if (tablet == socket)
                Assert.That(puzzle.PlayPrompt, Is.EqualTo("Release to seat the tablet."));
            Assert.That(puzzle.HandlePointer(EventType.MouseUp, drop), Is.True);
        }
    }
}
