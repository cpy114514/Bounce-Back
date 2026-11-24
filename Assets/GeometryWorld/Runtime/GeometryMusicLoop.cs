using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace BounceBack.GeometryWorld
{
    [Serializable]
    public sealed class MusicMarker
    {
        public string section;
        [Min(0f)] public float time;
        [Range(0f, 1f)] public float intensity = 1f;
        public UnityEvent onReached;
    }

    public sealed class GeometryMusicLoop : MonoBehaviour
    {
        [Range(30f, 60f)] public float duration = 40f;
        [Min(0f)] public float beatsPerMinute;
        public AudioSource[] layers = Array.Empty<AudioSource>();
        public PlayableDirector timeline;
        public MusicMarker[] markers = Array.Empty<MusicMarker>();
        public GeometryPose visualMotion;
        public AnimationCurve loopEnvelope;
        public string Section { get; private set; }
        public bool ReduceMotion { get; set; }
        public event Action<int> BeatReached;

        private float clock;
        private float previous = -0.001f;
        private float pulseAge = float.PositiveInfinity;
        private float strength;
        private bool audioActive;
        private AudioSource clockSource;
        private float markerTimeScale = 1f;
        private bool usesDefaultCityTheme;
        private float[] layerVolumes;

        internal void UseDefaultCityTheme()
        {
            if (timeline || layers.Length == 0 || !layers[0] || Array.Exists(layers, layer => layer && layer.clip))
                return;

            var theme = Resources.Load<AudioClip>("BounceBack/Audio/Curious Motion - City Loop");
            if (!theme)
                return;

            markerTimeScale = theme.length / Mathf.Max(1f, duration);
            duration = theme.length;
            layers[0].clip = theme;
            layers[0].volume = 0.46f;
            usesDefaultCityTheme = true;
        }

        public void StartAudio(int audibleLayers)
        {
            if (audioActive)
                return;
            if (usesDefaultCityTheme)
            {
                clock = 0f;
                previous = -0.001f;
            }
            audioActive = true;
            for (int i = 0; i < layers.Length; i++)
            {
                AudioSource source = layers[i];
                if (!source || !source.clip || i >= audibleLayers)
                    continue;
                layerVolumes ??= Array.ConvertAll(layers, layer => layer ? layer.volume : 0f);
                source.volume = layerVolumes[i] * CampaignSettings.MusicVolume;
                source.loop = true;
                source.time = clock % source.clip.length;
                source.Play();
                if (!clockSource && Mathf.Abs(source.clip.length - duration) < 0.05f)
                    clockSource = source;
            }
        }

        public void Pulse(float intensity)
        {
            strength = Mathf.Clamp01(intensity);
            pulseAge = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float period = Mathf.Max(1f, duration);
            if (timeline && timeline.state == PlayState.Playing)
                clock = (float)(timeline.time % period);
            else if (clockSource && clockSource.isPlaying)
                clock = clockSource.timeSamples / (float)clockSource.clip.frequency;
            else
                clock = Mathf.Repeat(clock + dt, period);
            float firstTime = previous;
            if (clock < previous)
            {
                Dispatch(previous, period);
                firstTime = -0.001f;
            }
            Dispatch(firstTime, clock);
            previous = clock;
            if (audioActive && layerVolumes != null)
            {
                float volume = CampaignSettings.MusicVolume;
                for (int i = 0; i < layers.Length; i++)
                    if (layers[i] && layers[i].isPlaying)
                        layers[i].volume = layerVolumes[i] * volume;
            }
            if (visualMotion)
            {
                float beat = float.IsPositiveInfinity(pulseAge) ? 0f : Mathf.Sin(pulseAge * 12f) * Mathf.Exp(-pulseAge * 5f) * strength;
                float phase = clock / period;
                float envelope = loopEnvelope?.length > 0 ? loopEnvelope.Evaluate(phase) : 0.24f * (1f - Mathf.Cos(phase * Mathf.PI * 2f));
                visualMotion.Apply(ReduceMotion || CampaignSettings.ReducedMotion ? 0f : envelope + beat * 0.3f);
            }
            pulseAge += dt;
        }

        private void Dispatch(float start, float end)
        {
            if (beatsPerMinute > 0f && BeatReached != null)
            {
                float rate = Mathf.Min(480f, beatsPerMinute) / 60f;
                int first = Mathf.Max(0, Mathf.FloorToInt(start * rate) + 1);
                int last = Mathf.Min(Mathf.FloorToInt(end * rate), Mathf.CeilToInt(duration * rate) - 1);
                for (int beat = first; beat <= last; beat++)
                    BeatReached(beat);
            }
            foreach (var marker in markers)
            {
                if (marker == null)
                    continue;
                float time = marker.time * markerTimeScale;
                if (time <= start || time > end)
                    continue;
                if (!string.IsNullOrWhiteSpace(marker.section))
                    Section = marker.section;
                Pulse(marker.intensity);
                marker.onReached?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (!audioActive)
                return;
            for (int i = 0; i < layers.Length; i++)
            {
                if (!layers[i])
                    continue;
                if (layers[i].isPlaying)
                    layers[i].Stop();
                if (layerVolumes != null && i < layerVolumes.Length)
                    layers[i].volume = layerVolumes[i];
            }
            layerVolumes = null;
            clockSource = null;
            audioActive = false;
        }
    }
}
