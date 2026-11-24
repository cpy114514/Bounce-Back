using UnityEngine;

namespace BounceBack.StylizedWater
{
    /// <summary>Optional per-renderer elastic pool feedback; no physics dependency.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class WaterRipple : MonoBehaviour
    {
        static readonly int ChargeId = Shader.PropertyToID("_Charge");
        static readonly int ImpactId = Shader.PropertyToID("_ImpactPos");

        [SerializeField] Renderer waterRenderer;
        [SerializeField, Range(0f, 1f)] float charge;
        [SerializeField] Vector3 impactPosition = new(-1.4f, 0.45f, 2.05f);
        MaterialPropertyBlock properties;

        public float Charge
        {
            get => charge;
            set { charge = Mathf.Clamp01(value); Apply(); }
        }

        public Vector3 ImpactPosition
        {
            get => impactPosition;
            set { impactPosition = value; Apply(); }
        }

        void Awake() { if (!waterRenderer) waterRenderer = GetComponentInChildren<Renderer>(); }
        void OnEnable() { Awake(); Apply(); }
        void OnValidate() { charge = Mathf.Clamp01(charge); Awake(); Apply(); }

        public void SetCharge(float value, Vector3 worldImpactPosition)
        {
            charge = Mathf.Clamp01(value);
            impactPosition = worldImpactPosition;
            Apply();
        }

        public void Clear() { Charge = 0f; }

        void Apply()
        {
            if (!waterRenderer) return;
            properties ??= new MaterialPropertyBlock();
            waterRenderer.GetPropertyBlock(properties);
            properties.SetFloat(ChargeId, charge);
            properties.SetVector(ImpactId, impactPosition);
            waterRenderer.SetPropertyBlock(properties);
        }

        void OnDisable()
        {
            if (!waterRenderer) return;
            waterRenderer.GetPropertyBlock(properties ??= new MaterialPropertyBlock());
            properties.SetFloat(ChargeId, 0f);
            waterRenderer.SetPropertyBlock(properties);
        }
    }
}
