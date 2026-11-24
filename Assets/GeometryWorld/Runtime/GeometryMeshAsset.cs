using BounceBack.Geometry;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class GeometryMeshAsset : ScriptableObject
    {
        [HideInInspector] public MeshTopology topology = new();
        [HideInInspector] public Mesh mesh;
        [HideInInspector] public Material[] materials;
        [HideInInspector] public GeometryDrawing sourceDrawing;
    }
}
