using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [DisallowMultipleComponent]
    public sealed class GeometryObject : MonoBehaviour
    {
        public GeometryMeshAsset model;
        public MeshFilter surface;
        public MeshRenderer surfaceRenderer;
        public Collider gameplayCollider;
        public bool decorative;
        public bool editLocked;
    }
}
