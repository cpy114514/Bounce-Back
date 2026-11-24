using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuSwallows : MonoBehaviour
    {
        [Serializable]
        public struct Bird
        {
            public Transform root, farWing, nearWing;
        }

        public Camera view;
        public Bird[] birds;

        private static readonly Vector2 OrbitRadius = new(.12f, .085f);
        private MainMenu menu;
        private Quaternion[] farRest, nearRest;
        private Vector2 orbitCenter, orbitVelocity;
        private float start, glideTime, orbitAmount;
        private bool reducedMotion;

        private void Awake()
        {
            start = Time.unscaledTime;
            reducedMotion = CampaignSettings.ReducedMotion;
            menu = view.GetComponentInParent<MainMenu>();
            farRest = new Quaternion[birds.Length];
            nearRest = new Quaternion[birds.Length];
            for (int i = 0; i < birds.Length; i++)
            {
                farRest[i] = birds[i].farWing.localRotation;
                nearRest[i] = birds[i].nearWing.localRotation;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                return;

            float time = reducedMotion ? 0f : Time.unscaledTime - start;
            float aspect = view.aspect;
            bool tracking = TryGetOrbitCenter(aspect, out Vector2 target);
            if (tracking)
            {
                orbitCenter = orbitAmount == 0f ? target :
                    Vector2.SmoothDamp(orbitCenter, target, ref orbitVelocity, .26f, Mathf.Infinity, dt);
            }
            orbitAmount = Mathf.MoveTowards(orbitAmount, tracking ? 1f : 0f, dt * (tracking ? 1.2f : .9f));
            float blend = Mathf.SmoothStep(0f, 1f, orbitAmount);
            if (orbitAmount == 0f)
            {
                orbitVelocity = Vector2.zero;
                if (!reducedMotion) glideTime += dt;
            }

            float flight = Mathf.Repeat(.68f + glideTime / 25f, 1f);
            float height = view.orthographicSize * 2f;
            for (int i = 0; i < birds.Length; i++)
            {
                Bird bird = birds[i];
                float phase = Mathf.Repeat(time * .28f + i * .17f, 1f);
                float dip = phase < .66f ? Mathf.SmoothStep(0f, 1f, phase / .66f)
                    : Mathf.SmoothStep(1f, 0f, (phase - .66f) / .34f);
                float x = Mathf.Lerp(-.10f, 1.19f, flight) - i * .043f;
                float y = .921f + i * .017f - .027f * dip;
                float bank = -9f * Mathf.Sin(phase * Mathf.PI * 2f);
                var glide = new Vector2((x - .5f) * aspect, y - .5f);
                float angle = time * 1.32f - i * .82f;
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 orbit = orbitCenter + Vector2.Scale(OrbitRadius, radial);
                Vector2 position = Vector2.Lerp(glide, orbit, blend) * height;
                float depth = Mathf.Lerp(60f, 1.6f + radial.y * .35f, blend);
                float tangent = Mathf.Atan2(OrbitRadius.y * radial.x, -OrbitRadius.x * radial.y) * Mathf.Rad2Deg;
                var heading = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(bank, tangent, blend));
                bird.root.localPosition = new Vector3(position.x, position.y, depth);
                bird.root.localScale = Vector3.one * (height * (.019f - i * .003f));
                bird.root.localRotation = blend > 0f
                    ? Quaternion.RotateTowards(bird.root.localRotation, heading, 390f * dt) : heading;
                float flap = reducedMotion ? 0f : Mathf.Sin(time * 11f + i * 2.1f) *
                    Mathf.Lerp(11f, 37f, dip);
                bird.farWing.localRotation = Quaternion.AngleAxis(flap + 23f, Vector3.right) * farRest[i];
                bird.nearWing.localRotation = Quaternion.AngleAxis(-flap - 29f, Vector3.right) * nearRest[i];
            }
        }

        private bool TryGetOrbitCenter(float aspect, out Vector2 center)
        {
            center = default;
            Mouse mouse = Mouse.current;
            if (reducedMotion || !menu || menu.CurrentView != MainMenu.View.Overview || !Application.isFocused ||
                mouse == null || Touchscreen.current?.primaryTouch.press.isPressed == true)
                return false;

            Vector2 pointer = mouse.position.ReadValue();
            if (pointer == Vector2.zero || !view.pixelRect.Contains(pointer))
                return false;

            Vector3 cursor = view.ScreenToViewportPoint(pointer);
            const float margin = .024f;
            float edgeX = Mathf.Max(0f, aspect * .5f - OrbitRadius.x - margin);
            float edgeY = .5f - OrbitRadius.y - margin;
            center = new Vector2(Mathf.Clamp((cursor.x - .5f) * aspect, -edgeX, edgeX),
                Mathf.Clamp(cursor.y - .5f, -edgeY, edgeY));
            return true;
        }
    }
}
