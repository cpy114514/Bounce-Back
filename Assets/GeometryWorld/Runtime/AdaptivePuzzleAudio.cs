using System;
using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum Cue { Bounce, Drag, Rotation, Connection, Goal, Custom, Slime, Retry, Release, SlimeCompression,
        PressurePlate, ArchitectureLayer, RoadBlock, Unlock, TransitionEnd }

    [Serializable]
    public sealed class AudioCue
    {
        public Cue kind;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Min(0.02f)] public float throttle = 0.12f;
    }

    public sealed class AdaptivePuzzleAudio : MonoBehaviour
    {
        public GeometryLevel level;
        public AudioClip ambient;
        public AudioClip activityMusic;
        public AudioClip rolling;
        public List<AudioCue> sounds = new();
        public bool courtyardTones;
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f)] public float rollingVolume = 0.32f;
        [Min(0.1f)] public float fullSpeed = 5f;
        [Min(0.05f)] public float fadeRate = 2f;

        private AudioSource calm, active, roll, oneShot;
        private readonly Dictionary<Cue, float> lastPlayed = new();
        private const string CourtyardTheme = "BounceBack/Audio/Floating Courtyard";
        private AudioClip pressurePlateChime, districtRevealChime;
        private AudioClip[] poolNotes;
        private AudioClip resolvedChord;
        private float quietUntil;
        private float movement;
        private bool activated;
        private bool musicSuppressed;

        private void Awake()
        {
            if (!level)
                level = GetComponentInParent<GeometryLevel>();
            calm = CreateVoice(ResolveAmbient());
            active = CreateVoice(activityMusic);
            roll = CreateVoice(rolling);
            oneShot = CreateVoice(null);
            oneShot.loop = false;
            oneShot.volume = 1f;
            if (level && level.audioDirector == this && gameObject.scene.name == CampaignScenes.SystemsScene)
            {
                pressurePlateChime = Resources.Load<AudioClip>("BounceBack/Audio/Pressure Plate Chime");
                districtRevealChime = Resources.Load<AudioClip>("BounceBack/Audio/District Reveal Chime");
            }
            if (courtyardTones)
            {
                int[] scale = { 60, 62, 64, 67, 69, 72 };
                poolNotes = Array.ConvertAll(scale, note => Tone($"Pool note {note}", 0.35f, note));
                if (!pressurePlateChime)
                    resolvedChord = Tone("Courtyard resolution", 0.83f, 60, 64, 67);
            }
        }

        private AudioClip ResolveAmbient()
        {
            if (ambient || !level || level.audioDirector != this)
                return ambient;

            string scene = gameObject.scene.name;
            return scene == CampaignScenes.Tutorial || scene == CampaignScenes.SystemsScene
                ? Resources.Load<AudioClip>(CourtyardTheme)
                : null;
        }

        private AudioSource CreateVoice(AudioClip clip)
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.loop = true;
            voice.clip = clip;
            voice.volume = 0f;
            return voice;
        }

        public void Activate()
        {
            if (activated || !isActiveAndEnabled)
                return;
            activated = true;
            if (calm.clip)
                calm.Play();
            if (active.clip)
                active.Play();
            if (roll.clip)
                roll.Play();
        }

        public void ReportMovement(float amount) => movement = Mathf.Max(movement, Mathf.Clamp01(amount));

        internal void SetMusicSuppressed(bool suppressed) => musicSuppressed = suppressed;

        public void Play(Cue cue, float intensity = 1f)
        {
            if (!isActiveAndEnabled)
                return;
            Activate();
            intensity = Mathf.Clamp01(intensity);
            if (courtyardTones && cue == Cue.SlimeCompression)
            {
                quietUntil = Time.unscaledTime + 0.23f;
                return;
            }
            if (courtyardTones && cue == Cue.Bounce && Time.unscaledTime < quietUntil)
                return;
            foreach (AudioCue sound in sounds)
            {
                if (sound.kind != cue || !sound.clip)
                    continue;
                PlayCue(cue, sound.clip, sound.volume * intensity, sound.throttle);
                return;
            }

            if (cue == Cue.Slime && poolNotes != null)
                PlayCue(cue, poolNotes[Mathf.RoundToInt(intensity * (poolNotes.Length - 1))], 0.48f, 0.12f);
            else if (cue == Cue.PressurePlate && (pressurePlateChime || resolvedChord))
                PlayCue(cue, pressurePlateChime ? pressurePlateChime : resolvedChord, 0.62f * intensity, 0.12f);
            else if (cue == Cue.Unlock && districtRevealChime)
                PlayCue(cue, districtRevealChime, 0.58f * intensity, 0.12f);
        }

        private void PlayCue(Cue cue, AudioClip clip, float volume, float throttle)
        {
            if (lastPlayed.TryGetValue(cue, out float last) && Time.unscaledTime - last < throttle)
                return;
            lastPlayed[cue] = Time.unscaledTime;
            oneShot.PlayOneShot(clip, volume * CampaignSettings.EffectsVolume);
        }

        private void Update()
        {
            if (!activated)
                return;
            var ball = level ? level.Ball : null;
            float speed = ball && ball.Released && !ball.InTransition
                ? Mathf.Clamp01(ball.Body.linearVelocity.magnitude / Mathf.Max(fullSpeed, 0.1f))
                : 0f;
            float energy = Mathf.Max(speed, movement);
            bool complete = level && level.Completed;
            float transition = Mathf.SmoothStep(0f, 1f, energy);
            float dt = Time.unscaledDeltaTime * fadeRate;
            float calmVolume = complete ? 0.35f : active.clip ? 1f - transition * 0.75f : 1f;
            bool quiet = Time.unscaledTime < quietUntil;
            float musicGain = musicVolume * CampaignSettings.MusicVolume;
            Fade(calm, musicSuppressed ? 0f : musicGain * calmVolume * (quiet ? 0.16f : 1f), quiet ? dt * 5f : dt);
            Fade(active, musicSuppressed || complete || quiet ? 0f : musicGain * transition, dt);
            float ground = ball && Mathf.Abs(ball.Body.linearVelocity.y) < 1.5f ? 1f : 0f;
            Fade(roll, complete ? 0f : rollingVolume * CampaignSettings.EffectsVolume * speed * ground, dt);
            roll.pitch = Mathf.Lerp(0.72f, 1.18f, speed);
            movement = Mathf.MoveTowards(movement, 0, Time.unscaledDeltaTime);
        }

        private static void Fade(AudioSource voice, float target, float step) =>
            voice.volume = Mathf.MoveTowards(voice.volume, target, step);

        private static AudioClip Tone(string name, float duration, params int[] notes)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            foreach (int note in notes)
            {
                float frequency = 440f * Mathf.Pow(2f, (note - 69) / 12f);
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / rate;
                    float phase = 2f * Mathf.PI * frequency * t;
                    float envelope = Mathf.Min(t * 170f, 1f) * Mathf.Exp(-t * 5f / duration);
                    samples[i] += (Mathf.Sin(phase) + 0.24f * Mathf.Sin(phase * 2f)) * envelope * 0.55f / notes.Length;
                }
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (poolNotes != null)
                foreach (var clip in poolNotes)
                    Destroy(clip);
            if (resolvedChord)
                Destroy(resolvedChord);
        }
    }
}
