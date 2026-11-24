using System;
using System.Collections.Generic;
using System.Linq;
using BounceBack.Geometry;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal enum GeometryShape
    {
        Cube, Plane, Sphere, Cylinder, Cone, Prism, Pyramid, CustomPolygon,
        Archway, Column, Bridge, Stairs, Ramp, Platform, Tower, CircularPlatform, CurvedPath, BallTrack
    }

    internal enum DrawingConversion { Solid, Surface, Beams }

    internal static class GeometryGenerators
    {
        internal static MeshTopology Generate(GeometryShape shape, Vector3 size, int segments, float arc,
            IReadOnlyList<Vector2> customPolygon = null)
        {
            size = new Vector3(Mathf.Clamp(size.x, 0.05f, 1000f), Mathf.Clamp(size.y, 0.05f, 1000f),
                Mathf.Clamp(size.z, 0.05f, 1000f));
            segments = Mathf.Clamp(segments, 3, 64);
            var model = new MeshTopology();
            switch (shape)
            {
                case GeometryShape.Cube:
                case GeometryShape.Platform:
                    Box(model, Vector3.zero, size);
                    break;
                case GeometryShape.Plane:
                    AddPolygon(model, Rectangle(size.x, size.z), 0f, 0f, 0);
                    break;
                case GeometryShape.Sphere:
                    Sphere(model, size * 0.5f, segments);
                    break;
                case GeometryShape.Cylinder:
                case GeometryShape.Prism:
                case GeometryShape.CircularPlatform:
                    AddPolygon(model, RegularPolygon(size.x, size.z, segments), -size.y / 2f, size.y, 0);
                    break;
                case GeometryShape.Cone:
                case GeometryShape.Pyramid:
                    Pointed(model, size, shape == GeometryShape.Pyramid ? 4 : segments);
                    break;
                case GeometryShape.CustomPolygon:
                    AddPolygon(model, customPolygon ?? throw new ArgumentException("Enter a polygon outline."),
                        -size.y / 2f, size.y, 0);
                    break;
                case GeometryShape.Ramp:
                    AddProfile(model, new[]
                    {
                        new Vector2(-size.x / 2f, -size.y / 2f), new Vector2(size.x / 2f, -size.y / 2f),
                        new Vector2(size.x / 2f, size.y / 2f)
                    }, size.z);
                    break;
                case GeometryShape.Stairs:
                    Staircase(model, size, segments);
                    break;
                case GeometryShape.Archway:
                    Arch(model, size, segments);
                    break;
                case GeometryShape.Column:
                case GeometryShape.Tower:
                    Column(model, size, segments, shape == GeometryShape.Tower);
                    break;
                case GeometryShape.Bridge:
                case GeometryShape.BallTrack:
                    Track(model, size, shape == GeometryShape.BallTrack);
                    break;
                case GeometryShape.CurvedPath:
                    CurvedPath(model, size, segments, arc);
                    break;
            }
            return model;
        }

        internal static MeshTopology Convert(GeometryDrawing drawing, DrawingConversion mode, float depth,
            float width, bool useStrokeWidths, List<Color> palette, out List<string> issues)
        {
            var model = new MeshTopology();
            issues = new List<string>();
            foreach (var layer in drawing.Layers.Where(layer => layer.Visible))
            {
                var nodes = layer.Nodes.ToDictionary(node => node.Id, node => node.Position);
                if (mode == DrawingConversion.Beams)
                {
                    foreach (var edge in layer.Edges)
                    {
                        if (!nodes.TryGetValue(edge.StartId, out Vector2 from) ||
                            !nodes.TryGetValue(edge.EndId, out Vector2 to) || (from - to).sqrMagnitude < 1e-8f)
                        {
                            issues.Add($"{layer.Name} has an edge with invalid endpoints.");
                            continue;
                        }

                        float thickness = useStrokeWidths ? edge.Width * width : width;
                        Vector3 delta = new(to.x - from.x, 0f, to.y - from.y);
                        Vector3 center = new((to.x + from.x) / 2f, depth / 2f, (to.y + from.y) / 2f);
                        int first = model.vertices.Count;
                        Box(model, Vector3.zero, new Vector3(delta.magnitude, depth, Mathf.Max(0.01f, thickness)),
                            ColorSlot(palette, edge.Color));
                        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
                        for (int index = first; index < model.vertices.Count; index++)
                            model.vertices[index] = center + rotation * model.vertices[index];
                    }
                    continue;
                }

                var adjacent = new Dictionary<string, List<DrawingEdge>>();
                foreach (var edge in layer.Edges)
                {
                    if (!nodes.ContainsKey(edge.StartId) || !nodes.ContainsKey(edge.EndId) || edge.StartId == edge.EndId)
                    {
                        issues.Add($"{layer.Name} has an edge with invalid endpoints.");
                        continue;
                    }
                    AddNeighbor(adjacent, edge.StartId, edge);
                    AddNeighbor(adjacent, edge.EndId, edge);
                }

                var visited = new HashSet<string>();
                foreach (string id in adjacent.Keys)
                {
                    if (!visited.Add(id))
                        continue;
                    var component = new List<string> { id };
                    for (int i = 0; i < component.Count; i++)
                        foreach (var edge in adjacent[component[i]])
                        {
                            string neighbor = OtherEndpoint(edge, component[i]);
                            if (visited.Add(neighbor))
                                component.Add(neighbor);
                        }

                    if (component.Count < 3 || component.Any(node => adjacent[node].Count != 2))
                    {
                        issues.Add($"{layer.Name} has an open or branched outline. Use Beams or draw a separate closed loop.");
                        continue;
                    }

                    var outline = new List<Vector2>();
                    string current = id;
                    DrawingEdge next = adjacent[id][0];
                    Color color = next.Color;
                    do
                    {
                        outline.Add(nodes[current]);
                        current = OtherEndpoint(next, current);
                        var pair = adjacent[current];
                        next = pair[0] == next ? pair[1] : pair[0];
                    } while (current != id && outline.Count < component.Count);

                    if (current != id || !PlanarPolygon.TryTriangulate(outline, out _, out _))
                    {
                        issues.Add($"{layer.Name} has a crossed, touching or flat outline.");
                        continue;
                    }
                    AddPolygon(model, outline, 0f, mode == DrawingConversion.Solid ? depth : 0f, ColorSlot(palette, color));
                }
            }

            if (model.faces.Count == 0 && issues.Count == 0)
                issues.Add("There are no edges on visible drawing layers.");
            return model;
        }

        private static void AddNeighbor(Dictionary<string, List<DrawingEdge>> adjacent, string id, DrawingEdge edge)
        {
            if (!adjacent.TryGetValue(id, out var list))
                adjacent.Add(id, list = new List<DrawingEdge>());
            list.Add(edge);
        }

        private static string OtherEndpoint(DrawingEdge edge, string id) => edge.StartId == id ? edge.EndId : edge.StartId;

        private static int ColorSlot(List<Color> palette, Color color)
        {
            int index = palette.IndexOf(color);
            if (index >= 0)
                return index;
            palette.Add(color);
            return palette.Count - 1;
        }

        private static Vector2[] Rectangle(float width, float length) => new[]
        {
            new Vector2(-width / 2f, -length / 2f), new Vector2(-width / 2f, length / 2f),
            new Vector2(width / 2f, length / 2f), new Vector2(width / 2f, -length / 2f)
        };

        private static void Box(MeshTopology model, Vector3 center, Vector3 size, int material = 0)
        {
            int start = model.vertices.Count;
            AddPolygon(model, Rectangle(size.x, size.z), center.y - size.y / 2f, size.y, material);
            for (int i = start; i < model.vertices.Count; i++)
                model.vertices[i] += new Vector3(center.x, 0f, center.z);
        }

        private static Vector2[] RegularPolygon(float width, float depth, int segments)
        {
            var points = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = -i * Mathf.PI * 2f / segments;
                points[i] = new Vector2(Mathf.Cos(angle) * width / 2f, Mathf.Sin(angle) * depth / 2f);
            }
            return points;
        }

        private static void AddPolygon(MeshTopology model, IReadOnlyList<Vector2> points, float y, float height, int material)
        {
            if (!PlanarPolygon.TryTriangulate(points, out _, out string issue))
                throw new InvalidOperationException(issue);

            int first = model.vertices.Count;
            var ring = Enumerable.Range(first, points.Count).ToArray();
            foreach (Vector2 point in points)
                model.vertices.Add(new Vector3(point.x, y, point.y));
            var cap = new PolygonFace(ring, material);
            if (model.Normal(cap).y < 0f)
                Array.Reverse(ring);

            if (height <= 0f)
            {
                model.faces.Add(new PolygonFace(ring, material));
                return;
            }

            foreach (Vector2 point in points)
                model.vertices.Add(new Vector3(point.x, y + height, point.y));
            int offset = points.Count;
            model.faces.Add(new PolygonFace(ring.Reverse(), material));
            model.faces.Add(new PolygonFace(ring.Select(index => index + offset), material));
            for (int i = 0; i < ring.Length; i++)
            {
                int a = ring[i], b = ring[(i + 1) % ring.Length];
                model.faces.Add(new PolygonFace(new[] { a, b, b + offset, a + offset }, material));
            }
        }

        private static void AddProfile(MeshTopology model, IReadOnlyList<Vector2> points, float depth)
        {
            int start = model.vertices.Count;
            AddPolygon(model, points.Select(point => new Vector2(point.x, -point.y)).ToArray(), -depth / 2f, depth, 0);
            for (int i = start; i < model.vertices.Count; i++)
            {
                Vector3 point = model.vertices[i];
                model.vertices[i] = new Vector3(point.x, -point.z, point.y);
            }
        }

        private static void Pointed(MeshTopology model, Vector3 size, int segments)
        {
            var points = RegularPolygon(size.x, size.z, segments);
            foreach (Vector2 point in points)
                model.vertices.Add(new Vector3(point.x, -size.y / 2f, point.y));
            model.vertices.Add(Vector3.up * size.y / 2f);
            var ring = Enumerable.Range(0, segments).ToArray();
            model.faces.Add(new PolygonFace(ring.Reverse()));
            for (int i = 0; i < segments; i++)
                model.faces.Add(new PolygonFace(new[] { i, (i + 1) % segments, segments }));
        }

        private static void Sphere(MeshTopology model, Vector3 radius, int segments)
        {
            int rings = Mathf.Max(3, segments / 2);
            model.vertices.Add(Vector3.Scale(Vector3.up, radius));
            for (int ring = 1; ring < rings; ring++)
            {
                float latitude = ring * Mathf.PI / rings;
                foreach (Vector2 point in RegularPolygon(2f, 2f, segments))
                    model.vertices.Add(Vector3.Scale(new Vector3(point.x * Mathf.Sin(latitude), Mathf.Cos(latitude),
                        point.y * Mathf.Sin(latitude)), radius));
            }
            int bottom = model.vertices.Count;
            model.vertices.Add(Vector3.Scale(Vector3.down, radius));
            for (int side = 0; side < segments; side++)
            {
                int next = (side + 1) % segments;
                AddOutward(model, 0, next + 1, side + 1);
                for (int ring = 0; ring < rings - 2; ring++)
                {
                    int start = 1 + ring * segments;
                    AddOutward(model, start + side, start + next, start + next + segments, start + side + segments);
                }
                AddOutward(model, bottom, bottom - segments + side, bottom - segments + next);
            }
            model.smooth = true;
        }

        private static void AddOutward(MeshTopology model, params int[] ring)
        {
            var face = new PolygonFace(ring);
            if (Vector3.Dot(model.Normal(face), model.Center(face)) < 0f)
                face.vertices.Reverse();
            model.faces.Add(face);
        }

        private static void Staircase(MeshTopology model, Vector3 size, int segments)
        {
            segments = Mathf.Min(segments, 24);
            float left = -size.x / 2f;
            float bottom = -size.y / 2f;
            var outline = new List<Vector2> { new(left, bottom) };
            for (int step = 0; step < segments; step++)
            {
                float y = bottom + size.y * (step + 1) / segments;
                outline.Add(new Vector2(left + size.x * step / segments, y));
                outline.Add(new Vector2(left + size.x * (step + 1) / segments, y));
            }
            outline.Add(new Vector2(size.x / 2f, bottom));
            AddProfile(model, outline, size.z);
        }

        private static void Arch(MeshTopology model, Vector3 size, int segments)
        {
            float floor = -size.y / 2f;
            float spring = size.y * 0.02f;
            var outline = new List<Vector2> { new(size.x / 2f, floor) };
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                outline.Add(new Vector2(Mathf.Cos(angle) * size.x * 0.5f, spring + Mathf.Sin(angle) * size.y * 0.48f));
            }
            outline.Add(new Vector2(-size.x / 2f, floor));
            outline.Add(new Vector2(-size.x * 0.35f, floor));
            for (int i = segments; i >= 0; i--)
            {
                float angle = Mathf.PI * i / segments;
                outline.Add(new Vector2(Mathf.Cos(angle) * size.x * 0.35f, spring + Mathf.Sin(angle) * size.y * 0.31f));
            }
            outline.Add(new Vector2(size.x * 0.35f, floor));
            AddProfile(model, outline, size.z);
        }

        private static void Column(MeshTopology model, Vector3 size, int segments, bool tower)
        {
            float bottom = -size.y / 2f;
            float band = size.y * 0.13f;
            AddPolygon(model, RegularPolygon(size.x, size.z, segments), bottom, band, 0);
            float taper = tower ? 0.86f : 0.68f;
            AddPolygon(model, RegularPolygon(size.x * taper, size.z * taper, segments),
                bottom + band, size.y - 2f * band, 0);
            AddPolygon(model, RegularPolygon(size.x, size.z, segments), size.y / 2f - band, band, 0);
        }

        private static void Track(MeshTopology model, Vector3 size, bool ballTrack)
        {
            float bed = size.y * (ballTrack ? 0.48f : 0.65f);
            Box(model, new Vector3(0f, -(size.y - bed) / 2f, 0f), new Vector3(size.x, bed, size.z));
            float rail = size.z * 0.09f;
            float distance = (size.z - rail) / 2f;
            var railSize = new Vector3(size.x, size.y - bed, rail);
            Box(model, new Vector3(0f, bed / 2f, -distance), railSize);
            Box(model, new Vector3(0f, bed / 2f, distance), railSize);
        }

        private static void CurvedPath(MeshTopology model, Vector3 size, int segments, float arc)
        {
            var profile = new List<Vector2>();
            float angle = Mathf.Clamp(arc, 15f, 330f) * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float t = angle * (i / (float)segments - 0.5f);
                profile.Add(new Vector2(Mathf.Cos(t) * size.x / 2f, Mathf.Sin(t) * size.z / 2f));
            }
            for (int i = segments; i >= 0; i--)
                profile.Add(profile[i] * 0.67f);
            AddPolygon(model, profile, -size.y / 2f, size.y, 0);
        }
    }
}
