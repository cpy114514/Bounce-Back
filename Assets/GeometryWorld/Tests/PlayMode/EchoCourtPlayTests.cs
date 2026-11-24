#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class EchoCourtPlayTests
    {
        private const string ScenePath = "Assets/GeometryWorldDemo/Echo Court.unity";
        private Scene fixture;

        [UnityTest]
        public IEnumerator BackpackPlacesSlimeWithItsGameplayAndCanBreakIt()
        {
            fixture = SceneManager.CreateScene("Backpack test");
            var root = new GameObject("Build Level");
            SceneManager.MoveGameObjectToScene(root, fixture);
            var level = root.AddComponent<GeometryLevel>();
            level.showPlayHud = false;
            var builder = new RuntimeBlockBuilder(level);
            var item = RuntimeBuildCatalog.Load().Find("Elastic Object");
            var placement = new BuildItemGeometry(item.prefab, Vector3.one);
            Pose pose = placement.RestOnFace(new Pose(Vector3.up * 0.5f, Quaternion.identity), Vector3.up, 1);
            var piece = builder.PlaceItem(item, pose);
            var elastic = piece.GetComponent<ElasticGeometry>();
            Assert.That(elastic.bounceBall, Is.True);
            Assert.That(Quaternion.Angle(piece.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
            var collider = piece.GetComponent<Collider>();
            Assert.That(collider.bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(builder.Snapshot().Single().itemId, Is.EqualTo("Elastic Object"));
            builder.Remove(collider);
            yield return null;
            Assert.That(builder.Snapshot().Single().IsRemoved, Is.True);
            Assert.That(piece.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeBlocksSnapFaceToFaceAndOnlyBuildBlocksCanBeRemoved()
        {
            fixture = SceneManager.CreateScene("Runtime blocks");
            var root = new GameObject("Build Level");
            SceneManager.MoveGameObjectToScene(root, fixture);
            var level = root.AddComponent<GeometryLevel>();
            level.showPlayHud = false;
            var builder = new RuntimeBlockBuilder(level);
            var authored = GameObject.CreatePrimitive(PrimitiveType.Cube);
            authored.transform.SetParent(root.transform);
            Quaternion rotation = Quaternion.Euler(0f, 33f, 0f);
            Vector3 center = new(0f, 48f, 36f);
            authored.transform.SetPositionAndRotation(center, rotation);
            Vector3 normal = rotation * Vector3.right;
            var collision = authored.GetComponent<Collider>();
            Physics.SyncTransforms();

            Assert.That(RuntimeBlockBuilder.TryFacePose(center, rotation, center + normal * 0.5f, normal,
                out Pose first), Is.True);
            Assert.That(Vector3.Distance(first.position, center + normal), Is.LessThan(0.0001f));
            Assert.That(builder.HasRoom(first), Is.True);
            builder.Add(first);
            Assert.That(builder.HasRoom(first), Is.False);
            builder.Remove(collision);
            Assert.That(collision.enabled, Is.True);
            var sculpted = root.GetComponentInChildren<SculptedBlock>();
            Vector3 corner = new(0.375f, 0.5f, 0.375f);
            Vector3 hit = sculpted.transform.TransformPoint(corner);
            Assert.That(sculpted.TryCell(hit, Vector3.up, false, out Vector3Int cut), Is.True);
            builder.ChangeCell(sculpted, cut, false);
            Assert.That(sculpted.Contains(cut), Is.False);
            Vector3 lowerFace = sculpted.transform.TransformPoint(corner - Vector3.up * SculptedBlock.CellSize);
            Assert.That(sculpted.TryCell(lowerFace, Vector3.up, true, out Vector3Int fill), Is.True);
            Assert.That(fill, Is.EqualTo(cut));
            builder.ChangeCell(sculpted, fill, true);
            Assert.That(sculpted.Contains(cut), Is.True);

            Assert.That(RuntimeBlockBuilder.TryFacePose(first.position, first.rotation,
                first.position + Vector3.up * 0.5f, Vector3.up, out Pose top), Is.True);
            Assert.That(Vector3.Distance(top.position, first.position + Vector3.up), Is.LessThan(0.0001f));
            builder.Add(top);
            var added = root.transform.GetChild(root.transform.childCount - 1).GetComponent<Collider>();
            builder.Remove(added);
            yield return null;
            var layout = builder.Snapshot();
            Assert.That(layout.Count(block => !block.IsRemoved), Is.EqualTo(1));
            Assert.That(layout.Single(block => !block.IsRemoved).position, Is.EqualTo(first.position));
        }

        [UnityTest]
        public IEnumerator SlimeSquashesAndBouncesTheBallOnTopImpact() => CheckSlimeBounce(Vector3.down, 1);

        [UnityTest]
        public IEnumerator SlimeSquashesAndBouncesTheBallOnSideImpact() => CheckSlimeBounce(Vector3.forward, 2);

        private IEnumerator CheckSlimeBounce(Vector3 incoming, int impactAxis)
        {
            fixture = SceneManager.CreateScene("Slime impact test");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GeometryWorldLibrary/Prefabs/Starter/Metric/Gadgets/Elastic Object.prefab");
            var slimePosition = new Vector3(0f, 48f, 36f);
            var slime = Object.Instantiate(prefab, slimePosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(slime, fixture);
            var gel = slime.GetComponent<ElasticGeometry>();
            Vector3 restScale = gel.visual.localScale;
            Vector3 farSide = incoming * 0.5f;
            var root = new GameObject("Slime Test Level");
            SceneManager.MoveGameObjectToScene(root, fixture);
            var level = root.AddComponent<GeometryLevel>();
            level.showPlayHud = false;
            var start = new GameObject("Ball Spawn");
            start.transform.SetParent(root.transform);
            var up = impactAxis == 1 ? Vector3.forward : Vector3.up;
            start.transform.SetPositionAndRotation(slimePosition - incoming * 1.6f, Quaternion.LookRotation(incoming, up));
            level.spawn = start.AddComponent<BallSpawn>();
            level.spawn.launchSpeed = 7f;
            yield return null;
            Assert.That(Vector3.Distance(slime.transform.position, slimePosition), Is.LessThan(0.0001f));
            Vector3 anchor = gel.visual.TransformPoint(farSide);
            level.ReleaseBall();

            float fastestRebound = 0f;
            bool squashed = false;
            float deadline = Time.time + 0.55f;
            while (Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                fastestRebound = Mathf.Max(fastestRebound, Vector3.Dot(level.Ball.Body.linearVelocity, -incoming));
                if (gel.visual.localScale[impactAxis] < restScale[impactAxis] - 0.06f)
                {
                    squashed = true;
                    Assert.That(Vector3.Distance(gel.visual.TransformPoint(farSide), anchor), Is.LessThan(0.0001f));
                }
            }
            Assert.That(fastestRebound, Is.GreaterThan(4f));
            Assert.That(squashed, Is.True);

            yield return new WaitForSeconds(0.40f);
            Assert.That(Vector3.Distance(gel.visual.localScale, restScale), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(gel.visual.TransformPoint(farSide), anchor), Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator PistonStopsItsForwardStrokeWhenTheBallHitsASolidWall()
        {
            fixture = EditorSceneManager.LoadSceneInPlayMode("Assets/GeometryWorldDemo/Trigger Run.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(item => item);
            var piston = level.StarterPiston;
            Vector3 origin = level.Ball.Body.position;
            Vector3 forward = piston.transform.forward;
            const float clearance = 0.15f;
            const float thickness = 0.04f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, fixture);
            wall.transform.SetPositionAndRotation(origin + forward * (level.spawn.diameter * 0.5f + clearance + thickness * 0.5f),
                piston.transform.rotation);
            wall.transform.localScale = new Vector3(1f, 1f, thickness);
            Physics.SyncTransforms();

            level.ReleaseBall();
            float deadline = Time.time + 1f;
            while (!level.Ball.Released && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                float travel = Vector3.Dot(level.Ball.Body.position - origin, forward);
                Assert.That(travel, Is.LessThanOrEqualTo(clearance + 0.005f));
            }
            Assert.That(level.Ball.Released, Is.True);
            Assert.That(level.Ball.Body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.ContinuousDynamic));
            for (int step = 0; step < 6; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Dot(level.Ball.Body.position - origin, forward), Is.LessThanOrEqualTo(clearance + 0.01f));
            }
        }

        [UnityTest]
        public IEnumerator StarterPistonPushesOneBallAndResetCancelsTheStroke()
        {
            const string path = "Assets/GeometryWorldDemo/Trigger Run.unity";
            fixture = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(item => item);
            var piston = level.StarterPiston;
            var ball = level.Ball;
            void AssertBallTouchesPlate()
            {
                Vector3 plateFace = piston.head.TransformPoint(Vector3.forward * PistonLauncher.HeadDepth * 0.5f);
                Vector3 ballBack = ball.Body.position - piston.transform.forward * (level.spawn.diameter * 0.5f);
                Assert.That(Vector3.Distance(ballBack, plateFace), Is.LessThan(0.0001f));
            }
            Assert.That(piston.rod, Is.Not.Null);
            AssertBallTouchesPlate();
            piston.stroke = 1.1f;
            level.ResetBall(false);
            AssertBallTouchesPlate();
            Vector3 RodSeat() => piston.housing.InverseTransformPoint(piston.rod.TransformPoint(Vector3.back));
            Vector3 seat = RodSeat();
            Assert.That(seat.z, Is.InRange(-0.20f, 0.17f));
            Vector3 rest = piston.head.localPosition;
            Assert.That(ball.Body.isKinematic, Is.True);
            Assert.That(level.GetComponentsInChildren<BallBody>().Length, Is.EqualTo(1));

            level.ReleaseBall();
            Assert.That(piston.IsPushing, Is.True);
            Assert.That(ball.Released, Is.False);
            level.ResetBall(false);
            Assert.That(piston.IsPushing, Is.False);
            Assert.That(piston.head.localPosition, Is.EqualTo(rest));
            AssertBallTouchesPlate();
            yield return new WaitForSeconds(piston.pushTime + 0.1f);
            Assert.That(ball.Released, Is.False);

            level.ReleaseBall();
            level.ReleaseBall();
            float deadline = Time.time + 1.5f;
            bool movedHead = false;
            bool carriedBall = false;
            while (!ball.Released && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                movedHead |= piston.head.localPosition.z > rest.z;
                Assert.That(Vector3.Distance(RodSeat(), seat), Is.LessThan(0.0001f));
                if (!ball.Released)
                {
                    AssertBallTouchesPlate();
                    carriedBall |= Vector3.Distance(ball.Body.position, level.spawn.StartPosition) > 0.001f;
                }
            }
            Assert.That(movedHead, Is.True);
            Assert.That(carriedBall, Is.True);
            Assert.That(ball.Released, Is.True);
            Assert.That(Vector3.Dot(ball.Body.linearVelocity, level.spawn.transform.forward), Is.GreaterThan(1f));
            while (piston.IsPushing && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Distance(RodSeat(), seat), Is.LessThan(0.0001f));
            }
            Assert.That(piston.IsPushing, Is.False);
            Assert.That(Vector3.Distance(piston.head.localPosition, rest), Is.LessThan(0.0001f));
            Assert.That(level.Ball, Is.SameAs(ball));
        }

        [UnityTest]
        public IEnumerator TriggerRunRotatesTheGateBeforeTheBounceIntoTheGoal()
        {
            const string path = "Assets/GeometryWorldDemo/Trigger Run.unity";
            fixture = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            var level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(item => item);
            var gate = level.GetComponentInChildren<PlatformMotion>();
            var pad = level.GetComponentInChildren<BouncePad>();
            var trigger = level.GetComponentsInChildren<PuzzleTrigger>().Single(zone => zone.role == TriggerRole.EventZone);
            var action = trigger.onEnter.actions.Single();
            Assert.That(action.target, Is.EqualTo(gate.gameObject));
            Assert.That(action.type, Is.EqualTo(PuzzleActionType.Rotate));
            Assert.That(gate.automatic, Is.False);
            Assert.That(level.Ball.Released, Is.False);

            Quaternion closed = gate.transform.localRotation;
            Quaternion open = closed * Quaternion.Euler(action.amount);
            level.ReleaseBall();
            var body = level.Ball.Body;
            bool gateOpened = false;
            bool bounced = false;
            float deadline = Time.time + 6f;
            while (!level.Completed && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                gateOpened |= Quaternion.Angle(gate.transform.localRotation, open) < 1f;
                if (body.position.x > pad.transform.position.x - 1f && body.linearVelocity.y > 2f)
                {
                    Assert.That(gateOpened, Is.True, "The gate must be open before the ball bounces.");
                    bounced = true;
                }
            }
            Assert.That(gateOpened, Is.True, $"The ball did not open the gate. Last position {body.position}.");
            Assert.That(bounced, Is.True, $"The ball did not bounce. Last position {body.position}.");
            Assert.That(level.Completed, Is.True, $"The ball did not reach the goal. Last position {body.position}.");
        }

        [UnityTest]
        public IEnumerator SwitchOpensCrossingAndTheBallBouncesIntoTheGoal()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                Assert.Ignore("Open Demo in Geometry World Editor to generate the Echo Court scene.");
            fixture = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var level = fixture.GetRootGameObjects().Select(root => root.GetComponent<GeometryLevel>()).First(item => item);
            var connection = level.GetComponentInChildren<SpaceConnection>();
            var elasticity = level.GetComponentInChildren<ElasticGeometry>();
            var puzzleSwitch = level.GetComponentInChildren<PuzzleSwitch>();
            Assert.That(level.Ball, Is.Not.Null);
            Assert.That(level.Ball.Body.isKinematic, Is.True);
            Assert.That(connection.IsReady(), Is.False);
            Assert.That(level.audioDirector.ambient, Is.Not.Null);

            Vector3 rootPosition = elasticity.transform.position;
            Vector3 visualPosition = elasticity.visual.localPosition;
            elasticity.Pulse();
            for (int step = 0; step < 8; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(visualPosition, elasticity.visual.localPosition), Is.GreaterThan(0.02f));
            Assert.That(Vector3.Distance(elasticity.transform.position, rootPosition), Is.LessThan(0.0001f));

            puzzleSwitch.Interact();
            float deadline = Time.time + 2f;
            while (!connection.IsReady() && Time.time < deadline)
                yield return null;
            Assert.That(connection.IsReady(), Is.True);
            level.ReleaseBall();
            var ball = level.Ball;
            bool crossed = false;
            float peakBounce = 0f;
            Vector3 entryVelocity = Vector3.zero, exitVelocity = Vector3.zero;
            deadline = Time.time + 7f;
            while (!level.Completed && Time.time < deadline)
            {
                yield return new WaitForFixedUpdate();
                if (ball.InTransition && !crossed)
                    crossed = true;
                if (!crossed)
                    entryVelocity = ball.Body.linearVelocity;
                else if (!ball.InTransition && exitVelocity == Vector3.zero)
                    exitVelocity = ball.Body.linearVelocity;
                if (ball.transform.position.x > 3.3f && !ball.InTransition)
                    peakBounce = Mathf.Max(peakBounce, ball.Body.linearVelocity.y);
            }
            Assert.That(crossed, Is.True, $"The ball did not traverse. Last position {ball.Body.position}.");
            Assert.That(exitVelocity.x, Is.EqualTo(entryVelocity.x).Within(0.35f));
            Assert.That(peakBounce, Is.GreaterThan(1.5f), $"The ball did not bounce. Last position {ball.Body.position}.");
            Assert.That(level.Completed, Is.True, $"The ball did not reach the goal. Last position {ball.Body.position}.");
            Assert.That(ball.Body.isKinematic, Is.True);
            level.ResetBall(false);
            Assert.That(level.Completed, Is.False);
            Assert.That(level.Ball.Body.isKinematic, Is.True);
            Assert.That(Vector3.Distance(level.Ball.Body.position, level.spawn.StartPosition), Is.LessThan(0.0001f));
        }

        [UnityTearDown]
        public IEnumerator UnloadFixture()
        {
            if (fixture.IsValid() && fixture.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixture);
        }
    }
}
#endif
