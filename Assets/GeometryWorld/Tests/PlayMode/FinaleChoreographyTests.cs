#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class FinaleChoreographyTests
    {
        private Scene fixture;

        [Test]
        public void LandingOvershootIsLocalAndRestoresBothEndpoints()
        {
            var root = new GameObject("Finale easing test");
            var child = new GameObject("Anchor");
            child.transform.SetParent(root.transform, false);
            var pose = root.AddComponent<GeometryPose>();
            pose.tracks = new[] { new PoseTrack { target = child.transform, translation = Vector3.right, overshoot = 0.08f } };
            pose.Apply(0f);
            Assert.That(child.transform.localPosition.x, Is.Zero);
            pose.Apply(0.90f);
            Assert.That(child.transform.localPosition.x, Is.GreaterThan(1f));
            pose.Apply(1f);
            Assert.That(child.transform.localPosition.x, Is.EqualTo(1f).Within(0.00001f));
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator FourAnchorsFoldInOrderWithSplashBellAndBallInFrame()
        {
            yield return Open();
            var flow = fixture.GetRootGameObjects().Select(root => root.GetComponent<CampaignPlayground>()).First(item => item);
            var level = flow.level;
            var finale = flow.finale;
            Assert.That(finale, Is.Not.Null);
            Assert.That(finale.AnchorCount, Is.EqualTo(4));
            Assert.That(finale.plateHold, Is.EqualTo(0.30f).Within(0.001f));
            Assert.That(finale.waterfallPause, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(finale.anchorStagger, Is.InRange(0.15f, 0.25f));
            Assert.That(finale.foldingCameraSize, Is.InRange(13f, 15f));
            flow.rig.view.aspect = 16f / 9f;
            yield return null;
            yield return null;

            var plate = flow.completionPlate;
            // The finale suspends every draggable mechanism, so the Release lock is read from its marker instead.
            bool releaseLockedBefore = level.wallLock && level.wallLock.activeSelf;
            level.Ball.Hold(plate.surface.position + Vector3.up * (level.spawn.diameter * 0.5f), Quaternion.identity);
            float started = Time.unscaledTime;
            float maximumSize = 0f;
            float minimumMargin = 1f;
            float[] firstMotion = Enumerable.Repeat(-1f, 4).ToArray();
            Vector3[] rest = finale.folding.tracks.Skip(1).Select(track => track.target.localPosition).ToArray();
            bool overshot = false;
            float splashAt = -1f, bellAt = -1f;
            finale.WaterBurst += () => splashAt = Time.unscaledTime - started;
            finale.Folded += () => bellAt = Time.unscaledTime - started;
            level.ReachGoal();
            while (!flow.city.Active && Time.unscaledTime - started < 12f)
            {
                Camera view = flow.rig.view;
                Vector3 viewport = view.WorldToViewportPoint(level.Ball.Body.position);
                minimumMargin = Mathf.Min(minimumMargin, viewport.z > 0f
                    ? Mathf.Min(Mathf.Min(viewport.x, 1f - viewport.x), Mathf.Min(viewport.y, 1f - viewport.y)) : -1f);
                maximumSize = Mathf.Max(maximumSize, view.orthographicSize);
                for (int i = 0; i < 4; i++)
                {
                    var track = finale.folding.tracks[i + 1];
                    if (firstMotion[i] < 0f && Vector3.Distance(track.target.localPosition, rest[i]) > 0.0001f)
                        firstMotion[i] = Time.unscaledTime - started;
                    Vector3 flatTranslation = Vector3.ProjectOnPlane(track.translation, Vector3.up);
                    float projected = Vector3.Dot(Vector3.ProjectOnPlane(track.target.localPosition - track.restPosition, Vector3.up), flatTranslation);
                    bool translationOvershoot = projected > flatTranslation.sqrMagnitude + 0.0001f;
                    bool rotationOvershoot = Quaternion.Angle(track.target.localRotation, track.restRotation) > track.rotation.magnitude + 0.01f;
                    overshot |= translationOvershoot || rotationOvershoot;
                }
                yield return null;
            }

            Assert.That(flow.city.Active, Is.True, "Completion must open the assembled road.");
            Assert.That(finale.SplashCount, Is.EqualTo(1));
            Assert.That(finale.BellCount, Is.EqualTo(1));
            float expectedSplash = finale.plateHold + finale.FoldStart;
            float expectedBell = finale.plateHold + finale.FoldComplete;
            Assert.That(splashAt, Is.GreaterThanOrEqualTo(expectedSplash - 0.04f));
            Assert.That(splashAt, Is.LessThan(expectedSplash + 0.50f), "Allow one headless frame of latency, not an early burst.");
            Assert.That(bellAt, Is.GreaterThanOrEqualTo(expectedBell - 0.04f));
            Assert.That(bellAt, Is.LessThan(expectedBell + 0.50f), "Allow one headless frame of latency, not an early bell.");
            Assert.That(bellAt, Is.GreaterThan(splashAt));
            Assert.That(maximumSize, Is.InRange(13f, 15f));
            Assert.That(minimumMargin, Is.GreaterThan(0.04f), "Ball must stay inside the safe frame throughout completion.");
            Assert.That(overshot, Is.True, "Anchor tracks need a visible but mild landing overshoot.");
            for (int i = 0; i < 4; i++)
            {
                Assert.That(firstMotion[i], Is.GreaterThanOrEqualTo(finale.FoldStart - 0.04f));
                if (i > 0)
                    Assert.That(firstMotion[i] - firstMotion[i - 1], Is.EqualTo(finale.anchorStagger).Within(0.055f));
            }
            Assert.That(level.wallLock && level.wallLock.activeSelf, Is.EqualTo(releaseLockedBefore),
                "Finale must not change the existing Release wall-lock state.");
            Assert.That(Vector3.Distance(level.Ball.Body.position, flow.city.Dock(0)), Is.LessThan(0.005f));
            Assert.That(flow.city.roadAssembly.tracks.All(track => Vector3.Distance(track.target.localPosition, track.restPosition + track.translation) < 0.0001f), Is.True);

            Assert.That(flow.city.crossingDuration, Is.EqualTo(0.15f).Within(0.001f));
            Vector3 a = flow.city.waypoints[3].position;
            Vector3 b = flow.city.waypoints[4].position;
            float angle = Vector3.Angle((b - a).normalized, flow.city.cameraOffset.normalized);
            angle = Mathf.Min(angle, 180f - angle);
            Assert.That(angle, Is.InRange(8f, 12f));
            Assert.That(PixelGap(a, b, flow.rig.view), Is.LessThanOrEqualTo(4f));
            var official = new GameObject("Official seam test camera").AddComponent<Camera>();
            official.orthographic = true;
            official.orthographicSize = 9.5f;
            official.aspect = 16f / 9f;
            official.transform.position = new Vector3(-0.05f, 0.95f, 0.25f) + new Vector3(12f, 15f, -15f);
            official.transform.rotation = Quaternion.LookRotation(new Vector3(-12f, -15f, 15f));
            Assert.That(PixelGap(a, b, official), Is.LessThanOrEqualTo(4f));
            Object.DestroyImmediate(official.gameObject);
        }

        private static float PixelGap(Vector3 a, Vector3 b, Camera view)
        {
            const float width = 1920f, height = 1080f;
            Vector2 Project(Vector3 point)
            {
                Vector3 local = view.transform.InverseTransformPoint(point);
                return new Vector2((0.5f + local.x / (2f * view.orthographicSize * view.aspect)) * width,
                    (0.5f + local.y / (2f * view.orthographicSize)) * height);
            }
            return Vector2.Distance(Project(a), Project(b));
        }

        private IEnumerator Open()
        {
            fixture = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Archive/BounceBackSystems/Santorini Gameplay.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            if (fixture.IsValid() && fixture.isLoaded && SceneManager.GetActiveScene() != fixture)
                yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
#endif
