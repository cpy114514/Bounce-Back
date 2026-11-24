using UnityEngine;

namespace BounceBack.GeometryWorld
{
    [System.Serializable]
    public struct MenuCameraPose
    {
        public Vector3 focus, angles;
        public float height, width;

        public MenuCameraPose(Vector3 focus, Vector3 angles, float height, float width)
        {
            this.focus = focus;
            this.angles = angles;
            this.height = height;
            this.width = width;
        }

        internal static MenuCameraPose Lerp(MenuCameraPose from, MenuCameraPose to, float amount) => new(
            Vector3.Lerp(from.focus, to.focus, amount), Vector3.Lerp(from.angles, to.angles, amount),
            Mathf.Lerp(from.height, to.height, amount), Mathf.Lerp(from.width, to.width, amount));

        public void Apply(Camera camera)
        {
            Quaternion angle = Quaternion.Euler(angles);
            camera.transform.SetPositionAndRotation(focus - angle * Vector3.forward * 32f, angle);
            camera.orthographicSize = Mathf.Max(height, width / Mathf.Max(0.25f, camera.aspect));
        }
    }
}
