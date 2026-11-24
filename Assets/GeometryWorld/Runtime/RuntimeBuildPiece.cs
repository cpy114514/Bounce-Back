using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [DisallowMultipleComponent]
    public sealed class RuntimeBuildPiece : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string pieceId;
        [SerializeField, HideInInspector] private string itemId;

        public string PieceId => pieceId;
        public string ItemId => itemId;
        private Pose placedPose;

        private void Awake() => placedPose = new Pose(transform.position, transform.rotation);

        public void SetIdentity(string id, string item)
        {
            pieceId = id;
            itemId = item;
            placedPose = new Pose(transform.position, transform.rotation);
        }

        internal BuildBlockState Capture() => new()
        {
            id = pieceId,
            itemId = itemId,
            position = placedPose.position,
            rotation = placedPose.rotation,
            removed = !gameObject.activeSelf
        };
    }
}
