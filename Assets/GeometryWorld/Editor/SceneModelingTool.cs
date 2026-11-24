using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BounceBack.GeometryWorld.Editor
{
    internal enum ModelingMode { Object, Vertex, Edge, Face }

    internal sealed class SceneModelingTool
    {
        private static readonly Color Highlight = new(0.36f, 1f, 0.81f, 1f);
        private static readonly Color Wire = new(0.75f, 0.79f, 1f, 0.58f);
        private static readonly Color VertexColor = new(0.86f, 0.9f, 1f, 0.9f);
        private MeshTopology cached;
        private Dictionary<TopologyEdge, List<int>> edges;
        private int dragGroup = -1;

        internal readonly HashSet<int> Vertices = new();
        internal TopologyEdge? Edge { get; private set; }
        internal int Face { get; private set; } = -1;
        internal ModelingMode Mode { get; private set; }
        internal bool SymmetryX;
        internal float SnapStep;
        internal event Action<string> Feedback;
        internal event Action Changed;

        internal void SelectMode(ModelingMode mode)
        {
            if (mode == Mode)
                return;
            Mode = mode;
            Reset();
        }

        internal void Reset()
        {
            FinishDrag();
            Vertices.Clear();
            Edge = null;
            Face = -1;
            cached = null;
            edges = null;
            Changed?.Invoke();
        }

        internal void Edit(GeometryObject instance, string label, Action<MeshTopology> change, bool clear = false)
        {
            if (!instance || instance.editLocked || !instance.model || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var topology = instance.model.topology.Copy();
            try
            {
                change(topology);
                GeometryAssets.CommitModel(instance.model, topology, instance.model.materials, label);
                if (clear)
                    Reset();
                else
                    Changed?.Invoke();
            }
            catch (InvalidOperationException error)
            {
                Feedback?.Invoke(error.Message);
            }
        }

        internal void AssignFaceMaterial(GeometryObject instance, Material material)
        {
            if (!instance || !material || Face < 0 || instance.editLocked)
                return;
            var asset = instance.model;
            int slot = Array.IndexOf(asset.materials, material);
            var materials = asset.materials;
            if (slot < 0)
            {
                slot = materials.Length;
                materials = materials.Append(material).ToArray();
            }
            var topology = asset.topology.Copy();
            topology.faces[Face].material = slot;
            GeometryAssets.CommitModel(asset, topology, materials, "Assign Face Material");
            Changed?.Invoke();
        }

        internal void Draw(GeometryObject instance)
        {
            if (Mode == ModelingMode.Object || !instance || instance.editLocked || !instance.model || !instance.surface ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var current = Event.current;
            if (current.rawType == EventType.MouseUp)
                FinishDrag();
            var mesh = instance.model.topology;
            if (cached != mesh)
            {
                cached = mesh;
                edges = mesh.GetEdges();
            }

            Transform surface = instance.surface.transform;
            CompareFunction previous = Handles.zTest;
            using var scope = new Handles.DrawingScope();
            Handles.zTest = CompareFunction.LessEqual;
            if (current.type == EventType.Repaint)
            {
                foreach (var edge in edges.Keys)
                {
                    Handles.color = Edge.HasValue && Edge.Value.Equals(edge) ? Highlight : Wire;
                    Handles.DrawLine(surface.TransformPoint(mesh.vertices[edge.A]), surface.TransformPoint(mesh.vertices[edge.B]));
                }
                if (Mode == ModelingMode.Face && Face >= 0 && Face < mesh.faces.Count)
                {
                    var ring = mesh.faces[Face].vertices;
                    Handles.color = Highlight;
                    for (int i = 0; i < ring.Count; i++)
                        Handles.DrawLine(surface.TransformPoint(mesh.vertices[ring[i]]),
                            surface.TransformPoint(mesh.vertices[ring[(i + 1) % ring.Count]]), 3f);
                }
            }
            if (Mode == ModelingMode.Vertex)
            {
                for (int i = 0; i < mesh.vertices.Count; i++)
                {
                    if (!Pick(surface.TransformPoint(mesh.vertices[i]), Vertices.Contains(i), Handles.DotHandleCap))
                        continue;
                    bool hadVertex = Vertices.Contains(i);
                    if (!current.shift)
                        Vertices.Clear();
                    if (!current.shift || !hadVertex)
                        Vertices.Add(i);
                    else
                        Vertices.Remove(i);
                    Changed?.Invoke();
                }
            }
            else if (Mode == ModelingMode.Edge)
            {
                foreach (var edge in edges.Keys)
                    if (Pick(surface.TransformPoint((mesh.vertices[edge.A] + mesh.vertices[edge.B]) / 2f), Edge.HasValue && Edge.Value.Equals(edge),
                        Handles.DotHandleCap))
                    {
                        Edge = edge;
                        Changed?.Invoke();
                    }
            }
            else
            {
                for (int i = 0; i < mesh.faces.Count; i++)
                    if (Pick(surface.TransformPoint(mesh.Center(mesh.faces[i])), Face == i, Handles.RectangleHandleCap))
                    {
                        Face = i;
                        Changed?.Invoke();
                    }
            }
            Handles.zTest = previous;
            DrawMoveHandle(instance, surface, mesh);
        }

        private static bool Pick(Vector3 point, bool selected, Handles.CapFunction cap)
        {
            Handles.color = selected ? Highlight : VertexColor;
            float scale = HandleUtility.GetHandleSize(point);
            return Handles.Button(point, Quaternion.identity, scale * (selected ? 0.048f : 0.037f), scale * 0.055f, cap);
        }

        private void DrawMoveHandle(GeometryObject instance, Transform surface, MeshTopology mesh)
        {
            IReadOnlyCollection<int> indices = Mode switch
            {
                ModelingMode.Vertex => Vertices,
                ModelingMode.Edge when Edge.HasValue => new[] { Edge.Value.A, Edge.Value.B },
                ModelingMode.Face when Face >= 0 && Face < mesh.faces.Count => mesh.faces[Face].vertices,
                _ => Array.Empty<int>()
            };
            if (indices.Count == 0)
                return;
            Vector3 center = Vector3.zero;
            foreach (int i in indices)
                center += mesh.vertices[i];
            center /= indices.Count;

            Vector3 initial = surface.TransformPoint(center);
            EditorGUI.BeginChangeCheck();
            Vector3 updated = Handles.PositionHandle(initial, Tools.pivotRotation == PivotRotation.Local ? surface.rotation : Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
                return;
            Vector3 position = surface.InverseTransformPoint(updated);
            if (SnapStep > 0f || EditorGUI.actionKey)
            {
                float increment = SnapStep > 0f ? SnapStep : Mathf.Max(EditorSnapSettings.move.x, 0.001f);
                position = new Vector3(Mathf.Round(position.x / increment), Mathf.Round(position.y / increment),
                    Mathf.Round(position.z / increment)) * increment;
            }
            if ((position - center).sqrMagnitude < 1e-10f)
                return;
            if (dragGroup < 0)
            {
                Undo.IncrementCurrentGroup();
                dragGroup = Undo.GetCurrentGroup();
            }
            Edit(instance, "Move Mesh Elements", candidate => TopologyEdits.Move(candidate, indices, position - center, SymmetryX));
        }

        private void FinishDrag()
        {
            if (dragGroup < 0)
                return;
            Undo.CollapseUndoOperations(dragGroup);
            dragGroup = -1;
        }
    }
}
