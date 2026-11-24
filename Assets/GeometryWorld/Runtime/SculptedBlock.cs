using System;
using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class SculptedBlock : MonoBehaviour
    {
        public const int Resolution = 4;
        internal const float CellSize = 1f / Resolution;
        private const int FullLayer = (1 << (Resolution * Resolution)) - 1;
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };

        [SerializeField, HideInInspector] private string blockId;
        [SerializeField, HideInInspector] private int[] layers = { FullLayer, FullLayer, FullLayer, FullLayer };
        private Mesh mesh;

        public string BlockId => blockId;
        internal bool Empty => BuildBlockState.IsEmpty(layers);
        internal void SetId(string id) => blockId = id;

        public void ApplyCells(string id, int[] cells)
        {
            blockId = id;
            layers = (int[])cells.Clone();
            Rebuild();
        }

        internal BuildBlockState Capture() => new()
        {
            id = blockId,
            position = transform.position,
            rotation = transform.rotation,
            layers = (int[])layers.Clone()
        };

        internal bool Contains(Vector3Int cell) => InBounds(cell) && (layers[cell.y] & Mask(cell)) != 0;

        internal void SetCell(Vector3Int cell, bool filled)
        {
            if (!InBounds(cell) || Contains(cell) == filled)
                return;
            if (filled)
                layers[cell.y] |= Mask(cell);
            else
                layers[cell.y] &= ~Mask(cell);
            Rebuild();
        }

        private static bool InBounds(Vector3Int c) => c.x >= 0 && c.x < Resolution &&
            c.y >= 0 && c.y < Resolution && c.z >= 0 && c.z < Resolution;

        private static int Mask(Vector3Int c) => 1 << (c.z * Resolution + c.x);

        internal static Vector3 CellCenter(Vector3Int c) => ((Vector3)c + Vector3.one * 0.5f) * CellSize - Vector3.one * 0.5f;

        internal bool TryCell(Vector3 hit, Vector3 normal, bool outward, out Vector3Int cell)
        {
            var local = transform.InverseTransformPoint(hit) +
                transform.InverseTransformDirection(normal) * (outward ? 0.002f : -0.002f);
            cell = Vector3Int.FloorToInt((local + Vector3.one * 0.5f) * Resolution);
            return InBounds(cell) && Contains(cell) != outward;
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (!mesh)
                return;
            GetComponent<MeshCollider>().sharedMesh = null;
            GetComponent<MeshFilter>().sharedMesh = null;
            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);
            mesh = null;
        }

        public void Rebuild()
        {
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.enabled = !Empty;
            renderer.enabled = !Empty;
            if (Empty)
                return;
            if (!renderer.sharedMaterial)
                renderer.sharedMaterial = Resources.Load<Material>("BounceBack/Build Block");
            if (!mesh)
                mesh = new Mesh { name = "Sculpted Block", hideFlags = HideFlags.DontSave };

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int y = 0; y < Resolution; y++)
            for (int z = 0; z < Resolution; z++)
            for (int x = 0; x < Resolution; x++)
            {
                var cell = new Vector3Int(x, y, z);
                if (!Contains(cell))
                    continue;
                foreach (var direction in Directions)
                    if (!Contains(cell + direction))
                        AddFace(CellCenter(cell), direction, vertices, normals, triangles);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            filter.sharedMesh = mesh;
            collider.sharedMesh = mesh;
        }

        private static void AddFace(Vector3 center, Vector3 normal, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            float half = CellSize * 0.5f;
            Vector3 u = Vector3.Cross(Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up, normal) * half;
            Vector3 v = Vector3.Cross(normal, u);
            center += normal * half;
            int start = vertices.Count;
            vertices.Add(center - u - v);
            vertices.Add(center + u - v);
            vertices.Add(center + u + v);
            vertices.Add(center - u + v);
            for (int i = 0; i < 4; i++)
                normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }

    [Serializable]
    public struct BuildBlockState
    {
        public string id;
        public string itemId;
        public Vector3 position;
        public Quaternion rotation;
        public int[] layers;
        public bool removed;

        public bool IsRemoved => removed || layers != null && IsEmpty(layers);

        internal static bool IsEmpty(int[] cells)
        {
            foreach (int layer in cells)
                if (layer != 0)
                    return false;
            return true;
        }
    }
}
