using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class PuzzleCameraRig : MonoBehaviour
    {
        internal const string ControlsHint = "Rotate: right-drag or Q / E    Reset view: Home";
        internal const float DefaultMouseDegreesPerPixel = 0.22f;
        internal const float DefaultKeyboardDegreesPerSecond = 75f;
        internal const float RotationResponsiveness = 5f;
        public GeometryLevel level;
        public Camera view;
        public Transform overview;
        [Min(0.1f)] public float overviewSize = 12f;
        [Min(0.1f)] public float followSize = 8f;
        [Min(0.05f)] public float damping = 0.48f;
        public Vector3 followOffset = new(18f, 18f, -21f);
        public bool frameCourtyard;
        [Min(0f)] public float mouseDegreesPerPixel = DefaultMouseDegreesPerPixel;
        [Min(0f)] public float keyboardDegreesPerSecond = DefaultKeyboardDegreesPerSecond;

        private Vector3 cameraVelocity;
        private float zoomVelocity;
        private float yaw, pitch;
        private float zoom = 1f;
        public bool Cinematic { get; set; }
        internal float ZoomedSize(float size) => size * zoom;

        private void LateUpdate()
        {
            if (Cinematic || !view || !overview || !level || !level.Ball || level.IsDraggingMechanism)
                return;
            if (frameCourtyard && level.Ball.InTransition)
                return;
            if (level.InputEnabled)
                ReadZoomInput();
            if (level.InputEnabled && (!frameCourtyard || !level.Ball.Released))
                ReadOrbitInput();

            var ball = level.Ball;
            bool following = ball.Released && !frameCourtyard;
            float size = ZoomedSize(following || frameCourtyard && ball.Released ? followSize : overviewSize);
            if (!following && yaw == 0f && pitch == 0f)
            {
                Move(overview.position, overview.rotation, size);
                return;
            }
            Vector3 baseOffset = following ? followOffset : -overview.forward * followOffset.magnitude;
            Vector3 focus = following
                ? ball.transform.position + Vector3.ClampMagnitude(ball.Body.linearVelocity * 0.32f, 2.1f)
                : overview.position - baseOffset;
            Vector3 orbitOffset = baseOffset;
            if (yaw != 0f || pitch != 0f)
            {
                float elevation = Mathf.Atan2(baseOffset.y, Vector3.ProjectOnPlane(baseOffset, Vector3.up).magnitude) * Mathf.Rad2Deg;
                float adjusted = Mathf.Clamp(elevation + pitch, 24f, 70f) - elevation;
                Vector3 pitchAxis = Vector3.Cross(baseOffset, Vector3.up).normalized;
                orbitOffset = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(adjusted, pitchAxis) * baseOffset;
            }
            if (orbitOffset.sqrMagnitude < 0.001f)
                return;
            Move(focus + orbitOffset, Quaternion.LookRotation(-orbitOffset), size);
        }

        internal void ReadZoomInput()
        {
            if (Keyboard.current?.homeKey.wasPressedThisFrame == true)
            {
                zoom = 1f;
                return;
            }
            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            zoom = ZoomFromScroll(zoom, scroll);
        }

        internal static float ScrollSteps(float scroll) => Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll;

        internal static float ZoomFromScroll(float zoom, float scroll)
        {
            if (scroll == 0f)
                return zoom;
            return Mathf.Clamp(zoom * Mathf.Pow(1.15f, -Mathf.Clamp(ScrollSteps(scroll), -3f, 3f)), 0.67f, 1.6f);
        }

        private void ReadOrbitInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.homeKey.wasPressedThisFrame == true)
            {
                yaw = pitch = 0f;
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed && !mouse.leftButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseDegreesPerPixel;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -46f, 46f);
            }
            if (keyboard != null)
            {
                float direction = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
                yaw += direction * keyboardDegreesPerSecond * Time.unscaledDeltaTime;
            }
            yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }

        public void Move(Vector3 position, Quaternion rotation, float size, bool snap = false)
        {
            if (!view)
                return;
            if (snap)
            {
                view.transform.SetPositionAndRotation(position, rotation);
                view.orthographicSize = size;
                cameraVelocity = Vector3.zero;
                zoomVelocity = 0f;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            view.transform.position = Vector3.SmoothDamp(view.transform.position, position, ref cameraVelocity, damping, Mathf.Infinity, dt);
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation, rotation, 1f - Mathf.Exp(-dt * RotationResponsiveness));
            view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, size, ref zoomVelocity, damping, Mathf.Infinity, dt);
        }
    }
}
