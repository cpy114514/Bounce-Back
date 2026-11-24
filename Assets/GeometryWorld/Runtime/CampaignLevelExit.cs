using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    [RequireComponent(typeof(GeometryLevel))]
    public sealed class CampaignLevelExit : MonoBehaviour
    {
        [Tooltip("Use the selected entrance index for the shared prototype scene.")]
        public bool useSelectedEntrance;
        public GeometryPose completion;
        [Min(0.1f)] public float duration = 2.8f;

        private GeometryLevel level;
        private bool finishing, hadHud;

        private void OnEnable()
        {
            level = GetComponent<GeometryLevel>();
            level.GoalReached += OnGoal;
        }

        private void OnDisable()
        {
            level.GoalReached -= OnGoal;
            StopAllCoroutines();
            if (finishing)
            {
                level.InputEnabled = true;
                level.showPlayHud = hadHud;
            }
            finishing = false;
        }

        private void OnGoal(GeometryLevel completed)
        {
            if (finishing || gameObject.scene != SceneManager.GetActiveScene())
                return;
            int index = useSelectedEntrance ? CampaignScenes.CurrentLevel : 0;
            if (useSelectedEntrance && index == 0)
                return;
            var progress = CampaignProgress.Load();
            bool unlocked = progress.Complete(index);
            if (unlocked)
                progress.Save();
            if (!Application.CanStreamedLevelBeLoaded(CampaignScenes.World))
                return;
            finishing = true;
            hadHud = level.showPlayHud;
            level.InputEnabled = false;
            level.showPlayHud = false;
            StartCoroutine(PlayCompletion(index, unlocked));
        }

        private IEnumerator PlayCompletion(int index, bool unlocked)
        {
            float durationSeconds = CampaignSettings.ReducedMotion ? 0.25f : Mathf.Max(0.1f, duration);
            Camera camera = level.gameplayCamera;
            float initialSize = camera ? camera.orthographicSize : 0f;
            for (float elapsed = 0f; elapsed < durationSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float amount = Mathf.SmoothStep(0f, 1f, elapsed / durationSeconds);
                if (completion)
                    completion.Apply(amount);
                if (camera && !useSelectedEntrance && camera.orthographic)
                    camera.orthographicSize = initialSize * Mathf.Lerp(1f, 1.22f, amount);
                yield return null;
            }
            if (completion)
                completion.Apply(1f);
            CampaignScenes.OpenWorld(index, unlocked);
        }
    }
}
