using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [RequireComponent(typeof(PuzzleTrigger))]
    public sealed class GoalPressurePlate : MonoBehaviour
    {
        public GeometryLevel level;
        public Transform surface;
        public GameObject litPattern;
        public LineRenderer[] pulses = Array.Empty<LineRenderer>();
        [Min(0f)] public float depression = 0.10f;
        public bool Pressed => level && level.Completed && level.goal == zone;
        public event Action Activated;

        private PuzzleTrigger zone;
        private Vector3 raisedPosition;
        private Vector3 contactPosition;
        private float amount;
        private bool wasPressed;
        private float pulseTime;

        private void Awake()
        {
            zone = GetComponent<PuzzleTrigger>();
            if (surface)
                raisedPosition = surface.localPosition;
            if (litPattern)
                litPattern.SetActive(false);
        }

        private void Update()
        {
            bool pressed = Pressed;
            if (pressed != wasPressed)
            {
                wasPressed = pressed;
                if (litPattern)
                    litPattern.SetActive(pressed);
                pulseTime = 0f;
                if (pressed)
                {
                    if (level.Ball)
                        contactPosition = level.Ball.Body.position;
                    Activated?.Invoke();
                    if (level.audioDirector)
                        level.audioDirector.Play(Cue.PressurePlate);
                }
            }
            UpdatePulses(pressed);
            float previous = amount;
            amount = Mathf.MoveTowards(amount, pressed ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            float depth = depression * Mathf.SmoothStep(0f, 1f, amount);
            if (surface)
                surface.localPosition = raisedPosition - Vector3.up * depth;
            if (pressed && previous < 1f && level.Ball && level.Ball.Body.isKinematic &&
                Vector3.ProjectOnPlane(level.Ball.Body.position - contactPosition, Vector3.up).sqrMagnitude < 0.0004f)
                level.Ball.Body.position = contactPosition - Vector3.up * depth;
        }

        private void UpdatePulses(bool pressed)
        {
            const float duration = 1.1f;
            const float interval = 0.13f;
            pulseTime += Time.unscaledDeltaTime;
            for (int i = 0; i < pulses.Length; i++)
            {
                var pulse = pulses[i];
                if (!pulse)
                    continue;
                float progress = (pulseTime - i * interval) / duration;
                bool visible = pressed && progress >= 0f && progress < 1f;
                pulse.enabled = visible;
                if (!visible)
                    continue;
                float radius = Mathf.Lerp(0.3f, 9f, Mathf.SmoothStep(0f, 1f, progress));
                pulse.transform.localScale = new Vector3(radius, 1f, radius);
                pulse.widthMultiplier = 0.044f * (1f - progress);
            }
        }
    }
}
