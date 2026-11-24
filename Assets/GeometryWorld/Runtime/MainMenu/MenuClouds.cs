using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuClouds : MonoBehaviour
    {
        [Serializable]
        public struct Cloud
        {
            public Transform model;
            public Vector2 viewport;
            public float width;
            public float verticalScale;
        }

        public Camera view;
        public Cloud[] clouds;

        private float framedSize;
        private float framedAspect;

        private void Start() => Frame();

        private void LateUpdate()
        {
            if (view.orthographicSize != framedSize || view.aspect != framedAspect)
                Frame();
        }

        public void Frame()
        {
            framedSize = view.orthographicSize;
            framedAspect = view.aspect;
            float height = framedSize * 2f;
            foreach (var cloud in clouds)
            {
                Vector2 offset = cloud.viewport - Vector2.one * .5f;
                cloud.model.localPosition = new Vector3(offset.x * height * framedAspect, offset.y * height, 68f);
                cloud.model.localScale = new Vector3(1f, cloud.verticalScale, 1f) * (height * cloud.width);
            }
        }
    }
}
