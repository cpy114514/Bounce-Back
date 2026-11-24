using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    // The original courtyard owns its existing fold and carrier. Transfer only once it is back at its dock.
    internal sealed class CampaignLevelHandoff : MonoBehaviour
    {
        private CampaignPlayground courtyard;
        private int completedOnEntry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= Connect;
            SceneManager.sceneLoaded += Connect;
        }

        private static void Connect(Scene scene, LoadSceneMode mode)
        {
            if (!CampaignScenes.FullCampaign || scene.name != CampaignScenes.SystemsScene)
                return;
            foreach (var root in scene.GetRootGameObjects())
            {
                var flow = root.GetComponentInChildren<CampaignPlayground>();
                if (flow)
                {
                    flow.gameObject.AddComponent<CampaignLevelHandoff>().Bind(flow);
                    return;
                }
            }
        }

        private void Bind(CampaignPlayground flow)
        {
            courtyard = flow;
            completedOnEntry = CampaignProgress.Load().CompletedCount;
            flow.TransitionFinished += OnDocked;
        }

        private void OnDocked(int district)
        {
            if (!courtyard || courtyard.InPuzzle || !courtyard.city.Active)
                return;
            bool firstClear = completedOnEntry == 0 && CampaignProgress.Load().IsCompleted(0);
            if (CampaignScenes.OpenWorld(district, firstClear))
                courtyard.city.Suspend();
        }

        private void OnDestroy()
        {
            if (courtyard)
                courtyard.TransitionFinished -= OnDocked;
        }

    }
}
