using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    /// <summary>Serialized, unscaled-time presentation data for the first courtyard's completion.</summary>
    public sealed class CourtyardFinale : MonoBehaviour
    {
        [Header("References")]
        public GeometryPose folding;
        public Transform waterfall;
        public AudioClip completionBell;
        public Material splashMaterial;

        [Header("Opening beats (seconds)")]
        [Min(0f)] public float plateHold = 0.30f;
        [Min(0f)] public float waterfallPause = 0.20f;
        [Min(0.1f)] public float splashDuration = 0.90f;

        [Header("Four independently timed anchor folds")]
        [Min(0.1f)] public float anchorFoldDuration = 1.40f;
        [Range(0.15f, 0.25f)] public float anchorStagger = 0.18f;
        [Range(0f, 0.12f)] public float overshoot = 0.055f;
        public float anchorLift = 0.34f;

        [Header("Road, camera and ball rollout")]
        [Min(0.1f)] public float roadRevealDuration = 1.30f;
        [Min(0.1f)] public float followTransitionDuration = 1.80f;
        [Min(0.1f)] public float rolloutDuration = 0.65f;
        [Range(13f, 15f)] public float foldingCameraSize = 14.2f;
        [Tooltip("Seconds the camera starts following the dock before the pull-back finishes, so it never stops mid-move.")]
        [Min(0f)] public float cameraOverlap = 0.45f;
        [Range(0.04f, 0.20f)] public float ballScreenMargin = 0.075f;
        [Range(0.041f, 0.10f)] public float ballHardMargin = 0.045f;
        [Min(0.01f)] public float framingDamping = 0.2f;
        [Tooltip("Beat between the last anchor landing and the bell.")]
        [Range(0f, 0.2f)] public float bellDelay = 0.07f;

        public float FoldStart => plateHold + waterfallPause;
        public float FoldSeconds => anchorFoldDuration + anchorStagger * Mathf.Max(0, AnchorCount - 1);
        public float FoldComplete => FoldStart + FoldSeconds;
        public float BellTime => FoldComplete + bellDelay;
        public float FollowStart => Mathf.Max(FoldStart, FoldComplete - cameraOverlap);
        public float RolloutStart => FoldComplete + followTransitionDuration;
        public float SequenceSeconds => RolloutStart + rolloutDuration;
        public float SequenceDuration => CampaignSettings.ReducedMotion ? Mathf.Min(0.5f, SequenceSeconds) : SequenceSeconds;
        public float SequenceRate => SequenceSeconds / Mathf.Max(0.01f, SequenceDuration);
        public int AnchorCount => folding && folding.tracks != null ? Mathf.Max(0, folding.tracks.Length - 1) : 0;
        public bool IsPlaying { get; private set; }
        public float SplashAmount { get; private set; }
        public int SplashCount { get; private set; }
        public int BellCount { get; private set; }
        public float MinimumBallMargin { get; private set; } = 1f;
        public event Action WaterBurst;
        public event Action Folded;

        private Transform[] ribbons = Array.Empty<Transform>();
        private Vector3[] ribbonPositions = Array.Empty<Vector3>();
        private Vector3[] ribbonScales = Array.Empty<Vector3>();
        private Vector3[] pausedPositions = Array.Empty<Vector3>();
        private Vector3[] pausedScales = Array.Empty<Vector3>();
        private Transform splashRoot;
        private Transform[] droplets = Array.Empty<Transform>();
        private Vector3[] dropletSeeds = Array.Empty<Vector3>();
        private AudioSource bellVoice;
        private bool captured, burstSent, foldSent, waterPaused;
        private float plateHoldUntil, waterPausedAt, waterPausedTotal;
        private Vector2 framing, framingVelocity;
        private float framingTime = -1f;

        private void Awake() => Capture();
        private void OnDestroy() => Shader.SetGlobalFloat("_GPreviewTime", 0f);

        private void Capture()
        {
            if (captured)
                return;
            if (folding)
                folding.Apply(0f);
            if (waterfall)
            {
                // Include the integrated G-Waterfall prefab as well as the old whitebox ribbons.
                var children = waterfall.GetComponentsInChildren<Renderer>(true);
                ribbons = new Transform[children.Length];
                ribbonPositions = new Vector3[children.Length];
                ribbonScales = new Vector3[children.Length];
                pausedPositions = new Vector3[children.Length];
                pausedScales = new Vector3[children.Length];
                for (int index = 0; index < children.Length; index++)
                {
                    ribbons[index] = children[index].transform;
                    ribbonPositions[index] = children[index].transform.localPosition;
                    ribbonScales[index] = children[index].transform.localScale;
                }
            }
            if (!bellVoice)
            {
                var voice = new GameObject("Finale Bell Voice");
                voice.transform.SetParent(transform, false);
                bellVoice = voice.AddComponent<AudioSource>();
                bellVoice.playOnAwake = false;
                bellVoice.spatialBlend = 0f;
                bellVoice.volume = 0.78f;
            }
            if (!completionBell)
                completionBell = Resources.Load<AudioClip>("BounceBack/Audio/District Reveal Chime");
            captured = true;
        }

        private void Update()
        {
            if (!captured || !waterfall)
                return;
            if (!IsPlaying || Time.realtimeSinceStartup < plateHoldUntil)
                AnimateWater(Time.unscaledTime);
        }

        private void AnimateWater(float realtime)
        {
            if (waterPaused)
            {
                waterPausedTotal += realtime - waterPausedAt;
                waterPaused = false;
            }
            // Subtracting the pause makes the flow resume where it froze instead of skipping ahead.
            float time = realtime - waterPausedTotal;
            // G water shaders use this optional clock; zero restores their normal realtime clock.
            Shader.SetGlobalFloat("_GPreviewTime", Mathf.Max(0.001f, time));
            for (int i = 0; i < ribbons.Length; i++)
            {
                if (!ribbons[i])
                    continue;
                float wave = Mathf.Sin(time * 2.8f + i * 0.73f);
                ribbons[i].localPosition = ribbonPositions[i] + Vector3.up * (wave * 0.025f);
                ribbons[i].localScale = Vector3.Scale(ribbonScales[i], new Vector3(1f, 1f + wave * 0.012f, 1f));
            }
        }

        public void Begin()
        {
            Capture();
            IsPlaying = true;
            plateHoldUntil = Time.realtimeSinceStartup + plateHold;
            burstSent = foldSent = waterPaused = false;
            framing = framingVelocity = Vector2.zero;
            framingTime = -1f;
            SplashAmount = 0f;
            SplashCount = 0;
            BellCount = 0;
            MinimumBallMargin = 1f;
            for (int i = 0; i < ribbons.Length; i++)
            {
                pausedPositions[i] = ribbons[i].localPosition;
                pausedScales[i] = ribbons[i].localScale;
            }
            if (splashRoot)
                splashRoot.gameObject.SetActive(false);
            Evaluate(0f);
        }

        /// <summary>Apply an absolute beat time. It never cancels recording, input locks or gameplay state.</summary>
        public void Evaluate(float seconds)
        {
            Capture();
            float foldTime = Mathf.Max(0f, seconds - FoldStart);
            if (folding && folding.tracks != null)
            {
                float total = Mathf.Max(0.01f, FoldSeconds);
                for (int i = 1; i < folding.tracks.Length; i++)
                {
                    PoseTrack track = folding.tracks[i];
                    if (track == null)
                        continue;
                    float delay = (i - 1) * anchorStagger;
                    track.phaseStart = delay / total;
                    track.phaseEnd = (delay + anchorFoldDuration) / total;
                    track.overshoot = overshoot;
                    track.lift = anchorLift * (1f - (i - 1) * 0.16f);
                }
                folding.Apply(Mathf.Clamp01(foldTime / total));
            }

            if (seconds < FoldStart)
            {
                if (seconds < plateHold)
                    AnimateWater(Time.unscaledTime);
                else
                {
                    if (!waterPaused)
                    {
                        waterPaused = true;
                        waterPausedAt = Time.unscaledTime;
                    }
                    for (int i = 0; i < ribbons.Length; i++)
                    {
                        ribbons[i].localPosition = pausedPositions[i];
                        ribbons[i].localScale = pausedScales[i];
                    }
                }
            }
            else
            {
                if (!burstSent)
                {
                    burstSent = true;
                    SplashCount++;
                    WaterBurst?.Invoke();
                }
                AnimateWater(Time.unscaledTime);
                SplashAmount = Mathf.Clamp01((seconds - FoldStart) / Mathf.Max(0.1f, splashDuration));
                AnimateSplash(SplashAmount);
            }

            if (!foldSent && seconds >= BellTime)
            {
                foldSent = true;
                if (bellVoice && completionBell)
                    bellVoice.PlayOneShot(completionBell, 0.82f);
                BellCount++;
                Folded?.Invoke();
            }
        }

        private void CreateSplash()
        {
            if (splashRoot || !waterfall)
                return;
            splashRoot = new GameObject("Completion Water Burst").transform;
            splashRoot.SetParent(waterfall, false);
            const int count = 18;
            droplets = new Transform[count];
            dropletSeeds = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                drop.name = $"Water Spark {i + 1:00}";
                var collider = drop.GetComponent<Collider>();
                if (collider)
                    collider.enabled = false;
                drop.transform.SetParent(splashRoot, false);
                if (splashMaterial)
                    drop.GetComponent<Renderer>().sharedMaterial = splashMaterial;
                float angle = i * 2.399963f;
                dropletSeeds[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                droplets[i] = drop.transform;
            }
            splashRoot.gameObject.SetActive(false);
        }

        private void AnimateSplash(float amount)
        {
            CreateSplash();
            if (!splashRoot)
                return;
            splashRoot.gameObject.SetActive(amount > 0f && amount < 1f);
            float travel = Mathf.Sin(amount * Mathf.PI * 0.72f);
            float envelope = Mathf.Sin(amount * Mathf.PI);
            for (int i = 0; i < droplets.Length; i++)
            {
                Vector3 seed = dropletSeeds[i];
                float spread = (0.42f + (i % 5) * 0.11f) * travel;
                droplets[i].localPosition = new Vector3(0.10f + spread, 0.08f + envelope * (0.58f + (i % 4) * 0.12f),
                    seed.z * spread * 1.5f);
                droplets[i].localRotation = Quaternion.Euler(24f + i * 13f, i * 29f, amount * 120f);
                float size = (0.055f + (i % 3) * 0.025f) * envelope;
                droplets[i].localScale = new Vector3(size * 1.6f, size, size);
            }
        }

        public void SnapFolded()
        {
            Capture();
            if (folding)
                folding.Apply(1f);
            IsPlaying = false;
            SplashAmount = 0f;
            Shader.SetGlobalFloat("_GPreviewTime", 0f);
            if (splashRoot)
                splashRoot.gameObject.SetActive(false);
        }

        public void ResetSequence()
        {
            Capture();
            IsPlaying = false;
            SplashAmount = 0f;
            Shader.SetGlobalFloat("_GPreviewTime", 0f);
            if (splashRoot)
                splashRoot.gameObject.SetActive(false);
            for (int i = 0; i < ribbons.Length; i++)
            {
                ribbons[i].localPosition = ribbonPositions[i];
                ribbons[i].localScale = ribbonScales[i];
            }
        }

        /// <summary>A deterministic cinematic frame with a screen-space safety correction for the ball.</summary>
        public void FrameBall(PuzzleCameraRig rig, Vector3 focus, Vector3 cameraOffset, float size, Vector3 ballPosition)
        {
            if (!rig || !rig.view)
                return;
            Camera view = rig.view;
            view.transform.SetPositionAndRotation(focus + cameraOffset, Quaternion.LookRotation(-cameraOffset));
            view.orthographicSize = size;
            float aspect = Mathf.Max(0.1f, view.aspect);
            Vector3 viewport = view.WorldToViewportPoint(ballPosition);
            Vector2 wanted = new(Mathf.Clamp(viewport.x, ballScreenMargin, 1f - ballScreenMargin) - viewport.x,
                Mathf.Clamp(viewport.y, ballScreenMargin, 1f - ballScreenMargin) - viewport.y);
            float now = Time.unscaledTime;
            if (framingTime < 0f)
                framing = wanted;
            else
                framing = Vector2.SmoothDamp(framing, wanted, ref framingVelocity, framingDamping, Mathf.Infinity,
                    Mathf.Max(0f, now - framingTime));
            framingTime = now;
            // The damped offset is in viewport units; the hard margin is only a fallback for fast moves.
            float hard = Mathf.Min(ballHardMargin, ballScreenMargin);
            framing.x = Mathf.Clamp(viewport.x + framing.x, hard, 1f - hard) - viewport.x;
            framing.y = Mathf.Clamp(viewport.y + framing.y, hard, 1f - hard) - viewport.y;
            view.transform.position += view.transform.right * (framing.x * size * 2f * aspect) +
                view.transform.up * (framing.y * size * 2f);
            viewport = view.WorldToViewportPoint(ballPosition);
            MinimumBallMargin = Mathf.Min(MinimumBallMargin, viewport.z > 0f
                ? Mathf.Min(Mathf.Min(viewport.x, 1f - viewport.x), Mathf.Min(viewport.y, 1f - viewport.y)) : -1f);
        }

        public static float Ease(float fraction) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fraction));
    }
}
