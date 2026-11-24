using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace BounceBack.Geometry.Editor
{
    internal sealed class DrawingCanvas : VisualElement
    {
        internal const float DefaultZoom = 46f;
        private const float MinZoom = 0.005f;
        private const float MaxZoom = 320f;
        private const float NodeHitRadius = 9f;
        private static readonly Color SelectionColor = new Color32(129, 232, 208, 255);
        private static readonly Vector2[] DotDirections =
        {
            new(1f, 0f), new(0.5f, 0.866f), new(-0.5f, 0.866f),
            new(-1f, 0f), new(-0.5f, -0.866f), new(0.5f, -0.866f)
        };

        private readonly DrawingEditSession session;
        private readonly Dictionary<string, Vector2> canvasPositions = new();
        private Vector2? hover;
        private Vector2 pointerStart;
        private Vector2 panStart;
        private Vector2 dragAnchor;
        private Vector2 dragDelta;
        private HashSet<string> draggingNodes;
        private int capturedPointer = -1;
        private Gesture gesture;
        private bool additiveSelection;
        private bool spaceHeld;
        private bool drawMode = true;

        private enum Gesture { None, Pan, Move, Marquee }

        internal event Action ViewChanged;
        internal event Action<KeyDownEvent> Shortcut;
        internal Vector2 Center { get; private set; }
        internal float Zoom { get; private set; }
        internal Vector2? CursorPosition => hover.HasValue ? ToDrawing(hover.Value) : null;

        internal bool DrawMode
        {
            get => drawMode;
            set
            {
                if (drawMode == value)
                    return;
                drawMode = value;
                CancelGesture();
                MarkDirtyRepaint();
            }
        }

        internal DrawingCanvas(DrawingEditSession session, Vector2 center, float zoom)
        {
            this.session = session;
            Center = center;
            Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            name = "geometry-canvas";
            AddToClassList("geometry-canvas");
            focusable = true;
            tabIndex = 0;
            generateVisualContent += DrawCanvas;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => ResetGesture());
            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (capturedPointer < 0)
                {
                    hover = null;
                    RefreshView();
                }
            });
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Space)
                {
                    spaceHeld = false;
                    evt.StopPropagation();
                }
            });
            RegisterCallback<BlurEvent>(_ =>
            {
                spaceHeld = false;
                CancelGesture();
            });
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private Vector2 ViewOrigin => contentRect.center;
        private Vector2 ToDrawing(Vector2 point)
        {
            Vector2 offset = (point - ViewOrigin) / Zoom;
            return Center + new Vector2(offset.x, -offset.y);
        }

        private Vector2 ToCanvas(Vector2 point)
        {
            Vector2 offset = (point - Center) * Zoom;
            return ViewOrigin + new Vector2(offset.x, -offset.y);
        }

        internal void ResetView()
        {
            CancelGesture();
            hover = null;
            Center = Vector2.zero;
            Zoom = DefaultZoom;
            RefreshView();
        }

        internal void Frame()
        {
            if (!session.Drawing || contentRect.width < 10f || contentRect.height < 10f)
                return;

            IEnumerable<DrawingNode> nodes = session.Drawing.Layers.Where(layer => layer.Visible)
                .SelectMany(layer => layer.Nodes);
            if (session.HasSelection)
            {
                var ids = session.SelectedEndpoints();
                nodes = session.Layer.Nodes.Where(node => ids.Contains(node.Id));
            }

            var positions = nodes.Select(node => node.Position).ToArray();
            if (positions.Length == 0)
            {
                ResetView();
                return;
            }

            Vector2 min = positions[0];
            Vector2 max = min;
            foreach (Vector2 point in positions)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Center = (min + max) / 2f;
            Vector2 extent = max - min;
            if (extent.sqrMagnitude > 0f)
            {
                float horizontal = Mathf.Max(10f, contentRect.width - 76f) / Mathf.Max(extent.x, 0.001f);
                float vertical = Mathf.Max(10f, contentRect.height - 76f) / Mathf.Max(extent.y, 0.001f);
                Zoom = Mathf.Clamp(Mathf.Min(horizontal, vertical), MinZoom, MaxZoom);
            }
            RefreshView();
        }

        internal void RevealPastedSelection()
        {
            var ids = session.SelectedEndpoints();
            if (session.Layer.Nodes.Any(node => ids.Contains(node.Id) && !contentRect.Contains(ToCanvas(node.Position))))
                Frame();
        }

        internal bool CancelGesture()
        {
            if (capturedPointer < 0)
                return false;

            int pointer = capturedPointer;
            ResetGesture();
            if (this.HasPointerCapture(pointer))
                this.ReleasePointer(pointer);
            return true;
        }

        private void ResetGesture()
        {
            capturedPointer = -1;
            gesture = Gesture.None;
            draggingNodes = null;
            dragDelta = Vector2.zero;
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!session.Drawing || capturedPointer >= 0)
                return;

            Focus();
            Vector2 point = this.WorldToLocal(evt.position);
            hover = point;

            if (evt.button == 2 || (evt.button == 0 && spaceHeld))
            {
                panStart = Center;
                StartGesture(Gesture.Pan, evt.pointerId, point);
            }
            else if (evt.button == 1)
            {
                session.FinishChain();
            }
            else if (evt.button == 0 && session.CanEdit)
            {
                string nodeId = HitNode(point);
                if (DrawMode)
                {
                    session.DrawTo(session.Snap(ToDrawing(point)), nodeId);
                }
                else
                {
                    DrawingEdge edge = nodeId == null ? HitEdge(point) : null;
                    if (nodeId != null || edge != null)
                    {
                        session.Select(nodeId, edge?.Id, evt.shiftKey);
                        bool hitSelected = nodeId != null ? session.IsNodeSelected(nodeId) : session.IsEdgeSelected(edge.Id);
                        if (hitSelected)
                        {
                            draggingNodes = session.SelectedEndpoints();
                            dragAnchor = session.Layer.nodes.Find(node => node.Id == (nodeId ?? edge.StartId)).Position;
                            StartGesture(Gesture.Move, evt.pointerId, point);
                        }
                    }
                    else
                    {
                        additiveSelection = evt.shiftKey;
                        StartGesture(Gesture.Marquee, evt.pointerId, point);
                    }
                }
            }

            RefreshView();
            evt.StopPropagation();
        }

        private void StartGesture(Gesture next, int pointerId, Vector2 point)
        {
            gesture = next;
            pointerStart = point;
            capturedPointer = pointerId;
            this.CapturePointer(pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 point = this.WorldToLocal(evt.position);
            hover = point;
            if (capturedPointer == evt.pointerId)
                UpdateGesture(point);
            RefreshView();
        }

        private void UpdateGesture(Vector2 point)
        {
            Vector2 movement = (point - pointerStart) / Zoom;
            var delta = new Vector2(movement.x, -movement.y);
            if (gesture == Gesture.Pan)
            {
                Center = LimitCenter(panStart - delta);
            }
            else if (gesture == Gesture.Move)
            {
                dragDelta = (point - pointerStart).sqrMagnitude < 9f
                    ? Vector2.zero
                    : session.Snap(dragAnchor + delta) - dragAnchor;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != capturedPointer)
                return;

            Vector2 point = this.WorldToLocal(evt.position);
            UpdateGesture(point);
            if (gesture == Gesture.Move)
            {
                session.MoveNodes(draggingNodes, dragDelta);
            }
            else if (gesture == Gesture.Marquee)
            {
                Rect area = Marquee(point);
                var ids = session.Layer.Nodes.Where(node => area.Contains(ToCanvas(node.Position))).Select(node => node.Id);
                session.SelectNodes(ids, additiveSelection);
            }

            CancelGesture();
            hover = contentRect.Contains(point) ? point : null;
            RefreshView();
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            if (!session.Drawing || capturedPointer >= 0)
                return;

            Vector2 point = this.WorldToLocal(evt.mousePosition);
            Vector2 fixedPoint = ToDrawing(point);
            Zoom = Mathf.Clamp(Zoom * Mathf.Pow(1.10f, -evt.delta.y), MinZoom, MaxZoom);
            Center = LimitCenter(Center + fixedPoint - ToDrawing(point));
            hover = point;
            RefreshView();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space && !evt.actionKey)
            {
                spaceHeld = true;
                evt.StopPropagation();
                return;
            }

            Shortcut?.Invoke(evt);
        }

        private void RefreshView()
        {
            MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }

        private static Vector2 LimitCenter(Vector2 point)
        {
            const float limit = DrawingEditSession.PositionLimit;
            return new Vector2(Mathf.Clamp(point.x, -limit, limit), Mathf.Clamp(point.y, -limit, limit));
        }

        private string HitNode(Vector2 point)
        {
            string hit = null;
            float nearest = NodeHitRadius * NodeHitRadius;
            foreach (DrawingNode node in session.Layer.Nodes)
            {
                float distance = (ToCanvas(node.Position) - point).sqrMagnitude;
                if (distance > nearest)
                    continue;
                nearest = distance;
                hit = node.Id;
            }
            return hit;
        }

        private DrawingEdge HitEdge(Vector2 point)
        {
            IndexPositions(session.Layer);
            DrawingEdge hit = null;
            float nearest = float.PositiveInfinity;
            for (int index = session.Layer.Edges.Count - 1; index >= 0; index--)
            {
                DrawingEdge edge = session.Layer.Edges[index];
                if (!canvasPositions.TryGetValue(edge.StartId, out Vector2 start) ||
                    !canvasPositions.TryGetValue(edge.EndId, out Vector2 end))
                    continue;

                Vector2 segment = end - start;
                float fraction = Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                    Mathf.Max(segment.sqrMagnitude, 0.000001f));
                float distance = Vector2.Distance(point, start + fraction * segment);
                if (distance <= Mathf.Max(6f, edge.Width * Zoom / 2f + 2f) && distance < nearest)
                {
                    nearest = distance;
                    hit = edge;
                }
            }
            return hit;
        }

        private void IndexPositions(DrawingLayer layer)
        {
            canvasPositions.Clear();
            bool active = layer.Id == session.ActiveLayerId;
            foreach (DrawingNode node in layer.Nodes)
            {
                Vector2 point = node.Position;
                if (active && draggingNodes != null && draggingNodes.Contains(node.Id))
                    point += dragDelta;
                canvasPositions[node.Id] = ToCanvas(point);
            }
        }

        private void DrawCanvas(MeshGenerationContext context)
        {
            if (!session.Drawing || contentRect.width <= 0f || contentRect.height <= 0f)
                return;

            if (session.Drawing.showGrid)
                DrawGrid(context);

            Painter2D painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            foreach (DrawingLayer layer in session.Drawing.Layers)
            {
                if (layer.Visible)
                    DrawLayer(painter, layer);
            }

            if (CanPreviewStroke())
                DrawStrokePreview(painter);
            if (gesture == Gesture.Marquee && hover.HasValue)
                DrawMarquee(painter, Marquee(hover.Value));
        }

        private void DrawGrid(MeshGenerationContext context)
        {
            float spacing = session.Drawing.gridSpacing;
            float screenSpacing = spacing * Zoom;
            while (screenSpacing < 11f ||
                (contentRect.width / screenSpacing + 1f) * (contentRect.height / screenSpacing + 1f) > 4000f)
            {
                spacing *= 2f;
                screenSpacing *= 2f;
            }

            Vector2 min = ToDrawing(new Vector2(contentRect.xMin, contentRect.yMax)) / spacing;
            Vector2 max = ToDrawing(new Vector2(contentRect.xMax, contentRect.yMin)) / spacing;
            int firstX = Mathf.CeilToInt(min.x);
            int firstY = Mathf.CeilToInt(min.y);
            int lastX = Mathf.FloorToInt(max.x);
            int lastY = Mathf.FloorToInt(max.y);
            int count = Mathf.Max(0, lastX - firstX + 1) * Mathf.Max(0, lastY - firstY + 1);
            if (count == 0)
                return;

            MeshWriteData mesh = context.Allocate(count * 13, count * 54);
            ushort vertexIndex = 0;
            for (int y = firstY; y <= lastY; y++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    Vector2 point = ToCanvas(new Vector2(x, y) * spacing);
                    bool origin = x == 0 && y == 0;
                    bool axis = x == 0 || y == 0;
                    byte opacity = origin ? (byte)235 : axis ? (byte)156 : (byte)110;
                    WriteGridDot(mesh, point, origin ? 2.5f : 1.55f, opacity, vertexIndex);
                    vertexIndex += 13;
                }
            }
        }

        private static void WriteGridDot(MeshWriteData mesh, Vector2 point, float radius, byte opacity, ushort index)
        {
            SetVertex(mesh, point, opacity);
            foreach (Vector2 direction in DotDirections)
                SetVertex(mesh, point + direction * radius * 0.55f, opacity);
            foreach (Vector2 direction in DotDirections)
                SetVertex(mesh, point + direction * radius, 0);

            for (int side = 0; side < DotDirections.Length; side++)
            {
                ushort inner = (ushort)(index + 1 + side);
                ushort next = (ushort)(index + 1 + (side + 1) % DotDirections.Length);
                ushort outer = (ushort)(inner + DotDirections.Length);
                ushort nextOuter = (ushort)(next + DotDirections.Length);
                mesh.SetNextIndex(index);
                mesh.SetNextIndex(inner);
                mesh.SetNextIndex(next);
                mesh.SetNextIndex(inner);
                mesh.SetNextIndex(outer);
                mesh.SetNextIndex(nextOuter);
                mesh.SetNextIndex(inner);
                mesh.SetNextIndex(nextOuter);
                mesh.SetNextIndex(next);
            }
        }

        private static void SetVertex(MeshWriteData mesh, Vector2 point, byte opacity)
        {
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(point.x, point.y, Vertex.nearZ),
                tint = new Color32(255, 255, 255, opacity)
            });
        }

        private void DrawLayer(Painter2D painter, DrawingLayer layer)
        {
            bool active = layer.Id == session.ActiveLayerId;
            IndexPositions(layer);
            foreach (DrawingEdge edge in layer.Edges)
            {
                if (!canvasPositions.TryGetValue(edge.StartId, out Vector2 start) ||
                    !canvasPositions.TryGetValue(edge.EndId, out Vector2 end))
                    continue;

                float width = Mathf.Max(1f, edge.Width * Zoom);
                float margin = width / 2f + 4f;
                if (Mathf.Max(start.x, end.x) < -margin || Mathf.Min(start.x, end.x) > contentRect.width + margin ||
                    Mathf.Max(start.y, end.y) < -margin || Mathf.Min(start.y, end.y) > contentRect.height + margin)
                    continue;

                if (active && session.IsEdgeSelected(edge.Id))
                    Stroke(painter, start, end, width + 4f, SelectionColor);

                Color color = edge.Color;
                if (!active)
                    color.a *= 0.56f;
                Stroke(painter, start, end, width, color);
            }

            foreach (DrawingNode node in layer.Nodes)
            {
                Vector2 point = canvasPositions[node.Id];
                if (!contentRect.Contains(point))
                    continue;

                bool selected = active && session.IsNodeSelected(node.Id);
                bool chain = active && node.Id == session.ChainNodeId;
                if (chain || selected)
                    Dot(painter, point, 6f, SelectionColor);

                Color color = chain || selected ? new Color32(30, 43, 48, 255) :
                    new Color32(238, 239, 247, active ? (byte)235 : (byte)130);
                Dot(painter, point, active ? 3.4f : 2.7f, color);
            }
        }

        private bool CanPreviewStroke() =>
            DrawMode && session.CanEdit && hover.HasValue && gesture == Gesture.None && !spaceHeld;

        private void DrawStrokePreview(Painter2D painter)
        {
            Vector2 point = hover.Value;
            string hit = HitNode(point);
            DrawingNode target = session.Layer.nodes.Find(node => node.Id == hit);
            Vector2 position = target?.Position ?? session.Snap(ToDrawing(point));
            if (!DrawingEditSession.IsValidPosition(position))
                return;

            Vector2 end = ToCanvas(position);
            DrawingNode start = session.Layer.nodes.Find(node => node.Id == session.ChainNodeId);
            Color preview = session.Drawing.strokeColor;
            preview.a *= 0.55f;
            if (start != null && (end - ToCanvas(start.Position)).sqrMagnitude > 0.01f)
                Stroke(painter, ToCanvas(start.Position), end, Mathf.Max(1f, session.Drawing.strokeWidth * Zoom), preview);
            Dot(painter, end, 4.8f, SelectionColor);
        }

        private Rect Marquee(Vector2 point)
        {
            Vector2 min = Vector2.Min(pointerStart, point);
            Vector2 max = Vector2.Max(pointerStart, point);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void DrawMarquee(Painter2D painter, Rect rect)
        {
            if (rect.width < 1f || rect.height < 1f)
                return;

            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.fillColor = new Color(0.5f, 0.9f, 0.82f, 0.13f);
            painter.Fill();
            painter.lineWidth = 1f;
            painter.strokeColor = SelectionColor;
            painter.Stroke();
        }

        private static void Stroke(Painter2D painter, Vector2 start, Vector2 end, float width, Color color)
        {
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.Stroke();
        }

        private static void Dot(Painter2D painter, Vector2 point, float radius, Color color)
        {
            painter.BeginPath();
            painter.Arc(point, radius, 0f, 360f);
            painter.fillColor = color;
            painter.Fill();
        }
    }
}
