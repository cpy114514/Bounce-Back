using System;
using System.Collections;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public static class ArchitectureMotion
    {
        public static IEnumerator Play(float start, float end, float seconds, Action<float> apply)
        {
            float duration = Mathf.Max(0.01f, CampaignSettings.ReducedMotion ? Mathf.Min(seconds, 0.3f) : seconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                apply(Mathf.Lerp(start, end, elapsed / duration));
                yield return null;
            }
            apply(end);
        }
    }
}
