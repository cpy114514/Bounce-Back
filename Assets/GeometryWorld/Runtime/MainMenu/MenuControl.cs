using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuControl : MonoBehaviour
    {
        public enum Action { Low, Medium, High, Fullscreen, Master, Music, Effects, Back }

        public Action action;
        public BoxCollider hitbox;
        public Transform model, label, indicator, handle;
        public Transform[] corners;
        public float railHalfLength = 1.75f;

        internal bool IsRail => action is Action.Master or Action.Music or Action.Effects;

        private Vector3 modelRest, handleRest;
        private Vector3[] cornerRest;

        private void Awake()
        {
            if (model) modelRest = model.localPosition;
            if (handle) handleRest = handle.localPosition;
            cornerRest = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++)
                cornerRest[i] = corners[i].localPosition;
        }

        internal void Present(float value, bool focused, float step)
        {
            bool selected = (int)action <= (int)Action.High && value > 0f;
            model.localPosition = Vector3.Lerp(model.localPosition, modelRest +
                Vector3.up * ((selected ? 0.16f : 0f) + (focused && !IsRail ? 0.09f : 0f)), step);
            if (indicator) indicator.gameObject.SetActive(selected || focused);
            if (handle)
            {
                handle.localPosition = handleRest + Vector3.right * ((2f * value - 1f) * railHalfLength);
                handle.localScale = Vector3.Lerp(handle.localScale, Vector3.one * (focused ? 1.18f : 1f), step);
            }
            for (int i = 0; i < corners.Length; i++)
                corners[i].localPosition = Vector3.Lerp(corners[i].localPosition, cornerRest[i] * (value > 0f ? 1.36f : 1f), step);
        }

        internal bool TryVolume(Ray ray, out float value)
        {
            var plane = new Plane(model.up, handle.position);
            if (plane.Raycast(ray, out float distance))
            {
                float x = model.InverseTransformPoint(ray.GetPoint(distance)).x;
                value = Mathf.Round(Mathf.InverseLerp(-railHalfLength, railHalfLength, x) * 100f) * .01f;
                return true;
            }
            value = 0f;
            return false;
        }

        internal static float Distance(BoxCollider box, Ray ray)
        {
            if (!box || !box.gameObject.activeInHierarchy)
                return float.PositiveInfinity;
            return Distance(box.transform, new Bounds(box.center, box.size), ray);
        }

        internal static float Distance(MeshFilter surface, Ray ray)
        {
            if (!surface || !surface.gameObject.activeInHierarchy || !surface.sharedMesh)
                return float.PositiveInfinity;
            return Distance(surface.transform, surface.sharedMesh.bounds, ray);
        }

        private static float Distance(Transform space, Bounds bounds, Ray ray)
        {
            var local = new Ray(space.InverseTransformPoint(ray.origin), space.InverseTransformVector(ray.direction));
            return bounds.IntersectRay(local, out float distance)
                ? Vector3.Distance(ray.origin, space.TransformPoint(local.GetPoint(distance))) : float.PositiveInfinity;
        }
    }
}
