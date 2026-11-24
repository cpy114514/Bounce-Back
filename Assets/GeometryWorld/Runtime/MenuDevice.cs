using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum MenuAction { Continue, LevelSelect, Settings, Quit, Start }

    public sealed class MenuDevice : MonoBehaviour
    {
        public MenuAction action;
        public GeometryPose unfolding;
        public Renderer glowRenderer;
        public Color glowColor = new(1f, 0.7f, 0.25f);

        public string Label => action == MenuAction.LevelSelect ? "Level Select" : action.ToString();
        private float amount;
        private MaterialPropertyBlock properties;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        internal void SetFocus(bool focused, bool reducedMotion)
        {
            float target = focused ? 1f : 0f;
            amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 5f);
            if (unfolding)
                unfolding.Apply(reducedMotion ? 0f : Mathf.SmoothStep(0f, 1f, amount) + Mathf.Sin(amount * Mathf.PI) * 0.14f);
            if (!glowRenderer)
                return;
            properties ??= new MaterialPropertyBlock();
            glowRenderer.GetPropertyBlock(properties);
            properties.SetColor(Emission, glowColor * amount * 0.75f);
            glowRenderer.SetPropertyBlock(properties);
        }
    }
}
