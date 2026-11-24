using UnityEngine;

namespace BounceBack.ArtIntegration
{
    /// <summary>Visual orbit only. No collider and no gameplay effect.</summary>
    public sealed class SeagullOrbit : MonoBehaviour
    {
        public float radius = 3.2f;
        public float height = 2.6f;
        public float degreesPerSecond = 14f;
        public float phase;

        void Update()
        {
            float angle = (phase + Time.time * degreesPerSecond) * Mathf.Deg2Rad;
            transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            if (tangent.sqrMagnitude > 0.0001f)
                transform.localRotation = Quaternion.LookRotation(-tangent, Vector3.up);
        }
    }
}
