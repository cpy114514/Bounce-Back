using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class WorldEntrance : MonoBehaviour
    {
        [Range(0, CampaignProgress.LevelCount - 1)] public int levelIndex;
        public string districtName;
        public Transform solid;
        public GameObject wire;
        public GeometryPose completedPose;
        public AudioSource ambience;
        public Color ambientLight = new(0.7f, 0.76f, 0.86f);

        private Vector3 size;
        private float amount;
        private float ambienceVolume;
        private bool revealing;

        private void Awake()
        {
            if (solid)
                size = solid.localScale;
            if (ambience)
            {
                ambienceVolume = ambience.volume;
                ambience.volume = 0f;
            }
        }

        public void ShowProgress(CampaignProgress progress, bool instant)
        {
            bool unlocked = progress.IsUnlocked(levelIndex);
            revealing = unlocked && !instant && levelIndex == progress.CompletedCount && levelIndex > 0;
            amount = revealing ? 0f : 1f;
            if (solid)
            {
                solid.gameObject.SetActive(unlocked);
                ApplyReveal();
            }
            if (wire)
                wire.SetActive(!unlocked || revealing);
            if (completedPose)
                completedPose.Apply(progress.IsCompleted(levelIndex) ? 1f : 0f);
        }

        private void Update()
        {
            if (!revealing)
                return;
            amount = Mathf.MoveTowards(amount, 1f, Time.unscaledDeltaTime / 1.8f);
            if (solid)
                ApplyReveal();
            if (amount < 1f)
                return;
            revealing = false;
            if (wire)
                wire.SetActive(false);
        }

        private void ApplyReveal() => solid.localScale = Vector3.Scale(size,
            new Vector3(1f, Mathf.SmoothStep(0.03f, 1f, amount), 1f));

        internal void UpdateAmbience(bool nearby, bool audioReady)
        {
            if (!audioReady || !ambience || !ambience.clip)
                return;
            if (nearby && !ambience.isPlaying)
            {
                ambience.loop = true;
                ambience.Play();
            }
            ambience.volume = Mathf.MoveTowards(ambience.volume, nearby ? ambienceVolume : 0f, Time.unscaledDeltaTime * 0.7f);
            if (!nearby && ambience.volume <= 0f && ambience.isPlaying)
                ambience.Stop();
        }
    }
}
