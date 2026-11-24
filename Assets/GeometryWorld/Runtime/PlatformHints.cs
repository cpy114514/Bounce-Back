using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BounceBack.GeometryWorld
{
    internal sealed class PlatformHints
    {
        private const float FirstHop = 0.46f;
        private const float SecondHop = 0.34f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly GUIContent ButtonLabel = new("?", "Show movable platforms");

        private readonly List<Platform> platforms = new();
        private Mesh arrowMesh;
        private float elapsed;

        internal static Rect ButtonRect(float width, float height) => new(width - 54f, height - 54f, 34f, 34f);
        internal static bool DrawButton(Rect rect) => GUI.Button(rect, ButtonLabel, CourtyardHud.Button);

        internal void Play(IEnumerable<Transform> targets)
        {
            Stop();
            var activeTargets = new HashSet<Transform>();
            foreach (Transform target in targets)
                if (target && target.gameObject.activeInHierarchy)
                    activeTargets.Add(target);

            var material = Resources.Load<Material>("BounceBack/Platform Hint Arrow");
            if (activeTargets.Count > 0 && material && !arrowMesh)
                arrowMesh = CreateArrowMesh();

            foreach (Transform target in activeTargets)
            {
                var platform = new Platform();
                foreach (var source in target.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!source.enabled || source.forceRenderingOff || !BelongsTo(source.transform, target, activeTargets) ||
                        !source.TryGetComponent(out MeshFilter filter) || !filter.sharedMesh)
                        continue;
                    platform.Models.Add(new Model(source, filter.sharedMesh));
                }
                if (platform.Models.Count == 0)
                    continue;

                if (material)
                {
                    platform.Arrow = CreateVisual("Platform Hint Arrow", target, arrowMesh, out MeshRenderer renderer);
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                platforms.Add(platform);
                platform.Pose(0f);
            }
        }

        internal void Update(bool available, float deltaTime)
        {
            if (platforms.Count == 0)
                return;
            elapsed += deltaTime;
            if (!available || elapsed >= FirstHop + SecondHop)
            {
                Stop();
                return;
            }
            float lift = elapsed < FirstHop
                ? Mathf.Sin(Mathf.PI * elapsed / FirstHop)
                : 0.65f * Mathf.Sin(Mathf.PI * (elapsed - FirstHop) / SecondHop);
            foreach (Platform platform in platforms)
                platform.Pose(lift);
        }

        internal void Stop()
        {
            foreach (Platform platform in platforms)
            {
                foreach (Model model in platform.Models)
                {
                    if (model.Source)
                        model.Source.forceRenderingOff = false;
                    Remove(model.Display);
                }
                Remove(platform.Arrow);
            }
            platforms.Clear();
            elapsed = 0f;
        }

        internal void Dispose()
        {
            Stop();
            if (arrowMesh)
                Object.Destroy(arrowMesh);
        }

        private static bool BelongsTo(Transform source, Transform target, HashSet<Transform> targets)
        {
            for (Transform current = source; current != target; current = current.parent)
                if (targets.Contains(current))
                    return false;
            return true;
        }

        private static Transform CreateVisual(string name, Transform parent, Mesh mesh, out MeshRenderer renderer)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer))
            {
                layer = parent.gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = obj.GetComponent<MeshRenderer>();
            return obj.transform;
        }

        private static void Remove(Transform visual)
        {
            if (!visual)
                return;
            visual.gameObject.SetActive(false);
            Object.Destroy(visual.gameObject);
        }

        private static Mesh CreateArrowMesh()
        {
            // A square shaft and four-sided point remain recognizable as the camera orbits.
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            Vector3[] shaftTop = Square(0.18f, 1f);
            Vector3[] shaftBottom = Square(0.18f, 0.53f);
            Vector3[] pointTop = Square(0.42f, 0.53f);
            AddFace(shaftTop[0], shaftTop[1], shaftTop[2], shaftTop[3]);
            AddFace(pointTop[3], pointTop[2], pointTop[1], pointTop[0]);
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                AddFace(shaftBottom[i], shaftBottom[next], shaftTop[next], shaftTop[i]);
                AddFace(pointTop[next], pointTop[i], Vector3.zero);
            }
            var mesh = new Mesh { name = "Platform Hint Arrow", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            return mesh;

            void AddFace(params Vector3[] face)
            {
                int first = vertices.Count;
                vertices.AddRange(face);
                for (int i = 1; i < face.Length - 1; i++)
                {
                    indices.Add(first);
                    indices.Add(first + i);
                    indices.Add(first + i + 1);
                }
            }
        }

        private static Vector3[] Square(float halfWidth, float height) => new Vector3[]
        {
            new(-halfWidth, height, -halfWidth), new(-halfWidth, height, halfWidth),
            new(halfWidth, height, halfWidth), new(halfWidth, height, -halfWidth)
        };

        private sealed class Platform
        {
            internal readonly List<Model> Models = new();
            internal Transform Arrow;

            internal void Pose(float lift)
            {
                Bounds bounds = default;
                bool hasBounds = false;
                foreach (Model model in Models)
                {
                    bool visible = model.Source && model.Source.enabled && model.Source.gameObject.activeInHierarchy;
                    if (model.Display && model.Display.gameObject.activeSelf != visible)
                        model.Display.gameObject.SetActive(visible);
                    if (!visible)
                        continue;
                    if (hasBounds)
                        bounds.Encapsulate(model.Source.bounds);
                    else
                        bounds = model.Source.bounds;
                    hasBounds = true;
                }
                if (Arrow)
                    Arrow.gameObject.SetActive(hasBounds);
                if (!hasBounds)
                    return;

                float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                Vector3 offset = Vector3.up * (Mathf.Clamp(size * 0.18f, 0.24f, 0.52f) * lift);
                foreach (Model model in Models)
                    if (model.Display && model.Display.gameObject.activeInHierarchy)
                        model.Display.localPosition = model.Source.transform.InverseTransformVector(offset);
                if (Arrow)
                {
                    Arrow.position = new Vector3(bounds.center.x, bounds.max.y + 0.14f, bounds.center.z) + offset;
                    Arrow.rotation = Quaternion.Euler(0f, 23f, 0f);
                    float arrowSize = Mathf.Clamp(size * 0.22f, 0.44f, 0.78f);
                    Arrow.localScale = Vector3.one;
                    Vector3 scale = Arrow.lossyScale;
                    Arrow.localScale = new Vector3(arrowSize / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                        arrowSize / Mathf.Max(0.001f, Mathf.Abs(scale.y)), arrowSize / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
                }
            }
        }

        private sealed class Model
        {
            internal readonly MeshRenderer Source;
            internal readonly Transform Display;

            internal Model(MeshRenderer source, Mesh mesh)
            {
                Source = source;
                Display = CreateVisual("Platform Hint Model", source.transform, mesh, out MeshRenderer renderer);
                Material[] materials = source.sharedMaterials;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.renderingLayerMask = source.renderingLayerMask;
                renderer.probeAnchor = source.probeAnchor;
                var block = new MaterialPropertyBlock();
                for (int i = 0; i < materials.Length; i++)
                {
                    source.GetPropertyBlock(block, i);
                    if (block.isEmpty)
                        source.GetPropertyBlock(block);
                    Material material = materials[i];
                    if (material)
                    {
                        int tint = material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                        if (material.HasProperty(tint))
                        {
                            Color current = block.HasColor(tint) ? block.GetColor(tint) : material.GetColor(tint);
                            block.SetColor(tint, new Color(current.r * 1.15f + 0.20f, current.g * 1.13f + 0.16f,
                                current.b * 1.11f + 0.10f, current.a));
                        }
                    }
                    renderer.SetPropertyBlock(block, i);
                }
                source.forceRenderingOff = true;
            }
        }
    }
}
