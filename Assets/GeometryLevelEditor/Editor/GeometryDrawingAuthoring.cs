using System;
using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.Geometry.Editor
{
    public static class GeometryDrawingAuthoring
    {
        public static GeometryDrawing CreateOutline(IReadOnlyList<Vector2> points, Color color, float width)
        {
            if (points == null || points.Count < 3 || width <= 0)
                throw new ArgumentException("An outline needs at least three points and a positive stroke width.");
            var drawing = ScriptableObject.CreateInstance<GeometryDrawing>();
            var layer = drawing.layers[0];
            foreach (Vector2 point in points)
                layer.nodes.Add(new DrawingNode(point));
            for (int i = 0; i < points.Count; i++)
                layer.edges.Add(new DrawingEdge(layer.nodes[i].Id, layer.nodes[(i + 1) % points.Count].Id, color, width));
            drawing.strokeColor = color;
            drawing.strokeWidth = width;
            return drawing;
        }
    }
}
