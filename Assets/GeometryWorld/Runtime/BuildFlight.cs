using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    internal sealed class BuildFlight
    {
        private Camera camera;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private bool orthographic;
        private float fieldOfView, near, yaw, pitch;
        private CursorLockMode previousLock;
        private bool previousVisibility;
        private bool started;

        internal bool Captured => Cursor.lockState == CursorLockMode.Locked;

        internal void Begin(Camera view)
        {
            if (!view)
                return;
            camera = view;
            started = true;
            originalPosition = view.transform.localPosition;
            originalRotation = view.transform.localRotation;
            orthographic = view.orthographic;
            fieldOfView = view.fieldOfView;
            near = view.nearClipPlane;
            previousLock = Cursor.lockState;
            previousVisibility = Cursor.visible;
            yaw = view.transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, view.transform.eulerAngles.x);
            view.orthographic = false;
            view.fieldOfView = 65f;
            view.nearClipPlane = 0.035f;
            Resume();
        }

        internal void Resume()
        {
            if (!camera)
                return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        internal void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        internal void Update(Keyboard keyboard, Mouse mouse)
        {
            if (!camera || !Captured || !Application.isFocused)
                return;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * 0.14f;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
                camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            if (keyboard == null)
                return;
            Vector3 input = new(Axis(keyboard.dKey, keyboard.aKey),
                Axis(keyboard.spaceKey, keyboard.leftCtrlKey), Axis(keyboard.wKey, keyboard.sKey));
            float speed = keyboard.leftShiftKey.isPressed ? 15f : 6f;
            camera.transform.position += Quaternion.Euler(0f, yaw, 0f) * Vector3.ClampMagnitude(input, 1f) *
                (speed * Time.unscaledDeltaTime);
        }

        private static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive, UnityEngine.InputSystem.Controls.KeyControl negative)
            => (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

        internal void End()
        {
            if (!started)
                return;
            if (camera)
            {
                camera.transform.SetLocalPositionAndRotation(originalPosition, originalRotation);
                camera.orthographic = orthographic;
                camera.fieldOfView = fieldOfView;
                camera.nearClipPlane = near;
            }
            camera = null;
            started = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisibility;
        }
    }
}
