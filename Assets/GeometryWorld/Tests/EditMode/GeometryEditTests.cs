using System;
using System.Collections.Generic;
using System.Linq;
using BounceBack.Geometry.Editor;
using BounceBack.GeometryWorld.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Tests
{
    public sealed class GeometryEditTests
    {
        [Test]
        public void RuntimeBackpackWritesPrefabGadgetsAndAssignsTheGoal()
        {
            using var scope = new ConstructionTestScene();
            var level = new GameObject("Backpack Level").AddComponent<GeometryLevel>();
            var catalog = RuntimeBuildCatalog.Load();
            Assert.That(catalog.items.Length, Is.EqualTo(16));
            Assert.That(catalog.items.All(item => item.prefab), Is.True);
            var changes = new[]
            {
                new BuildBlockState { id = "runtime-ramp", itemId = "Ramp", position = Vector3.up, rotation = Quaternion.identity },
                new BuildBlockState { id = "runtime-goal", itemId = "Goal", position = Vector3.right * 2f, rotation = Quaternion.identity }
            };
            Undo.IncrementCurrentGroup();
            Assert.That(RuntimeBuildSave.ApplyBlocks(level, changes, "Test backpack"), Is.EqualTo(2));
            Undo.FlushUndoRecordObjects();
            Assert.That(level.goal.role, Is.EqualTo(TriggerRole.Goal));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(level.goal.gameObject), Is.SameAs(catalog.Find("Goal").prefab));
            Assert.That(level.GetComponentsInChildren<RuntimeBuildPiece>().Length, Is.EqualTo(2));
            Assert.That(level.GetComponentInChildren<GeometryObject>().gameplayCollider.enabled, Is.True);
        }

        [Test]
        public void ApplyingVoxelLayoutPreservesCarvedCellsAndCanBeUndone()
        {
            using var scope = new ConstructionTestScene();
            var level = new GameObject("Voxel Level").AddComponent<GeometryLevel>();
            var block = new BuildBlockState
            {
                id = "scope-test-block",
                position = Vector3.up,
                rotation = Quaternion.identity,
                layers = new[] { 65535, 65535, 65535, 32767 }
            };

            Undo.IncrementCurrentGroup();
            RuntimeBuildSave.ApplyBlocks(level, new[] { block }, "Build test");
            Undo.FlushUndoRecordObjects();
            var instance = level.GetComponentInChildren<SculptedBlock>();
            var collider = instance.GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            Assert.That(collider.Raycast(new Ray(new Vector3(0.375f, 3f, 0.375f), Vector3.down),
                out RaycastHit hit, 4f), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(1.25f).Within(0.0001f));

            Undo.IncrementCurrentGroup();
            block.layers[3] = 65535;
            RuntimeBuildSave.ApplyBlocks(level, new[] { block }, "Fill test");
            Undo.FlushUndoRecordObjects();
            Assert.That(collider.Raycast(new Ray(new Vector3(0.375f, 3f, 0.375f), Vector3.down), out hit, 4f), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(1.5f).Within(0.0001f));
            Undo.PerformUndo();
            Assert.That(collider.Raycast(new Ray(new Vector3(0.375f, 3f, 0.375f), Vector3.down), out hit, 4f), Is.True);
            Assert.That(hit.point.y, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void MetricStarterPrefabsShareTheSameMeshAndColliderBasis()
        {
            foreach (string group in new[] { "Shapes", "Gadgets" })
            {
                var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ConstructionProfile.Metric + group });
                Assert.That(prefabs.Length, Is.EqualTo(group == "Shapes" ? 6 : 10));
                foreach (string guid in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    Vector3 basis = prefab.TryGetComponent(out GeometryObject shape) ? shape.surface.sharedMesh.bounds.size :
                        prefab.TryGetComponent(out BoxCollider box) ? box.size :
                        Vector3.one * prefab.GetComponent<SphereCollider>().radius * 2f;
                    Assert.That(Vector3.Distance(basis, Vector3.one), Is.LessThan(0.0001f), prefab.name);
                    if (shape && shape.gameplayCollider is MeshCollider meshCollider)
                        Assert.That(meshCollider.sharedMesh, Is.SameAs(shape.surface.sharedMesh), prefab.name);
                }
            }

            var slime = new ConstructionProfile("Slime", PuzzleItem.ElasticObject, "").Prefab;
            Assert.That(slime.GetComponent<ElasticGeometry>(), Is.Not.Null);
            var slimeGeometry = slime.GetComponent<GeometryObject>();
            Assert.That(slime.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(slimeGeometry.model.topology.vertices.Count, Is.EqualTo(8));
            Assert.That(slimeGeometry.model.topology.faces.Count, Is.EqualTo(6));
            Assert.That(slimeGeometry.surfaceRenderer.sharedMaterials.Length, Is.EqualTo(1));
            Color gel = slimeGeometry.surfaceRenderer.sharedMaterial.color;
            Assert.That(gel.g, Is.GreaterThan(gel.r));
            Assert.That(gel.g, Is.GreaterThan(gel.b));
            Assert.That(gel.a, Is.InRange(0.01f, 0.99f));
        }

        [Test]
        public void NormalizingLegacyPrefabKeepsItsWorldVerticesColliderAndPivot()
        {
            using var scope = new ConstructionTestScene();
            string path = GeometryAssets.Prefabs + "/Starter/Shapes/Platform.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var geometry = obj.GetComponent<GeometryObject>();
            string output = "Assets/GeometryWorldTest-" + Guid.NewGuid().ToString("N");
            try
            {
                obj.transform.SetPositionAndRotation(new Vector3(-4f, 3f, 1), Quaternion.Euler(15f, 30f, 2f));
                obj.transform.localScale = new Vector3(2f, 0.7f, 1.1f);
                var original = geometry.model;
                Vector3 pivot = obj.transform.position;
                Vector3[] vertices = original.topology.vertices.Select(obj.transform.TransformPoint).ToArray();

                Undo.IncrementCurrentGroup();
                GeometryUnitScale.Normalize(geometry, folder: output);
                Undo.FlushUndoRecordObjects();

                Assert.That(geometry.model.mesh.bounds.size, Is.EqualTo(Vector3.one));
                Assert.That(obj.transform.position, Is.EqualTo(pivot));
                Assert.That(((MeshCollider)geometry.gameplayCollider).sharedMesh, Is.SameAs(geometry.surface.sharedMesh));
                for (int i = 0; i < vertices.Length; i++)
                    Assert.That(Vector3.Distance(obj.transform.TransformPoint(geometry.model.topology.vertices[i]), vertices[i]),
                        Is.LessThan(0.0001f));
                Undo.PerformUndo();
                Assert.That(geometry.model, Is.SameAs(original));
                Assert.That(geometry.surface.sharedMesh, Is.SameAs(original.mesh));
            }
            finally
            {
                Object.DestroyImmediate(obj);
                AssetDatabase.DeleteAsset(output);
            }
        }

        [Test]
        public void LevelCreatesOneBallAndPistonAndMovingTheStartReusesIt()
        {
            using var scope = new ConstructionTestScene();
            var scene = SceneManager.GetActiveScene();
            var level = LevelAuthoring.CreateInScene(scene, "Starter Test", false);
            var spawn = level.spawn;
            Assert.That(spawn, Is.Not.Null);
            Assert.That(level.StarterPiston.HasVisuals, Is.True);
            Assert.That(level.GetComponentsInChildren<BallSpawn>().Length, Is.EqualTo(1));

            Vector3 newPosition = new(2f, 1f, 4f);
            var moved = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Spawn, newPosition, false);
            Assert.That(moved, Is.SameAs(spawn.gameObject));
            Assert.That(spawn.transform.position, Is.EqualTo(newPosition));
            Assert.That(LevelStarterAuthoring.CountStarts(level), Is.EqualTo(1));
        }

        [Test]
        public void PistonAlignsToRotatedBlockCenterlineAndStaysOverItsTop()
        {
            using var scope = new ConstructionTestScene();
            var level = LevelAuthoring.CreateInScene(SceneManager.GetActiveScene(), "Piston Alignment", false);
            var piston = level.StarterPiston;
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            var geometry = block.gameObject.AddComponent<GeometryObject>();
            geometry.surface = block.GetComponent<MeshFilter>();
            geometry.gameplayCollider = block.GetComponent<BoxCollider>();
            geometry.editLocked = true;
            block.SetPositionAndRotation(new Vector3(3f, 1f, -3f), Quaternion.Euler(0f, 35f, 0f));
            block.localScale = new Vector3(0.8f, 1.4f, 1.2f);
            piston.transform.rotation = Quaternion.Euler(0f, 118f, 0f);
            Vector3 startBefore = piston.transform.position;
            Quaternion headingBefore = piston.transform.rotation;
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            Vector3 hit = block.TransformPoint(new Vector3(0.45f, 0.5f, -0.28f));
            Physics.SyncTransforms();

            Undo.IncrementCurrentGroup();
            Assert.That(PistonAlignment.TryAlign(piston, geometry, new Ray(hit + block.up * 2f, -block.up)), Is.True);

            Assert.That(Vector3.Dot(piston.transform.forward, block.right), Is.GreaterThan(0.999f));
            Vector3 start = block.InverseTransformPoint(level.spawn.StartPosition);
            Vector3 end = block.InverseTransformPoint(piston.transform.position);
            Assert.That(start.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(end.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(start.x, Is.InRange(bounds.min.x, bounds.max.x));
            Assert.That(end.x, Is.InRange(bounds.min.x, bounds.max.x));
            float height = Vector3.Dot(level.spawn.StartPosition - hit, block.up);
            Assert.That(height, Is.EqualTo(level.spawn.diameter * 0.5f).Within(0.002f));
            Assert.That(PistonAlignment.TryPose(piston, block, bounds, hit, block.right, out _), Is.False);

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(piston.transform.position, Is.EqualTo(startBefore));
            Assert.That(Quaternion.Angle(piston.transform.rotation, headingBefore), Is.LessThan(0.01f));
        }

        [Test]
        public void WorkplaneSnapsOnItsOwnAxesAfterPickingAWall()
        {
            var plane = new ConstructionPlane(new Vector3(2f, 3f, 4f), Vector3.right);
            Vector3 point = plane.Origin + plane.Rotation * new Vector3(0.72f, 0f, 1.29f);
            Assert.That(plane.Intersect(new Ray(point + Vector3.right * 4f, Vector3.left), out var hit), Is.True);
            Vector3 snapped = plane.Snap(hit, new Vector3(0.5f, 2f, 1f));
            Vector3 expected = plane.Origin + plane.Rotation * new Vector3(0.5f, 0f, 1f);
            Assert.That(Vector3.Distance(snapped, expected), Is.LessThan(0.0001f));
            Vector3 position = SceneConstruction.RestingPosition(new Bounds(Vector3.zero, Vector3.one),
                Matrix4x4.Rotate(plane.Rotation), snapped, plane.Normal, Vector3.zero);
            Assert.That(position.x, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void DimensionsResizeTheSelectedInstanceAndHoldItsBaseOnTheWorkplane()
        {
            using var scene = new ConstructionTestScene();
            var parent = new GameObject("Scaled Group");
            var subject = new GameObject("Trigger");
            try
            {
                parent.transform.localScale = new Vector3(2f, 3f, 1f);
                subject.transform.SetParent(parent.transform, false);
                subject.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
                subject.transform.localScale = new Vector3(1f, -1f, 1f);
                var box = subject.AddComponent<PuzzleTrigger>().GetComponent<BoxCollider>();
                box.center = new Vector3(0.4f, 0.25f, -0.2f);
                box.size = new Vector3(3f, 1f, 2f);
                var localBase = box.center + Vector3.up * box.size.y * 0.5f;
                Vector3 anchor = subject.transform.TransformPoint(localBase);
                var size = new Vector3(4f, 2f, 7f);

                Assert.That(ConstructionDimensions.Resize(subject, size), Is.True);
                Assert.That(ConstructionDimensions.TryMeasure(subject, out var dimensions), Is.True);
                Assert.That(Vector3.Distance(dimensions, size), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(subject.transform.TransformPoint(localBase), anchor), Is.LessThan(0.0001f));
                Assert.That(box.size, Is.EqualTo(new Vector3(3f, 1f, 2f)));
                Assert.That(subject.transform.localScale.y, Is.LessThan(0f));
            }
            finally
            {
                Undo.ClearUndo(subject.transform);
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SwitchLinkCreatesAnUndoablePlatformActionWithoutDuplicates()
        {
            using var scene = new ConstructionTestScene();
            var source = new GameObject("Switch");
            var target = new GameObject("Moving Platform");
            try
            {
                var triggerSwitch = source.AddComponent<PuzzleSwitch>();
                var motion = target.AddComponent<PlatformMotion>();
                motion.localTravel = new Vector3(2f, 0f, 0f);
                motion.duration = 3f;
                Assert.That(SceneGadgetTools.TryLink(source, target, out var error), Is.True, error);
                var sequence = triggerSwitch.onInteract;
                Assert.That(sequence.actions.Count, Is.EqualTo(1));
                Assert.That(sequence.oneShot, Is.True);
                Assert.That(sequence.actions[0].type, Is.EqualTo(PuzzleActionType.Move));
                Assert.That(sequence.actions[0].target, Is.EqualTo(target));
                Assert.That(sequence.actions[0].amount, Is.EqualTo(motion.localTravel));
                Assert.That(sequence.actions[0].duration, Is.EqualTo(3f));
                Assert.That(motion.automatic, Is.False);

                Undo.PerformUndo();
                Assert.That(triggerSwitch.onInteract == null, Is.True);
                Assert.That(motion.automatic, Is.True);
                Undo.PerformRedo();
                Assert.That(triggerSwitch.onInteract.actions[0].target, Is.EqualTo(target));
                Assert.That(motion.automatic, Is.False);
                Assert.That(SceneGadgetTools.TryLink(source, target, out error), Is.True, error);
                Assert.That(triggerSwitch.onInteract.actions.Count, Is.EqualTo(1));
            }
            finally
            {
                Undo.ClearUndo(source);
                Undo.ClearUndo(target);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DroppingAGoalCreatesAPlayableZoneAndAssignsTheFirstGoal()
        {
            using var scene = new ConstructionTestScene();
            var root = new GameObject("Level");
            try
            {
                var level = root.AddComponent<GeometryLevel>();
                var profile = new ConstructionProfile("Goal", PuzzleItem.Goal, "Complete the level.");
                var placed = profile.Place(level, root.transform, Vector3.up, Quaternion.identity);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(placed), Is.EqualTo(profile.Prefab));
                Assert.That(level.goal, Is.EqualTo(placed.GetComponent<PuzzleTrigger>()));
                Assert.That(level.goal.role, Is.EqualTo(TriggerRole.Goal));
                Assert.That(placed.GetComponent<BoxCollider>().isTrigger, Is.True);
                Assert.That(placed.GetComponent<BoxCollider>().size, Is.EqualTo(profile.Bounds.size));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(level.goal == null, Is.True);
                Assert.That(placed == null, Is.True);
                Undo.PerformRedo();
                Assert.That(level.goal, Is.EqualTo(root.GetComponentInChildren<PuzzleTrigger>()));
            }
            finally
            {
                Undo.ClearUndo(root);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrayMeshAndPoseMatchThePlacedPrefabAndSurviveReopening()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Geometry Tray Test.unity");
            try
            {
                using var fixture = new ConstructionTestScene();
                Scene scene = SceneManager.GetActiveScene();
                var level = LevelAuthoring.CreateInScene(scene, "Tray Test", false);
                var parent = LevelAuthoring.NewObject("Rotated Group", level.transform, scene, Vector3.zero, false).transform;
                parent.localScale = new Vector3(2f, 1.5f, 1f);
                parent.localRotation = Quaternion.Euler(0f, 30f, 0f);
                var profile = new ConstructionProfile("Ramp");
                var preview = ConstructionPreview.From(profile.Prefab);
                var plane = new ConstructionPlane(new Vector3(3f, 2f, 1f), Vector3.right);
                var rotation = Quaternion.Inverse(parent.rotation) * plane.Rotation * profile.Prefab.transform.localRotation;
                var scale = profile.Prefab.transform.localScale;
                var orientation = SceneConstruction.LeafMatrix(parent, Vector3.zero) * Matrix4x4.TRS(Vector3.zero, rotation, scale);
                Vector3 point = plane.Snap(plane.Origin + Vector3.up * 0.38f, Vector3.one * 0.5f);
                Vector3 drop = SceneConstruction.RestingPosition(preview.Bounds, orientation, point, plane.Normal, Vector3.zero);
                var item = profile.Place(level, parent, drop, rotation);
                var surface = item.GetComponent<GeometryObject>().surface;
                Assert.That(surface.sharedMesh, Is.SameAs(preview.Mesh));

                Matrix4x4 expected = SceneConstruction.LeafMatrix(parent, drop) * Matrix4x4.TRS(Vector3.zero, rotation, scale) * preview.LocalMatrix;
                for (int i = 0; i < 4; i++)
                    Assert.That(Vector4.Distance(expected.GetColumn(i), surface.transform.localToWorldMatrix.GetColumn(i)), Is.LessThan(0.0001f));

                var size = new Vector3(2.5f, 1.75f, 3f);
                Assert.That(ConstructionDimensions.Resize(item, size), Is.True);
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                scene = fixture.Reopen(path);
                var reopened = LevelAuthoring.FindLevel(scene).GetComponentInChildren<GeometryObject>().gameObject;
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(reopened), Is.EqualTo(profile.Prefab));
                Assert.That(Quaternion.Angle(reopened.transform.localRotation, rotation), Is.LessThan(0.01f));
                Assert.That(ConstructionDimensions.TryMeasure(reopened, out Vector3 savedSize), Is.True);
                Assert.That(Vector3.Distance(savedSize, size), Is.LessThan(0.0001f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private sealed class ConstructionTestScene : IDisposable
        {
            private readonly Scene previous = SceneManager.GetActiveScene();
            private Scene fixture;
            private readonly bool untitled;
            private readonly NewSceneSetup restoreSetup;

            internal ConstructionTestScene()
            {
                untitled = string.IsNullOrEmpty(previous.path);
                if (untitled && previous.isDirty)
                    Assert.Ignore("Save the current untitled scene before running the construction tests.");
                restoreSetup = previous.rootCount == 0 ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
                fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(fixture);
            }

            internal Scene Reopen(string path)
            {
                if (!untitled)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(fixture, true);
                }
                fixture = EditorSceneManager.OpenScene(path, untitled ? OpenSceneMode.Single : OpenSceneMode.Additive);
                SceneManager.SetActiveScene(fixture);
                return fixture;
            }

            public void Dispose()
            {
                if (untitled)
                    EditorSceneManager.NewScene(restoreSetup, NewSceneMode.Single);
                else
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(fixture, true);
                }
            }
        }

        private static readonly Vector2[] Concave =
        {
            new(0, 0), new(4, 0), new(4, 1), new(1, 1), new(1, 3), new(0, 3)
        };

        [Test]
        public void ConcaveOutlineTriangulatesAndCrossedOutlineIsRejected()
        {
            Assert.That(PlanarPolygon.TryTriangulate(Concave, out var indices, out _), Is.True);
            float area = 0;
            for (int i = 0; i < indices.Count; i += 3)
            {
                Vector2 first = Concave[indices[i + 1]] - Concave[indices[i]];
                Vector2 second = Concave[indices[i + 2]] - Concave[indices[i]];
                area += (first.x * second.y - first.y * second.x) / 2f;
            }
            Assert.That(area, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(PlanarPolygon.TryTriangulate(new Vector2[] { new(0, 0), new(2, 2), new(2, 0), new(0, 2) },
                out _, out string issue), Is.False);
            Assert.That(issue, Does.Contain("crosses"));
        }

        [Test]
        public void ProceduralShapesGenerateValidTriangleNormalsAndUvs([Values] GeometryShapeForTest shape)
        {
            var topology = GeometryGenerators.Generate((GeometryShape)shape, new Vector3(3f, 2f, 1.7f), 15, 130f, Concave);
            var mesh = new Mesh();
            try
            {
                topology.WriteMesh(mesh, 1);
                Assert.That(mesh.vertexCount, Is.GreaterThan(2));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.normals.All(normal => normal.sqrMagnitude > 0.8f), Is.True);
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        public enum GeometryShapeForTest
        {
            Cube, Plane, Sphere, Cylinder, Cone, Prism, Pyramid, CustomPolygon, Archway,
            Column, Bridge, Stairs, Ramp, Platform, Tower, CircularPlatform, CurvedPath, BallTrack
        }

        [Test]
        public void FaceAndEdgeEditsKeepAClosedCubeConnected()
        {
            var cube = GeometryGenerators.Generate(GeometryShape.Cube, Vector3.one * 2f, 4, 0);
            int top = cube.faces.FindIndex(face => cube.Normal(face).y > 0.9f);
            TopologyEdits.InsetFace(cube, top, 0.23f);
            TopologyEdits.ExtrudeFace(cube, top, 0.55f);
            var ring = cube.faces[top].vertices;
            int vertex = TopologyEdits.SplitEdge(cube, new TopologyEdge(ring[0], ring[1]));
            Assert.That(cube.faces.Count(face => face.vertices.Contains(vertex)), Is.EqualTo(2));
            Assert.That(cube.GetEdges().Values.All(faces => faces.Count == 2), Is.True);
            AssertUsable(cube);
        }

        [Test]
        public void BevelKeepsAClosedConvexSolidAndRejectsAnOversizedCut()
        {
            var cube = GeometryGenerators.Generate(GeometryShape.Cube, Vector3.one * 2f, 4, 0);
            var edge = new TopologyEdge(4, 7);
            Assert.Throws<InvalidOperationException>(() => TopologyEdits.Bevel(cube.Copy(), edge, 3f, 0));
            TopologyEdits.Bevel(cube, edge, 0.15f, 1);
            Assert.That(cube.GetEdges().Values.All(adjacent => adjacent.Count == 2), Is.True);
            Assert.That(cube.faces.Count, Is.EqualTo(7));
            Assert.That(cube.faces.Count(face => face.material == 1), Is.EqualTo(1));
            AssertUsable(cube, 2);
        }

        [Test]
        public void BoundaryExtrusionCanBeSplitAndMirrored()
        {
            var plane = GeometryGenerators.Generate(GeometryShape.Plane, Vector3.one * 2f, 4, 0);
            var edge = TopologyEdits.ExtrudeBoundary(plane, new TopologyEdge(0, 1), 0.4f);
            Assert.That(plane.GetEdges()[edge].Count, Is.EqualTo(1));
            TopologyEdits.SplitEdge(plane, edge);
            TopologyEdits.Mirror(plane);
            Assert.That(plane.faces.All(face => plane.Normal(face).y > 0.95f), Is.True);
            AssertUsable(plane);
        }

        [Test]
        public void DrawingConversionPreservesTheSourceAndStrokeColors()
        {
            var source = GeometryDrawingAuthoring.CreateOutline(Concave, Color.magenta, 0.17f);
            try
            {
                var colors = new List<Color>();
                var solid = GeometryGenerators.Convert(source, DrawingConversion.Solid, 0.5f, 1f, true, colors, out var issues);
                Assert.That(issues, Is.Empty);
                Assert.That(colors, Is.EqualTo(new[] { Color.magenta }));
                Assert.That(solid.faces.Count, Is.EqualTo(Concave.Length + 2));
                var surface = GeometryGenerators.Convert(source, DrawingConversion.Surface, 1f, 1f, false, colors, out issues);
                Assert.That(surface.faces.Count, Is.EqualTo(1));
                var beams = GeometryGenerators.Convert(source, DrawingConversion.Beams, 0.2f, 1f, true, colors, out issues);
                Assert.That(beams.faces.Count, Is.EqualTo(Concave.Length * 6));
                Assert.That(source.Layers[0].Edges.All(edge => edge.Width == 0.17f && edge.Color == Color.magenta), Is.True);
                AssertUsable(solid);
                AssertUsable(beams);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void CrossedDrawingCanCreateBeamsButNotASolid()
        {
            var source = GeometryDrawingAuthoring.CreateOutline(new Vector2[] { new(0, 0), new(2, 2), new(2, 0), new(0, 2) },
                Color.cyan, 0.15f);
            try
            {
                var colors = new List<Color>();
                var solid = GeometryGenerators.Convert(source, DrawingConversion.Solid, 0.5f, 1f, true, colors, out var issues);
                Assert.That(solid.faces, Is.Empty);
                Assert.That(issues[0], Does.Contain("crossed"));
                var beams = GeometryGenerators.Convert(source, DrawingConversion.Beams, 0.3f, 1f, true, colors, out issues);
                Assert.That(issues, Is.Empty);
                AssertUsable(beams);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void ModelUndoAndSceneCopyPreserveNativeAssetReferences()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool untitled = string.IsNullOrEmpty(previous.path);
            if (untitled && previous.isDirty)
                Assert.Ignore("Save the current untitled scene before running the scene persistence test.");

            var restoreUntitledAs = previous.rootCount == 0 ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
            string folder = "Assets/GeometryWorldTest-" + Guid.NewGuid().ToString("N");
            GeometryAssets.EnsureFolder(folder);
            Scene scene = default;
            GeometryMeshAsset model = null;
            Scene copied = default;
            try
            {
                if (untitled)
                    Assert.That(EditorSceneManager.SaveScene(previous, folder + "/Runner Scene.unity"), Is.True);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var level = LevelAuthoring.CreateInScene(scene, "Persistence Test", false);
                var material = GeometryAssets.CreateMaterial("Fixture", Color.cyan, 0f, 0.5f, 1f, directory: folder);
                var topology = GeometryGenerators.Generate(GeometryShape.Cube, new Vector3(2f, 2f, 2f), 4, 0);
                model = GeometryAssets.CreateModel("Linked Cube", topology, new[] { material }, folder: folder);
                var instance = GeometryAssets.Instantiate(model, level.transform, new Vector3(1f, 2f, 3f), false, true, false);
                int initialVertices = model.topology.vertices.Count;
                Undo.IncrementCurrentGroup();
                var edited = model.topology.Copy();
                TopologyEdits.ExtrudeFace(edited, 1, 0.5f);
                GeometryAssets.CommitModel(model, edited, model.materials, "Test Extrusion");
                Undo.PerformUndo();
                Assert.That(model.topology.vertices.Count, Is.EqualTo(initialVertices));
                Undo.PerformRedo();
                Assert.That(model.topology.vertices.Count, Is.GreaterThan(initialVertices));
                string meshPath = AssetDatabase.GetAssetPath(model.mesh);
                string prefabPath = folder + "/Cube.prefab";
                GeometryAssets.SavePrefab(instance.gameObject, prefabPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var placed = GeometryAssets.PlacePrefab(prefab, level.transform, scene, new Vector3(4f, 2f, 1f));
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(placed), Is.True);
                Assert.That(placed.GetComponent<GeometryObject>().surface.sharedMesh, Is.SameAs(model.mesh));
                Assert.That(LevelAuthoring.Save(level, folder + "/Level.unity"), Is.True);
                Assert.That(LevelAuthoring.Save(level, folder + "/Copy.unity", true), Is.True);
                copied = EditorSceneManager.OpenScene(folder + "/Copy.unity", OpenSceneMode.Additive);
                var objects = LevelAuthoring.FindLevel(copied).GetComponentsInChildren<GeometryObject>();
                Assert.That(objects.Length, Is.EqualTo(2));
                Assert.That(objects.All(obj => AssetDatabase.GetAssetPath(obj.surface.sharedMesh) == meshPath), Is.True);
                Assert.That(objects.All(obj => obj.gameplayCollider is MeshCollider collider && collider.sharedMesh == model.mesh), Is.True);
            }
            finally
            {
                if (copied.IsValid() && copied.isLoaded)
                    EditorSceneManager.CloseScene(copied, true);
                if (model)
                {
                    Undo.ClearUndo(model);
                    Undo.ClearUndo(model.mesh);
                }
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (untitled)
                    EditorSceneManager.NewScene(restoreUntitledAs, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void AlignmentRequiresExplicitPairedAnchorsInTheCameraBounds()
        {
            var root = new GameObject("Alignment Test");
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 3;
                var connection = root.AddComponent<SpaceConnection>();
                connection.alignmentCamera = camera;
                connection.activation = ConnectionActivation.CameraAligned;
                var anchorA = new GameObject("A");
                var anchorB = new GameObject("B");
                anchorA.transform.SetParent(root.transform);
                anchorB.transform.SetParent(root.transform);
                connection.source = anchorA.AddComponent<SpatialAnchor>();
                connection.destination = anchorB.AddComponent<SpatialAnchor>();
                anchorA.transform.position = new Vector3(0, 0, 2);
                anchorB.transform.position = new Vector3(0, 0, 3);
                Assert.That(connection.IsReady(), Is.True);
                anchorB.transform.position = new Vector3(0, 0, -2);
                Assert.That(connection.IsReady(), Is.False);
                connection.activation = ConnectionActivation.Manual;
                Assert.That(connection.IsReady(), Is.True);
                connection.enabledByLogic = false;
                Assert.That(connection.IsReady(), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ConstructionSnapsAlongTheFloorAndKeepsTheBottomFlush()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f));
            var result = SceneConstruction.RestingPosition(bounds, Matrix4x4.identity,
                new Vector3(1.2f, 0.35f, 2.7f), Vector3.up, Vector3.one);
            Assert.That(result.x, Is.EqualTo(1f));
            Assert.That(result.y, Is.EqualTo(1.35f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(3f));
        }

        [Test]
        public void ConstructionRespectsModelsWithAnOffsetPivotInARotatedGroup()
        {
            var bounds = new Bounds(new Vector3(0.25f, 0.5f, 0f), new Vector3(2f, 1f, 2f));
            var orientation = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), new Vector3(2f, 3f, 1f));
            var result = SceneConstruction.RestingPosition(bounds, orientation,
                new Vector3(2f, 3f, 1f), Vector3.up, Vector3.zero);
            Assert.That(result.y, Is.EqualTo(4.5f).Within(0.0001f));
        }

        [Test]
        public void ConstructionSnapsToTheContactPlaneWithoutMovingIntoASideWall()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var result = SceneConstruction.RestingPosition(bounds, Matrix4x4.identity,
                new Vector3(1.2f, 2.8f, 4.25f), Vector3.back, Vector3.one);
            Assert.That(result, Is.EqualTo(new Vector3(1f, 3f, 3.75f)));
        }

        private static void AssertUsable(MeshTopology topology, int materials = 1)
        {
            var mesh = new Mesh();
            try
            {
                topology.WriteMesh(mesh, materials);
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
