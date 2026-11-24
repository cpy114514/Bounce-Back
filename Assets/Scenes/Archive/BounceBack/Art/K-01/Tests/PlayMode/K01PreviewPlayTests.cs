using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BounceBack.GeometryWorld;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BounceBack.K01.Tests
{
    /// <summary>Runs against the K-01 preview copy, never the formal Santorini scene.</summary>
    public sealed class K01PreviewPlayTests
    {
        private const string PreviewScenePath = "Assets/Scenes/Archive/BounceBack/Art/K-01/Preview/K01_Santorini_Preview.unity";
        private Scene fixture;

        [UnityTest]
        public IEnumerator DragThreeMechanismsToMarkersAndCompleteTwoFullRoutes()
        {
            fixture = EditorSceneManager.LoadSceneInPlayMode(PreviewScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(component => component);
            var rails = level.GetComponentsInChildren<ElasticGeometry>();
            Assert.That(rails.Length, Is.EqualTo(3));
            foreach (var rail in rails)
                DragToMarker(rail, level.gameplayCamera);
            yield return new WaitForSeconds(0.9f);
            Assert.That(rails.Select(rail => rail.CurrentOffset), Is.All.InRange(-0.04f, 0.04f));
            var results = new StringBuilder();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0)
                {
                    level.ResetBall(false);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                }
                var contacts = new List<string>();
                void Rebounded(ElasticGeometry element, BallBody ball) =>
                    contacts.Add($"{element.name} at {ball.Body.position:F2}");
                foreach (var rail in rails)
                    rail.BallRebounded += Rebounded;
                level.ReleaseBall();
                int tick = 0;
                while (level.Ball.Released && tick < 650)
                {
                    yield return new WaitForFixedUpdate();
                    if (tick++ % 20 == 0)
                        results.AppendLine($"Attempt {attempt + 1}, tick {tick}: {level.Ball.Body.position:F2} {level.Ball.Body.linearVelocity:F2}");
                }
                foreach (var rail in rails)
                    rail.BallRebounded -= Rebounded;
                results.AppendLine($"Attempt {attempt + 1}: completed={level.Completed}; contacts={string.Join(" -> ", contacts)}");
                Assert.That(level.Completed, Is.True, results.ToString());
                Assert.That(contacts, Has.Count.EqualTo(3), results.ToString());
                Assert.That(contacts[0], Does.StartWith("Slime Block 1"));
                Assert.That(contacts[1], Does.StartWith("Slime Block 2"));
                Assert.That(contacts[2], Does.StartWith("Deflector"));
            }
            Debug.Log("K01_TWO_ROUTE_RUNS_PASSED\n" + results);
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

        [UnityTearDown]
        public IEnumerator ClosePreview()
        {
            if (fixture.IsValid() && fixture.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
