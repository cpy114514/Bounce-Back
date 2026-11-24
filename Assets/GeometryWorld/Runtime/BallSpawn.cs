using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class BallSpawn : MonoBehaviour
    {
        public BallBody prefab;
        public Material material;
        [Min(0.05f)] public float diameter = 0.64f;
        [Min(0.01f)] public float mass = 1f;
        [Min(0f)] public float launchSpeed = 3f;
        public bool autoRelease;

        public Vector3 LocalStartPosition =>
            TryGetComponent(out PistonLauncher piston) && piston.HasVisuals ? piston.BallRestOffset : Vector3.zero;

        public Vector3 StartPosition => transform.TransformPoint(LocalStartPosition);

        internal BallBody Create(GeometryLevel level)
        {
            BallBody ball;
            if (prefab)
            {
                ball = Instantiate(prefab, StartPosition, transform.rotation, level.transform);
            }
            else
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(level.transform, true);
                sphere.transform.SetPositionAndRotation(StartPosition, transform.rotation);
                ball = sphere.AddComponent<BallBody>();
                if (material)
                    sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            ball.name = "Puzzle Ball";
            ball.transform.localScale = Vector3.one * diameter;
            ball.Initialize(level, mass);
            return ball;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.95f, 0.8f);
            Gizmos.DrawWireSphere(StartPosition, diameter * 0.5f);
            Gizmos.DrawRay(StartPosition, transform.forward * Mathf.Max(0.5f, launchSpeed * 0.35f));
        }
    }
}
