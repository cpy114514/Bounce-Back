using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MenuSettingsDrawer : MonoBehaviour
    {
        public Transform tray;
        public Transform[] runners, catches;
        public MenuControl[] controls;

        private Vector3 trayRest;
        private Vector3[] controlRests;

        private void Awake()
        {
            trayRest = tray.localPosition;
            controlRests = new Vector3[controls.Length];
            for (int i = 0; i < controls.Length; i++)
                controlRests[i] = controls[i].transform.localPosition;
        }

        internal void Pose(float amount)
        {
            float unlock = Phase(.02f, .23f);
            float travel = Phase(.14f, .86f);
            float lift = Phase(.75f, 1f);
            tray.localPosition = trayRest + Vector3.back * (3.38f * travel);
            for (int i = 0; i < runners.Length; i++)
            {
                runners[i].localPosition = new Vector3(i == 0 ? -1.89f : 1.89f, -.65f, -1.18f - 1.69f * travel);
                runners[i].localScale = new Vector3(.15f, .13f, 2.15f + 3.38f * travel);
                catches[i].localRotation = Quaternion.Euler(unlock * -88f, 0f, 0f);
            }
            for (int i = 0; i < controls.Length; i++)
            {
                Transform liftPlatform = controls[i].transform;
                liftPlatform.localPosition = controlRests[i] - Vector3.up * (.34f * (1f - lift));
                liftPlatform.localScale = new Vector3(1f, Mathf.Lerp(.38f, 1f, lift), 1f);
            }

            float Phase(float start, float end) => MenuIsland.Ease(Mathf.InverseLerp(start, end, amount));
        }
    }
}
