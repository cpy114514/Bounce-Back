#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class SantoriniPlayTests
    {
        private const string ScenePath = "Assets/Scenes/Archive/BounceBack/Santorini.unity";
        private Scene fixture;

        [UnityTest]
        public IEnumerator CenteredMechanismsBounceTheBallTwiceThenDeflectItToTheGoal()
        {
            OpenLevel();
            yield return null;
            yield return null;
            var level = FindLevel();
            var rails = level.GetComponentsInChildren<ElasticGeometry>();
            Assert.That(rails.Length, Is.EqualTo(3));
            var contacts = new List<string>();
            foreach (var rail in rails)
            {
                rail.BallRebounded += (element, ball) => contacts.Add($"{element.name} {ball.Body.position:F2} {ball.Body.linearVelocity:F2}");
                DragToMarker(rail, level.gameplayCamera);
            }
            yield return new WaitForSeconds(0.9f);
            Assert.That(rails.Select(rail => rail.CurrentOffset), Is.All.InRange(-0.04f, 0.04f));

            level.ReleaseBall();
            var trace = new StringBuilder();
            int tick = 0;
            while (level.Ball.Released && tick < 500)
            {
                yield return new WaitForFixedUpdate();
                if (tick++ % 10 == 0)
                    trace.AppendLine($"{tick}: {level.Ball.Body.position:F2} {level.Ball.Body.linearVelocity:F2}");
            }

            string details = string.Join("\n", contacts) + "\n" + trace;
            Assert.That(level.Completed, Is.True, details);
            Assert.That(contacts, Has.Count.EqualTo(3), details);
            Assert.That(contacts[0], Does.StartWith("Slime Block 1"));
            Assert.That(contacts[1], Does.StartWith("Slime Block 2"));
            Assert.That(contacts[2], Does.StartWith("Deflector"));
        }

        [UnityTest]
        public IEnumerator MissedSlimeRetriesAndKeepsRailAdjustmentMadeDuringPlay()
        {
            OpenLevel();
            yield return null;
            yield return null;
            var level = FindLevel();
            var deflector = level.GetComponentsInChildren<ElasticGeometry>().Single(rail => rail.name == "Deflector");
            level.ReleaseBall();
            Assert.That(level.Ball.Released, Is.True);
            deflector.SetTargetOffset(-0.4f);
            float deadline = Time.time + 8f;
            while (!level.Retrying && Time.time < deadline)
                yield return null;
            Assert.That(level.Retrying, Is.True);
            Assert.That(level.Ball.Body.detectCollisions, Is.False);
            level.ReleaseBall();
            Assert.That(level.Ball.Released, Is.False);
            yield return new WaitForSeconds(level.retryEffect.duration + 0.1f);
            Assert.That(level.Retrying, Is.False);
            Assert.That(level.Ball.Body.detectCollisions, Is.True);
            Assert.That(level.Ball.Body.position, Is.EqualTo(level.spawn.StartPosition));
            Assert.That(deflector.CurrentOffset, Is.EqualTo(-0.4f).Within(0.01f));
        }

        private static void DragToMarker(ElasticGeometry rail, Camera camera)
        {
            Vector3 position = rail.transform.position;
            Vector3 home = position - rail.transform.TransformDirection(rail.localAxis) * rail.CurrentOffset;
            Ray start = camera.ScreenPointToRay(camera.WorldToScreenPoint(position));
            Ray end = camera.ScreenPointToRay(camera.WorldToScreenPoint(home));
            Assert.That(rail.BeginDrag(start, camera.transform.forward), Is.True);
            rail.Drag(end);
            rail.EndDrag();
        }

        private void OpenLevel() =>
            fixture = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));

        private GeometryLevel FindLevel() =>
            fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(level => level);

        [UnityTearDown]
        public IEnumerator CloseLevel()
        {
            if (fixture.IsValid() && fixture.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
#endif
