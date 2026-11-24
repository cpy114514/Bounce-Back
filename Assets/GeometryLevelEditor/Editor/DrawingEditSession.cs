using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BounceBack.Geometry.Editor
{
    internal sealed class DrawingEditSession
    {
        internal const float MinSpacing = 0.05f;
        internal const float MaxSpacing = 100f;
        internal const float MinWidth = 0.01f;
        internal const float MaxWidth = 20f;
        internal const float PositionLimit = 100000f;

        private readonly HashSet<string> selectedNodes = new();
        private readonly HashSet<string> selectedEdges = new();
        private Fragment clipboard;

        internal event Action Changed;
        internal event Action<string> Feedback;

        internal GeometryDrawing Drawing { get; private set; }
        internal string ActiveLayerId { get; private set; }
        internal DrawingLayer Layer => Drawing ? Drawing.layers.Find(layer => layer.Id == ActiveLayerId) : null;
        internal string ChainNodeId { get; private set; }
        internal IReadOnlyCollection<string> SelectedNodes => selectedNodes;
        internal IReadOnlyCollection<string> SelectedEdges => selectedEdges;
        internal bool HasSelection => selectedNodes.Count + selectedEdges.Count > 0;
        internal bool HasClipboard => clipboard != null;
        internal bool CanEdit => Layer is { Visible: true, Locked: false };

        internal void Open(GeometryDrawing drawing, string layerId = null)
        {
            Drawing = drawing;
            ActiveLayerId = layerId;
            ClearInteraction();
            RefreshFromAsset();
        }

        internal void RefreshFromAsset(bool finishChain = false)
        {
            if (Layer == null)
            {
                ActiveLayerId = Drawing ? Drawing.layers.LastOrDefault()?.Id : null;
                ClearInteraction();
            }
            else if (CanEdit)
            {
                if (finishChain || !Layer.nodes.Exists(node => node.Id == ChainNodeId))
                    ChainNodeId = null;
                selectedNodes.IntersectWith(Layer.nodes.Select(node => node.Id));
                selectedEdges.IntersectWith(Layer.edges.Select(edge => edge.Id));
            }
            else
            {
                ClearInteraction();
            }
            Changed?.Invoke();
        }

        internal void ActivateLayer(string layerId)
        {
            if (ActiveLayerId == layerId)
                return;

            ActiveLayerId = layerId;
            ClearInteraction();
            Changed?.Invoke();
        }

        internal void AddLayer()
        {
            int number = 1;
            while (Drawing.layers.Exists(layer => layer.Name == $"Layer {number}"))
                number++;

            var layer = new DrawingLayer($"Layer {number}");
            Edit("Add Drawing Layer", () =>
            {
                Drawing.layers.Add(layer);
                ActiveLayerId = layer.Id;
                ClearInteraction();
            });
        }

        internal void RemoveLayer(DrawingLayer layer)
        {
            if (layer == null || layer.Locked || Drawing.layers.Count <= 1)
                return;

            int index = Drawing.layers.IndexOf(layer);
            Edit("Remove Drawing Layer", () =>
            {
                Drawing.layers.RemoveAt(index);
                ActiveLayerId = Drawing.layers[Mathf.Min(index, Drawing.layers.Count - 1)].Id;
                ClearInteraction();
            });
        }

        internal void ReorderLayer(DrawingLayer layer, int offset)
        {
            int from = Drawing.layers.IndexOf(layer);
            int to = from + offset;
            if (from < 0 || to < 0 || to >= Drawing.layers.Count)
                return;

            Edit("Reorder Drawing Layers", () =>
            {
                Drawing.layers.RemoveAt(from);
                Drawing.layers.Insert(to, layer);
            });
        }

        internal void SetLayerVisible(DrawingLayer layer, bool value)
        {
            Edit(value ? "Show Drawing Layer" : "Hide Drawing Layer", () =>
            {
                layer.visible = value;
                if (layer.Id == ActiveLayerId)
                    ClearInteraction();
            });
        }

        internal void SetLayerLocked(DrawingLayer layer, bool value)
        {
            Edit(value ? "Lock Drawing Layer" : "Unlock Drawing Layer", () =>
            {
                layer.locked = value;
                if (layer.Id == ActiveLayerId)
                    ClearInteraction();
            });
        }

        internal Vector2 Snap(Vector2 position)
        {
            if (!Drawing.snap)
                return position;

            float spacing = Drawing.gridSpacing;
            return new Vector2(Mathf.Round(position.x / spacing), Mathf.Round(position.y / spacing)) * spacing;
        }

        internal void DrawTo(Vector2 position, string hitNodeId)
        {
            DrawingLayer layer = Layer;
            if (!CanEdit || !IsValidPosition(position))
                return;

            DrawingNode target = layer.nodes.Find(node => node.Id == hitNodeId) ??
                layer.nodes.Find(node => SamePosition(node.Position, position));
            DrawingNode start = layer.nodes.Find(node => node.Id == ChainNodeId);
            if (target != null && start == target)
                return;

            bool newNode = target == null;
            target ??= new DrawingNode(position);
            bool newEdge = start != null && !layer.edges.Exists(edge =>
                (edge.StartId == start.Id && edge.EndId == target.Id) ||
                (edge.EndId == start.Id && edge.StartId == target.Id));

            ChainNodeId = target.Id;
            if (newNode || newEdge)
            {
                Edit(newEdge ? "Draw Edge" : "Add Drawing Node", () =>
                {
                    if (newNode)
                        layer.nodes.Add(target);
                    if (newEdge)
                        layer.edges.Add(new DrawingEdge(start.Id, target.Id, Drawing.strokeColor, Drawing.strokeWidth));
                });
            }
            else
            {
                Changed?.Invoke();
            }
        }

        internal bool FinishChain()
        {
            if (ChainNodeId == null)
                return false;

            ChainNodeId = null;
            Changed?.Invoke();
            return true;
        }

        internal void Select(string nodeId, string edgeId, bool additive)
        {
            if (!CanEdit)
                return;

            var selection = nodeId != null ? selectedNodes : selectedEdges;
            string id = nodeId ?? edgeId;
            if (!additive && (id == null || !selection.Contains(id)))
            {
                selectedNodes.Clear();
                selectedEdges.Clear();
            }

            if (id != null && (!additive || !selection.Remove(id)))
                selection.Add(id);
            Changed?.Invoke();
        }

        internal void SelectNodes(IEnumerable<string> ids, bool additive)
        {
            if (!CanEdit)
                return;

            if (!additive)
            {
                selectedNodes.Clear();
                selectedEdges.Clear();
            }

            selectedNodes.UnionWith(ids);
            Changed?.Invoke();
        }

        internal void SelectAll()
        {
            if (!CanEdit)
                return;

            selectedNodes.UnionWith(Layer.nodes.Select(node => node.Id));
            selectedEdges.UnionWith(Layer.edges.Select(edge => edge.Id));
            Changed?.Invoke();
        }

        internal void ClearSelection()
        {
            if (!HasSelection)
                return;

            selectedNodes.Clear();
            selectedEdges.Clear();
            Changed?.Invoke();
        }

        internal bool IsNodeSelected(string id) => selectedNodes.Contains(id);
        internal bool IsEdgeSelected(string id) => selectedEdges.Contains(id);

        internal HashSet<string> SelectedEndpoints()
        {
            var ids = new HashSet<string>(selectedNodes);
            if (Layer == null || selectedEdges.Count == 0)
                return ids;

            foreach (DrawingEdge edge in Layer.edges.Where(edge => IsEdgeSelected(edge.Id)))
            {
                ids.Add(edge.StartId);
                ids.Add(edge.EndId);
            }

            return ids;
        }

        internal void MoveNodes(HashSet<string> ids, Vector2 delta)
        {
            if (!CanEdit || delta == Vector2.zero)
                return;

            DrawingLayer layer = Layer;
            var moving = layer.nodes.Where(node => ids.Contains(node.Id)).ToArray();
            if (moving.Length == 0)
                return;

            var stationary = layer.nodes.Where(node => !ids.Contains(node.Id)).ToArray();
            if (moving.Any(node => !CanPlace(node.Position + delta, stationary)))
            {
                Feedback?.Invoke("Move blocked by another node or the drawing bounds.");
                Changed?.Invoke();
                return;
            }

            Edit(moving.Length == 1 ? "Move Drawing Node" : "Move Drawing Selection", () =>
            {
                foreach (DrawingNode node in moving)
                    node.position += delta;
            });
        }

        internal void DeleteSelection()
        {
            if (!CanEdit || !HasSelection)
                return;

            DrawingLayer layer = Layer;
            Edit("Delete Drawing Selection", () =>
            {
                layer.edges.RemoveAll(edge => IsEdgeSelected(edge.Id) ||
                    IsNodeSelected(edge.StartId) || IsNodeSelected(edge.EndId));
                layer.nodes.RemoveAll(node => IsNodeSelected(node.Id));
                ClearInteraction();
            });
        }

        internal void Copy()
        {
            if (!CanEdit || !HasSelection)
                return;

            clipboard = CaptureSelection();
            Feedback?.Invoke($"Copied {clipboard.Positions.Length} nodes and {clipboard.Edges.Length} edges.");
            Changed?.Invoke();
        }

        internal bool Paste(Vector2 viewCenter) => Paste(clipboard, viewCenter);

        internal bool DuplicateSelection(Vector2 viewCenter) =>
            CanEdit && HasSelection && Paste(CaptureSelection(), viewCenter);

        private Fragment CaptureSelection()
        {
            var ids = SelectedEndpoints();
            var nodes = Layer.nodes.Where(node => ids.Contains(node.Id)).ToArray();
            var indices = new Dictionary<string, int>(nodes.Length);
            for (int index = 0; index < nodes.Length; index++)
                indices.Add(nodes[index].Id, index);

            var edges = Layer.edges.Where(edge => IsEdgeSelected(edge.Id) ||
                (IsNodeSelected(edge.StartId) && IsNodeSelected(edge.EndId)))
                .Select(edge => new FragmentEdge(indices[edge.StartId], indices[edge.EndId], edge.Color, edge.Width))
                .ToArray();
            return new Fragment(Drawing, nodes.Select(node => node.Position).ToArray(), edges);
        }

        private bool Paste(Fragment fragment, Vector2 viewCenter)
        {
            if (!CanEdit || fragment == null || fragment.Positions.Length == 0)
                return false;

            var layer = Layer;
            float spacing = Drawing.gridSpacing;
            Vector2 baseOffset = fragment.Source == Drawing ? Vector2.zero : Snap(viewCenter - fragment.Positions[0]);
            if (!TryFindPasteOffset(fragment.Positions, layer.nodes, baseOffset, spacing, out Vector2 offset))
            {
                Feedback?.Invoke("No room to paste near this drawing position.");
                return false;
            }

            Edit("Paste Drawing Selection", () =>
            {
                ClearInteraction();
                var nodes = fragment.Positions.Select(point => new DrawingNode(point + offset)).ToArray();
                layer.nodes.AddRange(nodes);
                selectedNodes.UnionWith(nodes.Select(node => node.Id));
                foreach (FragmentEdge edge in fragment.Edges)
                {
                    var copy = new DrawingEdge(nodes[edge.Start].Id, nodes[edge.End].Id, edge.Color, edge.Width);
                    layer.edges.Add(copy);
                    selectedEdges.Add(copy.Id);
                }
            });
            return true;
        }

        private static readonly Vector2[] PasteDirections =
        {
            new(1f, -1f), new(-1f, 1f), new(-1f, -1f), new(1f, 1f),
            Vector2.right, Vector2.down, Vector2.left, Vector2.up
        };

        private static bool TryFindPasteOffset(Vector2[] positions, List<DrawingNode> nodes,
            Vector2 baseOffset, float spacing, out Vector2 offset)
        {
            for (int distance = 1; distance <= Mathf.Max(32, nodes.Count); distance++)
            {
                foreach (Vector2 direction in PasteDirections)
                {
                    Vector2 candidate = baseOffset + direction * (spacing * distance);
                    if (positions.All(point => CanPlace(point + candidate, nodes)))
                    {
                        offset = candidate;
                        return true;
                    }
                }
            }
            offset = default;
            return false;
        }

        internal void Edit(string label, Action mutation)
        {
            if (!Drawing)
                return;

            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(Drawing, label);
            mutation();
            EditorUtility.SetDirty(Drawing);
            Changed?.Invoke();
        }

        private void ClearInteraction()
        {
            ChainNodeId = null;
            selectedNodes.Clear();
            selectedEdges.Clear();
        }

        private static bool CanPlace(Vector2 point, IReadOnlyList<DrawingNode> nodes)
        {
            if (!IsValidPosition(point))
                return false;

            foreach (DrawingNode node in nodes)
                if (SamePosition(node.Position, point))
                    return false;

            return true;
        }

        internal static bool IsValidPosition(Vector2 position) =>
            Mathf.Abs(position.x) <= PositionLimit && Mathf.Abs(position.y) <= PositionLimit;

        private static bool SamePosition(Vector2 first, Vector2 second) =>
            (first - second).sqrMagnitude < 0.00000001f;

        private sealed class Fragment
        {
            internal readonly GeometryDrawing Source;
            internal readonly Vector2[] Positions;
            internal readonly FragmentEdge[] Edges;

            internal Fragment(GeometryDrawing source, Vector2[] positions, FragmentEdge[] edges)
            {
                Source = source;
                Positions = positions;
                Edges = edges;
            }
        }

        private readonly struct FragmentEdge
        {
            internal readonly int Start;
            internal readonly int End;
            internal readonly Color Color;
            internal readonly float Width;

            internal FragmentEdge(int start, int end, Color color, float width)
            {
                Start = start;
                End = end;
                Color = color;
                Width = width;
            }
        }
    }
}
