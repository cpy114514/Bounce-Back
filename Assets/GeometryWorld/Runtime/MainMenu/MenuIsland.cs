using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuIsland : MonoBehaviour
    {
        internal const float RecedeDistance = 22f;

        [Serializable]
        public struct Fragment
        {
            public Transform part;
            public Vector3 assembled, ruined, ruinedEuler, openedOffset, openedEuler, scatteredOffset, scatteredEuler;
            public float delay, arc, assemblyDelay;
            public Quaternion assembledRotation;
        }

        [Serializable]
        public struct ClockHand
        {
            public Transform pivot;
            public Vector3 axis;
            public Quaternion rest;
            public float turns;
        }

        public BoxCollider hitbox;
        public Transform label;
        public Fragment[] fragments;
        public ClockHand[] hands;

        private float restored, velocity;
        private Vector3 shore;
        private Quaternion heading;
        private MeshFilter[] hoverSurfaces;

        internal float Restored => restored;
        internal bool Settled => restored > 0.995f;

        private void Awake()
        {
            shore = transform.localPosition;
            heading = transform.localRotation;
            hoverSurfaces = new MeshFilter[fragments.Length];
            for (int i = 0; i < fragments.Length; i++)
                hoverSurfaces[i] = fragments[i].part.GetComponent<MeshFilter>();
        }

        internal float HoverDistance(Ray ray)
        {
            float nearest = float.PositiveInfinity;
            foreach (var surface in hoverSurfaces)
                nearest = Mathf.Min(nearest, MenuControl.Distance(surface, ray));
            return nearest;
        }

        internal void Pose(float assembly, bool restore, float opening, float recede, float clockRewind,
            float time, float deltaTime, bool reducedMotion, float drift, int index)
        {
            float target = restore ? 1f : 0f;
            if (deltaTime > 0f)
                restored = Mathf.SmoothDamp(restored, target, ref velocity, reducedMotion ? 0.09f : 0.31f,
                    Mathf.Infinity, deltaTime);
            if (Mathf.Abs(restored - target) < 0.0006f && Mathf.Abs(velocity) < 0.005f)
            {
                restored = target;
                velocity = 0f;
            }

            PoseBase(transform, shore, heading, recede, drift, time, index);
            foreach (Fragment fragment in fragments)
            {
                float gather = Ease(Mathf.InverseLerp(fragment.assemblyDelay, 1f, assembly));
                float repair = Ease(Mathf.InverseLerp(fragment.delay, 0.97f, restored));
                Vector3 position = Vector3.LerpUnclamped(fragment.ruined, fragment.assembled, repair);
                position.y += Mathf.Sin(repair * Mathf.PI) * fragment.arc;
                Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(fragment.ruinedEuler),
                    fragment.assembledRotation, repair);
                if (gather < 1f)
                {
                    position += fragment.scatteredOffset * (1f - gather);
                    if (!reducedMotion)
                        position += Vector3.up * (Mathf.Sin(time * 0.87f + fragment.assemblyDelay * 12f) * 0.11f * (1f - gather));
                    rotation *= Quaternion.Euler(fragment.scatteredEuler * (1f - gather));
                }
                fragment.part.SetLocalPositionAndRotation(position + opening * fragment.openedOffset,
                    rotation * Quaternion.Euler(opening * fragment.openedEuler));
            }

            foreach (ClockHand hand in hands)
                hand.pivot.localRotation = hand.rest *
                    Quaternion.AngleAxis((-restored - clockRewind * 1.3f) * hand.turns * 360f, hand.axis);
        }

        internal static void PoseBase(Transform island, Vector3 shore, Quaternion heading, float recede,
            float drift, float time, int index)
        {
            float phase = time * (.85f + .11f * index) + 1.7f * index;
            float rise = Mathf.Sin(phase);
            float sway = Mathf.Cos(phase * .77f + .4f);
            Vector3 offset = drift * new Vector3(.09f * sway, (.27f - .015f * index) * rise, .06f * sway);
            Quaternion tilt = Quaternion.Euler(.7f * sway * drift, 0f, 1.05f * rise * drift);
            island.SetLocalPositionAndRotation(shore + offset - Vector3.up * (recede * RecedeDistance), heading * tilt);
        }

        internal static float Ease(float amount) => amount * amount * (3f - 2f * amount);
    }
}
