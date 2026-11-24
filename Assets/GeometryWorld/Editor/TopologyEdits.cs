using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class TopologyEdits
    {
        internal static void Move(MeshTopology model, IEnumerable<int> selection, Vector3 delta, bool mirrorX)
        {
            var selected = new HashSet<int>(selection);
            if (mirrorX)
            {
                foreach (int index in selected.ToArray())
                {
                    Vector3 point = model.vertices[index];
                    if (Mathf.Abs(point.x) < 0.0001f)
                    {
                        model.vertices[index] += new Vector3(0f, delta.y, delta.z);
                        selected.Remove(index);
                        continue;
                    }

                    int opposite = model.vertices.FindIndex(other => (other - ReflectX(point)).sqrMagnitude < 1e-8f);
                    if (opposite >= 0 && !selected.Contains(opposite))
                        model.vertices[opposite] += ReflectX(delta);
                }
            }
            foreach (int index in selected)
                model.vertices[index] += delta;
        }

        internal static void ScaleFace(MeshTopology model, int faceIndex, float factor)
        {
            var face = model.faces[faceIndex];
            Vector3 center = model.Center(face);
            foreach (int index in face.vertices)
                model.vertices[index] = center + (model.vertices[index] - center) * factor;
        }

        internal static void ExtrudeFace(MeshTopology model, int faceIndex, float distance)
        {
            if (distance <= 0f)
                throw new InvalidOperationException("Extrusion distance must be positive.");
            var face = model.faces[faceIndex];
            var direction = model.Normal(face) * distance;
            ReplaceFaceRing(model, faceIndex, point => point + direction);
        }

        internal static void InsetFace(MeshTopology model, int faceIndex, float fraction)
        {
            var face = model.faces[faceIndex];
            Vector3 normal = model.Normal(face);
            RequireConvexFace(model, face, normal);
            Vector3 center = model.Center(face);
            ReplaceFaceRing(model, faceIndex, point => Vector3.Lerp(point, center, Mathf.Clamp(fraction, 0.01f, 0.95f)));
        }

        private static void ReplaceFaceRing(MeshTopology model, int faceIndex, Func<Vector3, Vector3> change)
        {
            var face = model.faces[faceIndex];
            var outer = face.vertices;
            var inner = new int[outer.Count];
            for (int i = 0; i < outer.Count; i++)
            {
                inner[i] = model.vertices.Count;
                model.vertices.Add(change(model.vertices[outer[i]]));
            }
            model.faces[faceIndex] = new PolygonFace(inner, face.material);
            for (int i = 0; i < outer.Count; i++)
            {
                int j = (i + 1) % outer.Count;
                model.faces.Add(new PolygonFace(new[] { outer[i], outer[j], inner[j], inner[i] }, face.material));
            }
        }

        internal static int SplitEdge(MeshTopology model, TopologyEdge edge)
        {
            int index = model.vertices.Count;
            model.vertices.Add((model.vertices[edge.A] + model.vertices[edge.B]) / 2f);
            foreach (var face in model.faces)
            {
                int start = DirectedIndex(face, edge);
                if (start >= 0)
                    face.vertices.Insert(start + 1, index);
            }
            return index;
        }

        internal static TopologyEdge ExtrudeBoundary(MeshTopology model, TopologyEdge edge, float distance)
        {
            var faces = model.GetEdges()[edge];
            if (faces.Count != 1 || distance <= 0)
                throw new InvalidOperationException("Select an open boundary edge and use a positive distance.");
            var face = model.faces[faces[0]];
            int i = DirectedIndex(face, edge);
            int a = face.vertices[i], b = face.vertices[(i + 1) % face.vertices.Count];
            Vector3 offset = Vector3.Cross(model.vertices[b] - model.vertices[a], model.Normal(face)).normalized * distance;
            int first = model.vertices.Count;
            model.vertices.Add(model.vertices[a] + offset);
            model.vertices.Add(model.vertices[b] + offset);
            model.faces.Add(new PolygonFace(new[] { b, a, first, first + 1 }, face.material));
            return new TopologyEdge(first, first + 1);
        }

        internal static void Bevel(MeshTopology model, TopologyEdge edge, float width, int material)
        {
            var edges = model.GetEdges();
            if (width <= 0f || edges[edge].Count != 2 || edges.Values.Any(faces => faces.Count != 2))
                throw new InvalidOperationException("Bevel needs a shared edge on a closed convex model.");

            var connected = edges[edge];
            Vector3 normal = (model.Normal(model.faces[connected[0]]) + model.Normal(model.faces[connected[1]])).normalized;
            if (normal == Vector3.zero)
                throw new InvalidOperationException("The edge between these faces has no usable bevel plane.");

            foreach (var face in model.faces)
            {
                Vector3 outward = model.Normal(face);
                Vector3 origin = model.vertices[face.vertices[0]];
                RequireConvexFace(model, face, outward);
                if (model.vertices.Any(point => Vector3.Dot(outward, point - origin) > 0.0001f))
                    throw new InvalidOperationException("Bevel is limited to a convex model with outward-facing faces.");
            }

            float height = Vector3.Dot(normal, (model.vertices[edge.A] + model.vertices[edge.B]) / 2f) - width;
            for (int i = 0; i < model.vertices.Count; i++)
            {
                if (i != edge.A && i != edge.B && Vector3.Dot(normal, model.vertices[i]) >= height - 0.0001f)
                    throw new InvalidOperationException("This bevel would cut another vertex. Reduce the width or select a single sharp edge.");
            }

            var intersections = new Dictionary<TopologyEdge, int>();
            foreach (var face in model.faces)
            {
                var clipped = new List<int>();
                var ring = face.vertices;
                for (int i = 0; i < ring.Count; i++)
                {
                    int a = ring[i], b = ring[(i + 1) % ring.Count];
                    Vector3 from = model.vertices[a], to = model.vertices[b];
                    float first = Vector3.Dot(normal, from) - height;
                    float second = Vector3.Dot(normal, to) - height;
                    if (first < 0f)
                        clipped.Add(a);
                    if ((first < 0f) == (second < 0f))
                        continue;
                    var cut = new TopologyEdge(a, b);
                    if (!intersections.TryGetValue(cut, out int index))
                    {
                        index = model.vertices.Count;
                        intersections.Add(cut, index);
                        model.vertices.Add(Vector3.LerpUnclamped(from, to, first / (first - second)));
                    }
                    clipped.Add(index);
                }
                face.vertices = clipped;
            }

            var points = intersections.Values.ToArray();
            if (points.Length < 3)
                throw new InvalidOperationException("The bevel plane did not intersect enough edges.");
            Vector3 center = points.Select(i => model.vertices[i]).Aggregate(Vector3.zero, (sum, point) => sum + point) / points.Length;
            Vector3 x = (model.vertices[points[0]] - center).normalized;
            Vector3 y = Vector3.Cross(normal, x);
            model.faces.Add(new PolygonFace(points.OrderBy(i =>
            {
                Vector3 local = model.vertices[i] - center;
                return Mathf.Atan2(Vector3.Dot(local, y), Vector3.Dot(local, x));
            }), material));
            Compact(model);
        }

        private static void RequireConvexFace(MeshTopology model, PolygonFace face, Vector3 normal)
        {
            Vector3 origin = model.vertices[face.vertices[0]];
            var ring = face.vertices;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 a = model.vertices[ring[i]], b = model.vertices[ring[(i + 1) % ring.Count]];
                Vector3 c = model.vertices[ring[(i + 2) % ring.Count]];
                if (Mathf.Abs(Vector3.Dot(normal, a - origin)) > 0.0001f ||
                    Vector3.Dot(Vector3.Cross(b - a, c - b), normal) < -0.000001f)
                    throw new InvalidOperationException("This operation needs a planar, convex face.");
            }
        }

        internal static void Merge(MeshTopology model, IReadOnlyCollection<int> selection)
        {
            if (selection.Count < 2)
                throw new InvalidOperationException("Shift-select two or more vertices to merge.");

            int keep = selection.Min();
            model.vertices[keep] = selection.Select(i => model.vertices[i])
                .Aggregate(Vector3.zero, (sum, point) => sum + point) / selection.Count;
            foreach (var face in model.faces)
            {
                for (int i = 0; i < face.vertices.Count; i++)
                    if (selection.Contains(face.vertices[i]))
                        face.vertices[i] = keep;
                for (int i = face.vertices.Count - 1; i > 0; i--)
                    if (face.vertices[i] == face.vertices[i - 1])
                        face.vertices.RemoveAt(i);
                if (face.vertices.Count > 1 && face.vertices[0] == face.vertices[face.vertices.Count - 1])
                    face.vertices.RemoveAt(face.vertices.Count - 1);
            }
            model.faces.RemoveAll(face => face.vertices.Count < 3);
            if (model.GetEdges().Values.Any(faces => faces.Count > 2))
                throw new InvalidOperationException("This merge would create a non-manifold edge.");
            Compact(model);
        }

        internal static void DeleteVertices(MeshTopology model, IReadOnlyCollection<int> selection)
        {
            foreach (var face in model.faces)
            {
                face.vertices.RemoveAll(index => selection.Contains(index));
                if (face.vertices.Count < 3)
                    throw new InvalidOperationException("Delete the attached faces first or merge the vertices.");
            }
            Compact(model);
        }

        internal static void DeleteFace(MeshTopology model, int index)
        {
            model.faces.RemoveAt(index);
            Compact(model);
        }

        internal static void Mirror(MeshTopology model)
        {
            for (int i = 0; i < model.vertices.Count; i++)
                model.vertices[i] = ReflectX(model.vertices[i]);
            foreach (var face in model.faces)
                face.vertices.Reverse();
        }

        private static Vector3 ReflectX(Vector3 point) => new(-point.x, point.y, point.z);

        private static int DirectedIndex(PolygonFace face, TopologyEdge edge)
        {
            var ring = face.vertices;
            for (int i = 0; i < ring.Count; i++)
                if (new TopologyEdge(ring[i], ring[(i + 1) % ring.Count]).Equals(edge))
                    return i;
            return -1;
        }

        private static void Compact(MeshTopology model)
        {
            var used = new HashSet<int>(model.faces.SelectMany(face => face.vertices));
            var map = new int[model.vertices.Count];
            var positions = new List<Vector3>(used.Count);
            for (int i = 0; i < model.vertices.Count; i++)
            {
                if (!used.Contains(i))
                    continue;
                map[i] = positions.Count;
                positions.Add(model.vertices[i]);
            }
            foreach (var face in model.faces)
                for (int i = 0; i < face.vertices.Count; i++)
                    face.vertices[i] = map[face.vertices[i]];
            model.vertices = positions;
        }
    }
}
