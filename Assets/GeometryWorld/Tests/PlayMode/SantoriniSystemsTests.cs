#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class SantoriniSystemsTests
    {
        private const string Path = "Assets/Scenes/Archive/BounceBackSystems/Santorini Gameplay.unity";
        private Scene fixture;
        private GeometryLevel level;

        [UnityTest]
        public IEnumerator PlatformHintShowsA3DArrowAndBouncesTheModelTwice()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var hints = new PlatformHints();
            try
            {
                var source = target.GetComponent<MeshRenderer>();
                var collider = target.GetComponent<BoxCollider>();
                Vector3 originalPosition = target.transform.position;
                Bounds originalBounds = collider.bounds;
                hints.Play(new[] { target.transform, target.transform });

                var model = target.transform.Find("Platform Hint Model");
                var arrow = target.transform.Find("Platform Hint Arrow");
                Assert.That(model, Is.Not.Null);
                Assert.That(arrow, Is.Not.Null);
                Assert.That(target.transform.childCount, Is.EqualTo(2));
                Assert.That(arrow.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.GreaterThan(0f));
                Assert.That(arrow.GetComponent<Collider>(), Is.Null);
                Assert.That(source.forceRenderingOff, Is.True);

                hints.Update(true, 0.23f);
                float firstHeight = model.localPosition.y;
                Assert.That(firstHeight, Is.GreaterThan(0.2f));
                Assert.That(arrow.position.y, Is.GreaterThan(collider.bounds.max.y));
                hints.Update(true, 0.23f);
                Assert.That(model.localPosition.y, Is.EqualTo(0f).Within(0.001f));
                hints.Update(true, 0.17f);
                Assert.That(model.localPosition.y, Is.GreaterThan(0.1f).And.LessThan(firstHeight));
                Assert.That(collider.bounds, Is.EqualTo(originalBounds));
                Assert.That(target.transform.position, Is.EqualTo(originalPosition));

                hints.Update(true, 0.18f);
                Assert.That(source.forceRenderingOff, Is.False);
                Assert.That(model.gameObject.activeSelf, Is.False);
                Assert.That(arrow.gameObject.activeSelf, Is.False);
                yield return null;
                Assert.That(target.transform.childCount, Is.Zero);
            }
            finally
            {
                hints.Dispose();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator TwoChargedPadsAndTheLiftGateSendTheBallOntoTheWaterfallPlate()
        {
            yield return Open();
            var rig = level.GetComponent<PuzzleCameraRig>();
            Assert.That(rig.overviewSize, Is.LessThanOrEqualTo(10f));
            Assert.That(1080f * level.spawn.diameter / (2f * rig.overviewSize), Is.GreaterThanOrEqualTo(40f));
            var rails = level.GetComponentsInChildren<ElasticGeometry>();
            Assert.That(rails.Count(item => item.draggable), Is.EqualTo(3));
            var pool = rails.Single(item => item.name == "Elastic Pool Surface");
            var terrace = rails.Single(item => item.name == "Terrace Spring Pad");
            var wall = level.preparationWall;
            var reflector = wall.GetComponent<PassiveReflector>();
            var plate = level.goal.GetComponent<GoalPressurePlate>();
            var contacts = new List<string>();
            float charge = 0f, bounce = 0f, terraceCharge = 0f, start = 0f;
            Vector3 cameraAtCharge = default;
            Quaternion angleAtCharge = default;
            float zoomAtCharge = 0f;
            pool.BallCompressed += (_, ball) =>
            {
                charge = Time.fixedTime;
                cameraAtCharge = rig.view.transform.position;
                angleAtCharge = rig.view.transform.rotation;
                zoomAtCharge = rig.view.orthographicSize;
                contacts.Add("pool");
                Assert.That(ball.InTransition && ball.Body.isKinematic, Is.True);
            };
            pool.BallRebounded += (_, _) =>
            {
                bounce = Time.fixedTime;
                Assert.That(bounce - charge, Is.InRange(0.18f, 0.28f));
            };
            reflector.Reflected += (_, incoming, outgoing) =>
            {
                contacts.Add("wall");
                Assert.That(outgoing.magnitude, Is.LessThanOrEqualTo(incoming.magnitude + 0.001f));
            };
            terrace.BallCompressed += (_, _) =>
            {
                contacts.Add("terrace pad charge");
                terraceCharge = Time.fixedTime;
            };
            terrace.BallRebounded += (_, _) =>
            {
                contacts.Add("terrace pad rebound");
                Assert.That(Time.fixedTime - terraceCharge, Is.InRange(0.18f, 0.28f));
            };
            foreach (var rail in rails.Where(item => item.draggable))
                rail.SetTargetOffset(0f);
            yield return new WaitForSeconds(0.85f);
            var camera = level.gameplayCamera;
            foreach (var rail in rails.Where(item => item.draggable))
            {
                Ray ray = camera.ScreenPointToRay(camera.WorldToScreenPoint(rail.transform.position));
                Assert.That(Physics.Raycast(ray, out RaycastHit hit, camera.farClipPlane, ~0, QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(hit.collider.GetComponentInParent<ElasticGeometry>(), Is.SameAs(rail), $"{rail.name} must be reachable in the overview");
            }
            start = Time.fixedTime;
            level.ReleaseBall();
            Assert.That(rails.Where(item => item.draggable).All(item => !item.Locked), Is.True);
            yield return WaitForPiston();
            var trace = new System.Text.StringBuilder();
            int step = 0;
            while (level.Ball.Released && Time.fixedTime - start < 8.02f)
            {
                yield return new WaitForFixedUpdate();
                if (step++ % 10 == 0)
                    trace.AppendLine($"{Time.fixedTime-start:F2} {level.Ball.Body.position:F2} {level.Ball.Body.linearVelocity:F2}");
                if (charge > 0f && bounce == 0f)
                {
                    Assert.That(rig.view.transform.position, Is.EqualTo(cameraAtCharge));
                    Assert.That(Quaternion.Angle(rig.view.transform.rotation, angleAtCharge), Is.LessThan(0.02f));
                    Assert.That(rig.view.orthographicSize, Is.EqualTo(zoomAtCharge));
                }
            }
            float elapsed = Time.fixedTime - start;
            Assert.That(level.Completed, Is.True, trace.ToString());
            Assert.That(contacts, Is.EqualTo(new[] { "pool", "wall", "terrace pad charge", "terrace pad rebound" }));
            Assert.That(bounce - start, Is.LessThanOrEqualTo(3f));
            Assert.That(elapsed, Is.LessThanOrEqualTo(8f));
            yield return new WaitForSeconds(0.24f);
            Assert.That(plate.Pressed && plate.litPattern.activeSelf, Is.True);
            Assert.That(plate.pulses.Any(pulse => pulse.enabled), Is.True);
            TestContext.WriteLine($"Release to first bounce {bounce-start:F2}s, second charge {terraceCharge-start:F2}s, and plate {elapsed:F2}s.");
        }

        [UnityTest]
        public IEnumerator MovingTheSecondPadOffCourseDuringFlightCausesARetryAndKeepsTheSetting()
        {
            yield return Open();
            var wall = level.preparationWall;
            var pads = level.GetComponentsInChildren<ElasticGeometry>().Where(item => item.bounceBall).ToArray();
            var pool = pads.Single(item => item.name == "Elastic Pool Surface");
            var terrace = pads.Single(item => item.name == "Terrace Spring Pad");
            pool.SetTargetOffset(0f);
            wall.SetTargetOffset(0f);
            terrace.SetTargetOffset(0f);
            yield return new WaitForSeconds(0.85f);
            bool reflected = false;
            bool secondBounce = false;
            wall.GetComponent<PassiveReflector>().Reflected += (_, _, _) => reflected = true;
            terrace.BallCompressed += (_, _) => secondBounce = true;
            var ball = level.Ball;
            level.ReleaseBall();
            yield return WaitForPiston();
            var camera = level.gameplayCamera;
            Vector3 from = terrace.transform.position;
            Vector3 to = from + Vector3.forward * terrace.maximumDisplacement;
            Assert.That(terrace.BeginDrag(camera.ScreenPointToRay(camera.WorldToScreenPoint(from)),
                camera.transform.forward), Is.True);
            terrace.Drag(camera.ScreenPointToRay(camera.WorldToScreenPoint(to)));
            terrace.EndDrag();

            float started = Time.time;
            while (ball.Released && Time.time - started < 9f)
                yield return null;
            Assert.That(level.Retrying && !level.Completed, Is.True);
            Assert.That(reflected && !secondBounce, Is.True);
            Assert.That(ball.Body.position.y, Is.LessThan(level.fallHeight + 0.43f));
            Assert.That(ball.GetComponent<Renderer>().forceRenderingOff, Is.False);
            int traceLength = level.retryEffect.lastAttempt.positionCount;
            Assert.That(traceLength, Is.GreaterThan(10));
            Vector3[] sampled = new Vector3[traceLength];
            level.retryEffect.lastAttempt.GetPositions(sampled);
            yield return new WaitForSeconds(0.44f);
            Assert.That(ball.Body.position.x, Is.GreaterThan(level.spawn.StartPosition.x + 1f));
            Assert.That(ball.Body.position.y, Is.GreaterThan(level.fallHeight + 0.8f));
            yield return new WaitForSeconds(0.69f);
            Assert.That(level.Retrying || wall.Locked || level.wallLock.activeSelf, Is.False);
            Assert.That(ball.Body.position, Is.EqualTo(level.spawn.StartPosition));
            Assert.That(terrace.CurrentOffset, Is.EqualTo(terrace.maximumDisplacement).Within(0.01f));
            Assert.That(wall.CurrentOffset, Is.EqualTo(0f).Within(0.01f));
            Assert.That(level.retryEffect.lastAttempt.positionCount, Is.EqualTo(traceLength));
            level.ReleaseBall();
            yield return WaitForPiston();
            Assert.That(level.retryEffect.lastAttempt.positionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AStoppedBallGetsOneAndAHalfSecondsBeforeRetry()
        {
            yield return Open();
            level.ReleaseBall();
            yield return WaitForPiston();
            var body = level.Ball.Body;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = new Vector3(0f, 2f, -0.8f);
            yield return new WaitForSeconds(1.2f);
            Assert.That(level.Ball.Released && !level.Retrying, Is.True);
            yield return new WaitForSeconds(0.46f);
            Assert.That(level.Retrying, Is.True);
        }

        private IEnumerator WaitForPiston()
        {
            float limit = Time.time + 1f;
            while (!level.Ball.Released && Time.time < limit)
                yield return null;
            Assert.That(level.Ball.Released, Is.True);
        }

        private IEnumerator Open()
        {
            fixture = EditorSceneManager.LoadSceneInPlayMode(Path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(item => item);
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            if (fixture.IsValid() && fixture.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
#endif
