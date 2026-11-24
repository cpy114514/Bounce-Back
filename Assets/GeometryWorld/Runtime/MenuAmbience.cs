using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    /// <summary>Idle breathing for the title courtyard: a slow camera drift and a floating-island bob.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class MenuAmbience : MonoBehaviour
    {
        public GeometricMenu menu;
        public Transform island;
        [Min(0f)] public float cameraYaw = 1.6f;
        [Min(0f)] public float cameraPitch = 0.6f;
        [Min(0f)] public float islandBob = 0.06f;
        [Min(0f)] public float islandSway = 0.35f;
        [Min(1f)] public float period = 9f;
        [Min(0.05f)] public float fade = 0.6f;

        public Transform gulls;
        [Min(0f)] public float gullRadius = 1.4f;

        private Transform[] gullBirds = System.Array.Empty<Transform>();
        private Vector3[] gullCenters = System.Array.Empty<Vector3>();
        private Quaternion[] gullFacing = System.Array.Empty<Quaternion>();
        private Vector3 cameraFocus;
        private Quaternion appliedCamera = Quaternion.identity;
        private Vector3 appliedIsland;
        private Quaternion appliedIslandRotation = Quaternion.identity;
        private float weight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (_, _) => Attach();
            Attach();
        }

        private static void Attach()
        {
            foreach (var menu in FindObjectsByType<GeometricMenu>(FindObjectsSortMode.None))
                if (!menu.GetComponent<MenuAmbience>())
                {
                    var ambience = menu.gameObject.AddComponent<MenuAmbience>();
                    ambience.menu = menu;
                    ambience.island = menu.transform.Find("Menu Architecture");
                    ambience.gulls = menu.transform.Find("Dressed Seagulls");
                }
        }

        private void Start()
        {
            if (!gulls)
                return;
            int count = gulls.childCount;
            gullBirds = new Transform[count];
            gullCenters = new Vector3[count];
            gullFacing = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                gullBirds[i] = gulls.GetChild(i);
                gullCenters[i] = gullBirds[i].position;
                gullFacing[i] = gullBirds[i].rotation;
            }
        }

        private void FlyGulls()
        {
            float time = Time.unscaledTime;
            bool still = CampaignSettings.ReducedMotion;
            for (int i = 0; i < gullBirds.Length; i++)
            {
                if (!gullBirds[i])
                    continue;
                float speed = 0.32f + i * 0.07f;
                float angle = still ? 0f : time * speed * (i % 2 == 0 ? 1f : -1f) + i * 2.1f;
                var orbit = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-orbit.z, 0f, orbit.x) * (i % 2 == 0 ? 1f : -1f);
                float bob = still ? 0f : Mathf.Sin(time * 1.7f + i) * 0.18f;
                gullBirds[i].position = gullCenters[i] + orbit * gullRadius + Vector3.up * bob;
                gullBirds[i].rotation = still ? gullFacing[i] :
                    Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 14f);
            }
        }

        // Runs after GeometricMenu.Update but before its coroutines, so menu animations always see the authored pose.
        private void Update() => Restore();

        private void LateUpdate()
        {
            FlyGulls();
            if (!menu || !menu.view)
                return;
            bool idle = menu.NavigationAvailable && !CampaignSettings.ReducedMotion;
            weight = Mathf.MoveTowards(weight, idle ? 1f : 0f, Time.unscaledDeltaTime / fade);
            float w = Mathf.SmoothStep(0f, 1f, weight);
            if (w <= 0f)
                return;
            float t = Time.unscaledTime * Mathf.PI * 2f / period;

            Transform view = menu.view.transform;
            cameraFocus = view.position + view.forward * 20f;
            appliedCamera = Quaternion.Euler(Mathf.Sin(t * 0.7f) * cameraPitch * w, Mathf.Sin(t) * cameraYaw * w, 0f);
            Vector3 arm = view.position - cameraFocus;
            view.SetPositionAndRotation(cameraFocus + appliedCamera * arm, appliedCamera * view.rotation);

            if (island)
            {
                appliedIsland = Vector3.up * (Mathf.Sin(t * 1.3f + 0.8f) * islandBob * w);
                appliedIslandRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.9f) * islandSway * w);
                island.SetLocalPositionAndRotation(island.localPosition + appliedIsland,
                    appliedIslandRotation * island.localRotation);
            }
        }

        private void Restore()
        {
            if (menu && menu.view && appliedCamera != Quaternion.identity)
            {
                Transform view = menu.view.transform;
                Quaternion undo = Quaternion.Inverse(appliedCamera);
                view.SetPositionAndRotation(cameraFocus + undo * (view.position - cameraFocus), undo * view.rotation);
            }
            appliedCamera = Quaternion.identity;
            if (island && (appliedIsland != Vector3.zero || appliedIslandRotation != Quaternion.identity))
                island.SetLocalPositionAndRotation(island.localPosition - appliedIsland,
                    Quaternion.Inverse(appliedIslandRotation) * island.localRotation);
            appliedIsland = Vector3.zero;
            appliedIslandRotation = Quaternion.identity;
        }

        private void OnDisable() => Restore();
    }
}
