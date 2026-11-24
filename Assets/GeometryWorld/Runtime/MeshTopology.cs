using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BounceBack.GeometryWorld
{
    [Serializable]
    public sealed class PolygonFace
    {
        public List<int> vertices = new();
        public int material;

        public PolygonFace(IEnumerable<int> vertices, int material = 0)
        {
            this.vertices.AddRange(vertices);
            this.material = material;
        }
    }

    public readonly struct TopologyEdge : IEquatable<TopologyEdge>
    {
        public readonly int A;
        public readonly int B;

        public TopologyEdge(int a, int b)
        {
            A = Mathf.Min(a, b);
            B = Mathf.Max(a, b);
        }

        public bool Equals(TopologyEdge other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is TopologyEdge other && Equals(other);
        public override int GetHashCode() => unchecked(A * 397 ^ B);
    }

    [Serializable]
    public sealed class MeshTopology
    {
        public List<Vector3> vertices = new();
        public List<PolygonFace> faces = new();
        public bool smooth;

        public MeshTopology Copy()
        {
            var copy = new MeshTopology { smooth = smooth };
            copy.vertices.AddRange(vertices);
            foreach (var face in faces)
                copy.faces.Add(new PolygonFace(face.vertices, face.material));
            return copy;
        }

        public Dictionary<TopologyEdge, List<int>> GetEdges()
        {
            var edges = new Dictionary<TopologyEdge, List<int>>();
            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                var ring = faces[faceIndex].vertices;
                for (int i = 0; i < ring.Count; i++)
                {
                    var edge = new TopologyEdge(ring[i], ring[(i + 1) % ring.Count]);
                    if (!edges.TryGetValue(edge, out var attached))
                        edges.Add(edge, attached = new List<int>(2));
                    attached.Add(faceIndex);
                }
            }
            return edges;
        }

        public Vector3 Center(PolygonFace face)
        {
            Vector3 center = Vector3.zero;
            foreach (int i in face.vertices)
                center += vertices[i];
            return center / face.vertices.Count;
        }

        public Vector3 Normal(PolygonFace face)
        {
            Vector3 origin = vertices[face.vertices[0]];
            Vector3 normal = Vector3.zero;
            for (int i = 1; i < face.vertices.Count - 1; i++)
                normal += Vector3.Cross(vertices[face.vertices[i]] - origin, vertices[face.vertices[i + 1]] - origin);
            return normal.normalized;
        }

        public void WriteMesh(Mesh mesh, int materialCount)
        {
            if (faces.Count == 0)
                throw new InvalidOperationException("The model needs at least one face.");

            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var smoothNormals = new Vector3[vertices.Count];
            var triangles = new List<int>[Mathf.Max(1, materialCount)];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new List<int>();

            if (smooth)
            {
                foreach (var face in faces)
                {
                    Vector3 normal = Normal(face);
                    foreach (int index in face.vertices)
                        smoothNormals[index] += normal;
                }
            }

            for (int index = 0; index < faces.Count; index++)
            {
                var face = faces[index];
                if (face.vertices.Count < 3 || face.material < 0 || face.material >= triangles.Length)
                    throw new InvalidOperationException($"Face {index + 1} has an invalid loop or material.");
                Vector3 normal = Normal(face);
                var polygon = new List<Vector2>(face.vertices.Count);
                int start = points.Count;
                int axis = PlanarPolygon.DominantAxis(normal);
                foreach (int vertex in face.vertices)
                {
                    Vector3 point = vertices[vertex];
                    Vector2 projected = PlanarPolygon.Project(point, axis);
                    polygon.Add(projected);
                    points.Add(point);
                    normals.Add(smooth ? smoothNormals[vertex].normalized : normal);
                    uv.Add(projected);
                }
                if (!PlanarPolygon.TryTriangulate(polygon, out var localTriangles, out string reason))
                    throw new InvalidOperationException($"Face {index + 1} · {reason}");
                foreach (int triangleIndex in localTriangles)
                    triangles[face.material].Add(start + triangleIndex);
            }

            mesh.Clear();
            mesh.indexFormat = points.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
        }
    }

    public static class PlanarPolygon
    {
        private const double Epsilon = 1e-9;

        public static int DominantAxis(Vector3 normal)
        {
            var abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (abs.x >= abs.y && abs.x >= abs.z)
                return 0;
            return abs.y >= abs.z ? 1 : 2;
        }

        public static Vector2 Project(Vector3 point, int axis) => axis switch
        {
            0 => new Vector2(point.y, point.z),
            1 => new Vector2(point.x, point.z),
            _ => new Vector2(point.x, point.y)
        };

        public static bool TryTriangulate(IReadOnlyList<Vector2> points, out List<int> triangles, out string issue)
        {
            triangles = new List<int>(Mathf.Max(0, points.Count - 2) * 3);
            issue = "";
            if (points.Count < 3)
            {
                issue = "A polygon needs at least three vertices.";
                return false;
            }

            double area = 0;
            var remaining = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                if (!float.IsFinite(points[i].x) || !float.IsFinite(points[i].y) ||
                    (points[i] - points[next]).sqrMagnitude <= Epsilon)
                {
                    issue = "The polygon has a coincident or invalid boundary vertex.";
                    return false;
                }
                for (int j = i + 2; j < points.Count; j++)
                {
                    int following = (j + 1) % points.Count;
                    if (following != i && Intersects(points[i], points[next], points[j], points[following]))
                    {
                        issue = "The polygon boundary crosses or touches itself.";
                        return false;
                    }
                }
                area += Cross(points[i], points[next], points[0]);
                remaining.Add(i);
            }
            if (Math.Abs(area) <= Epsilon)
            {
                issue = "The polygon has no area.";
                return false;
            }
            double winding = Math.Sign(area);

            while (remaining.Count > 3)
            {
                bool removed = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    double turn = Cross(points[a], points[b], points[c]) * winding;
                    if (Math.Abs(turn) <= Epsilon && OnSegment(points[a], points[c], points[b]))
                    {
                        remaining.RemoveAt(i);
                        removed = true;
                        break;
                    }
                    if (turn <= Epsilon)
                        continue;

                    bool contains = false;
                    foreach (int other in remaining)
                    {
                        if (other == a || other == b || other == c)
                            continue;
                        Vector2 p = points[other];
                        if (Cross(points[a], points[b], p) * winding >= -Epsilon &&
                            Cross(points[b], points[c], p) * winding >= -Epsilon &&
                            Cross(points[c], points[a], p) * winding >= -Epsilon)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (contains)
                        continue;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    remaining.RemoveAt(i);
                    removed = true;
                    break;
                }
                if (!removed)
                {
                    issue = "The polygon could not be triangulated.";
                    return false;
                }
            }

            if (Math.Abs(Cross(points[remaining[0]], points[remaining[1]], points[remaining[2]])) > Epsilon)
                triangles.AddRange(remaining);
            if (triangles.Count > 0)
                return true;
            issue = "The polygon has no nondegenerate triangles.";
            return false;
        }

        private static double Cross(Vector2 a, Vector2 b, Vector2 p) =>
            ((double)b.x - a.x) * ((double)p.y - a.y) - ((double)b.y - a.y) * ((double)p.x - a.x);

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
            p.x >= Mathf.Min(a.x, b.x) - Epsilon && p.x <= Mathf.Max(a.x, b.x) + Epsilon &&
            p.y >= Mathf.Min(a.y, b.y) - Epsilon && p.y <= Mathf.Max(a.y, b.y) + Epsilon;

        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double first = Cross(a, b, c), second = Cross(a, b, d);
            double third = Cross(c, d, a), fourth = Cross(c, d, b);
            return (first * second < -Epsilon * Epsilon && third * fourth < -Epsilon * Epsilon) ||
                (Math.Abs(first) <= Epsilon && OnSegment(a, b, c)) ||
                (Math.Abs(second) <= Epsilon && OnSegment(a, b, d)) ||
                (Math.Abs(third) <= Epsilon && OnSegment(c, d, a)) ||
                (Math.Abs(fourth) <= Epsilon && OnSegment(c, d, b));
        }
    }
}
