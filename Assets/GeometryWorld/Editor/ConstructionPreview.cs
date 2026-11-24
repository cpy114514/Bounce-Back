using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BounceBack.GeometryWorld.Editor
{
    internal readonly struct ConstructionPreview
    {
        private static Material material;
        private static Mesh boxMesh;
        private static Mesh ballMesh;

        internal readonly Mesh Mesh;
        internal readonly Matrix4x4 LocalMatrix;
        internal readonly Bounds Bounds;
        private readonly MeshTopology topology;
        private readonly Color color;
        private readonly bool sphere;
        private readonly float launchSpeed;

        private ConstructionPreview(Mesh mesh, Matrix4x4 localMatrix, Bounds bounds, Color color,
            MeshTopology topology = null, bool sphere = false, float launchSpeed = 0f)
        {
            Mesh = mesh;
            LocalMatrix = localMatrix;
            Bounds = bounds;
            this.color = color;
            this.topology = topology;
            this.sphere = sphere;
            this.launchSpeed = launchSpeed;
        }

        internal static ConstructionPreview From(GameObject prefab)
        {
            if (!prefab)
                return default;
            if (prefab.TryGetComponent(out GeometryObject geometry) && geometry.surface && geometry.surface.sharedMesh)
            {
                Mesh mesh = geometry.surface.sharedMesh;
                Matrix4x4 local = prefab.transform.worldToLocalMatrix * geometry.surface.transform.localToWorldMatrix;
                ConstructionDimensions.TryBounds(prefab, out Bounds bounds);
                Material surface = geometry.surfaceRenderer ? geometry.surfaceRenderer.sharedMaterial : null;
                Color tint = surface && surface.HasProperty("_BaseColor") ? surface.GetColor("_BaseColor") : GeometryAssets.Pastels[1].Color;
                return new ConstructionPreview(mesh, local, bounds, tint, geometry.model ? geometry.model.topology : null);
            }
            if (prefab.TryGetComponent(out BallSpawn spawn))
                return Marker(new Bounds(spawn.LocalStartPosition, Vector3.one * spawn.diameter), 1, true, Mathf.Max(0.5f, spawn.launchSpeed * 0.35f));
            if (prefab.TryGetComponent(out SpatialAnchor _) && prefab.TryGetComponent(out SphereCollider anchor))
                return Marker(new Bounds(anchor.center, Vector3.one * (anchor.radius * 2f)), 4, true);
            if (prefab.TryGetComponent(out PuzzleTrigger zone) && prefab.TryGetComponent(out BoxCollider box))
                return Marker(new Bounds(box.center, box.size), SceneGadgetTools.ZonePalette(zone.role));
            return default;
        }

        private static ConstructionPreview Marker(Bounds bounds, int palette, bool sphere = false, float launchSpeed = 0f)
        {
            if (sphere && !ballMesh)
                ballMesh = MakeMarker(GeometryShape.Sphere);
            if (!sphere && !boxMesh)
                boxMesh = MakeMarker(GeometryShape.Cube);
            return new ConstructionPreview(sphere ? ballMesh : boxMesh,
                Matrix4x4.TRS(bounds.center, Quaternion.identity, bounds.size), bounds,
                GeometryAssets.Pastels[palette].Color, sphere: sphere, launchSpeed: launchSpeed);
        }

        private static Mesh MakeMarker(GeometryShape shape)
        {
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            GeometryGenerators.Generate(shape, Vector3.one, 24, 0f).WriteMesh(mesh, 1);
            return mesh;
        }

        internal static void ReleaseResources()
        {
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(boxMesh);
            Object.DestroyImmediate(ballMesh);
        }

        internal void Draw(Matrix4x4 pose)
        {
            if (!Mesh)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            Color fill = color;
            fill.a = sphere || topology != null ? 0.42f : 0.23f;
            DrawSolid(Mesh, pose * LocalMatrix, fill);
            var depth = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            using (new Handles.DrawingScope(color, topology != null ? pose * LocalMatrix : pose))
            {
                if (topology != null)
                {
                    foreach (TopologyEdge edge in topology.GetEdges().Keys)
                        Handles.DrawLine(topology.vertices[edge.A], topology.vertices[edge.B], 1.5f);
                }
                else if (sphere)
                {
                    float radius = Bounds.extents.x;
                    Handles.DrawWireDisc(Bounds.center, Vector3.right, radius);
                    Handles.DrawWireDisc(Bounds.center, Vector3.up, radius);
                    Handles.DrawWireDisc(Bounds.center, Vector3.forward, radius);
                }
                else
                    Handles.DrawWireCube(Bounds.center, Bounds.size);
            }

            if (launchSpeed > 0f)
            {
                Vector3 origin = pose.MultiplyPoint3x4(Bounds.center);
                Vector3 forward = pose.MultiplyVector(Vector3.forward).normalized;
                SceneGadgetTools.DrawArrow(origin, forward, launchSpeed, color);
            }
            Handles.zTest = depth;
        }

        internal static void DrawSolid(Mesh mesh, Matrix4x4 transform, Color color)
        {
            if (!material)
            {
                Shader shader = Shader.Find("Hidden/BounceBack/Construction Preview");
                if (!shader)
                    return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            material.color = color;
            if (material.SetPass(0))
                Graphics.DrawMeshNow(mesh, transform);
        }
    }
}
