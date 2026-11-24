using UnityEngine;

namespace BounceBack.ArtIntegration
{
    public sealed class LiftShutterFollow : MonoBehaviour
    {
        public BounceBack.GeometryWorld.ElasticGeometry wall;

        private void LateUpdate()
        {
            if (!wall || wall.Locked)
                return;
            transform.SetPositionAndRotation(wall.transform.position, Quaternion.identity);
        }
    }
}
