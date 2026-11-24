using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [Serializable]
    public sealed class PoseTrack
    {
        public Transform target;
        public Vector3 translation;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        [Range(0f, 1f)] public float phaseStart;
        [Range(0f, 1f)] public float phaseEnd = 1f;
        public float lift;
        public Vector3 swing;
        public AnimationCurve easing;
        [Tooltip("Zero preserves the legacy ease. A small value adds a soft landing overshoot to this track only.")]
        [Range(0f, 0.12f)] public float overshoot;

        [NonSerialized] internal Vector3 restPosition;
        [NonSerialized] internal Quaternion restRotation;
        [NonSerialized] internal Vector3 restScale;
    }

    public sealed class GeometryPose : MonoBehaviour
    {
        public PoseTrack[] tracks = Array.Empty<PoseTrack>();
        private bool captured;

        private void Capture()
        {
            foreach (var track in tracks)
            {
                if (track == null || !track.target)
                    continue;
                track.restPosition = track.target.localPosition;
                track.restRotation = track.target.localRotation;
                track.restScale = track.target.localScale;
            }
            captured = true;
        }

        public void Apply(float amount)
        {
            if (!captured)
                Capture();
            foreach (var track in tracks)
            {
                if (track == null || !track.target)
                    continue;
                float t = track.phaseStart <= 0f && (track.phaseEnd >= 1f || track.phaseEnd == 0f) ? amount :
                    Mathf.InverseLerp(track.phaseStart, track.phaseEnd, amount);
                float phase = Mathf.SmoothStep(0f, 1f, t);
                if (track.easing?.length > 0)
                    phase = track.easing.Evaluate(phase);
                if (track.overshoot > 0f && t > 0.62f && t < 1f)
                    phase += track.overshoot * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(0.62f, 1f, t)) *
                        Mathf.SmoothStep(0.62f, 0.78f, t);
                float arc = phase > 0f && phase < 1f ? Mathf.Sin(phase * Mathf.PI) : 0f;
                Vector3 rise = Vector3.up * (arc * track.lift);
                track.target.SetLocalPositionAndRotation(track.restPosition + track.translation * phase + rise,
                    track.restRotation * Quaternion.Euler(track.rotation * phase + track.swing * arc));
                track.target.localScale = Vector3.Scale(track.restScale, Vector3.LerpUnclamped(Vector3.one, track.scale, phase));
            }
        }
    }
}
