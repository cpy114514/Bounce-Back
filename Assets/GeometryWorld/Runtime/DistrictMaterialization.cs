using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class DistrictMaterialization : MonoBehaviour
    {
        public WorldEntrance entrance;
        public Transform[] layers = Array.Empty<Transform>();
        public GeometryPose unfolding;
        public Transform carrierAnchor;
        public Light revealLight;
        [Min(0.2f)] public float duration = 1.65f;

        public bool IsRevealing { get; private set; }
        public event Action<int> LayerFormed;
        public event Action<int> Finished;

        private Vector3[] restScale;
        private float elapsed;
        private int announcedLayers;
        private bool ready;

        internal void Show(CampaignProgress progress, bool locked = false)
        {
            if (restScale == null)
            {
                restScale = new Vector3[layers.Length];
                for (int layer = 0; layer < layers.Length; layer++)
                    restScale[layer] = layers[layer].localScale;
            }
            ready = progress.IsMaterialized(entrance.levelIndex);
            IsRevealing = false;
            elapsed = 0f;
            entrance.ShowProgress(progress, true);
            bool visible = !locked && progress.IsUnlocked(entrance.levelIndex) && ready;
            if (entrance.solid)
                entrance.solid.gameObject.SetActive(visible);
            if (entrance.wire)
                entrance.wire.SetActive(!visible);
            ApplyLayers(visible ? 1f : 0f);
        }

        internal bool Approach(CampaignProgress progress)
        {
            if (ready || IsRevealing || !progress.IsUnlocked(entrance.levelIndex))
                return false;
            IsRevealing = true;
            elapsed = 0f;
            announcedLayers = 0;
            if (entrance.solid)
                entrance.solid.gameObject.SetActive(true);
            return true;
        }

        internal void Finish()
        {
            if (!IsRevealing)
                return;
            IsRevealing = false;
            ready = true;
            ApplyLayers(1f);
            if (entrance.wire)
                entrance.wire.SetActive(false);
            Finished?.Invoke(entrance.levelIndex);
        }

        private void Update()
        {
            if (!IsRevealing)
                return;
            elapsed += Time.unscaledDeltaTime;
            float time = Mathf.Clamp01(elapsed / (CampaignSettings.ReducedMotion ? 0.25f : Mathf.Max(0.2f, duration)));
            ApplyLayers(time);
            while (announcedLayers < layers.Length && time >= (announcedLayers + 0.75f) / layers.Length)
            {
                int layer = announcedLayers++;
                LayerFormed?.Invoke(layer);
            }
            if (time >= 1f)
                Finish();
        }

        private void ApplyLayers(float amount)
        {
            for (int layer = 0; layer < layers.Length; layer++)
            {
                float scale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount * layers.Length - layer));
                layers[layer].gameObject.SetActive(scale > 0f);
                layers[layer].localScale = Vector3.Scale(restScale[layer], new Vector3(1f, Mathf.Max(0.001f, scale), 1f));
            }
            if (revealLight)
                revealLight.intensity = IsRevealing ? Mathf.Sin(amount * Mathf.PI) * 4f : 0f;
        }
    }
}
