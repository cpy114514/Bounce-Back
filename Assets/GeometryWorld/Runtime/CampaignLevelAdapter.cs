using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    [DisallowMultipleComponent]
    public sealed class CampaignLevelAdapter : MonoBehaviour
    {
        [Tooltip("Optional GeometryLevel for automatic Release and GoalReached integration.")]
        public GeometryLevel puzzle;
        public GeometryPose exitMotion;
        public GeometryPose completionEffect;
        [Min(0.1f)] public float exitDuration = 0.85f;
        [Min(CampaignProgress.LevelCount)] public int levelCapacity = CampaignProgress.LevelCount;
        [Tooltip("When set, only use this adapter after entering the matching campaign slot.")]
        [Min(-1)] public int requiredCampaignSlot = -1;
        public CampaignPhase Phase { get; private set; } = CampaignPhase.Entering;
        public int Slot => CampaignScenes.CurrentLevel;
        public event Action<CampaignPhase> PhaseChanged;

        private bool subscribed;
        private bool inputBeforeExit;
        private bool hudBeforeExit;
        private bool wasPaused;
        private float timeBeforePause;
        private CampaignPhase beforePause;

        private void OnEnable()
        {
            if (requiredCampaignSlot >= 0 && (!CampaignScenes.FullCampaign || Slot != requiredCampaignSlot))
            {
                enabled = false;
                return;
            }
            if (!puzzle)
                return;
            if (GetComponentInParent<CampaignPlayground>() || puzzle.GetComponent<CampaignPlayground>() ||
                puzzle.GetComponent<CampaignLevelExit>())
            {
                Debug.LogWarning("Remove the duplicate campaign completion adapter. This level already owns its exit.", this);
                enabled = false;
                return;
            }
            puzzle.BallReleased += OnRelease;
            puzzle.GoalReached += OnComplete;
            subscribed = true;
            if (puzzle.spawn)
                StartCoroutine(ReadyWhenSpawned());
        }

        private IEnumerator ReadyWhenSpawned()
        {
            while (puzzle && !puzzle.Ball)
                yield return null;
            if (puzzle && puzzle.Ball && !puzzle.Ball.Released)
                ReportPrepared();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (subscribed && puzzle)
            {
                puzzle.BallReleased -= OnRelease;
                puzzle.GoalReached -= OnComplete;
            }
            subscribed = false;
            if (wasPaused)
                RestorePause();
            if (puzzle && Phase is CampaignPhase.Completing or CampaignPhase.Returning)
            {
                puzzle.InputEnabled = inputBeforeExit;
                puzzle.showPlayHud = hudBeforeExit;
            }
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                TogglePause();
        }

        public void ReportPrepared()
        {
            if (Phase is CampaignPhase.Entering or CampaignPhase.Failed)
                SetPhase(CampaignPhase.Preparation);
        }

        public void ReportReleased()
        {
            if (Phase == CampaignPhase.Preparation)
                SetPhase(CampaignPhase.Playing);
        }

        public void ReportFailed()
        {
            if (Phase == CampaignPhase.Playing)
                SetPhase(CampaignPhase.Failed);
        }

        public bool ReportCompleted()
        {
            if (Phase != CampaignPhase.Playing)
                return false;
            var save = CampaignProgress.Load(levelCapacity);
            bool advanced = save.Complete(Slot);
            save.MoveTo(Slot);
            save.Save();
            StartCoroutine(Exit(true, advanced && Slot == 0));
            return true;
        }

        public bool ReturnToCity()
        {
            if (Phase is not (CampaignPhase.Preparation or CampaignPhase.Playing or CampaignPhase.Failed or CampaignPhase.Paused))
                return false;
            RestorePause();
            if (Phase == CampaignPhase.Paused)
                SetPhase(beforePause);
            StartCoroutine(Exit(false, false));
            return true;
        }

        private IEnumerator Exit(bool completed, bool reveal)
        {
            var previous = Phase;
            SetPhase(completed ? CampaignPhase.Completing : CampaignPhase.Returning);
            if (puzzle)
            {
                inputBeforeExit = puzzle.InputEnabled;
                hudBeforeExit = puzzle.showPlayHud;
                puzzle.InputEnabled = false;
                puzzle.showPlayHud = false;
                if (puzzle.Ball)
                    puzzle.Ball.Hold(puzzle.Ball.Body.position, puzzle.Ball.Body.rotation);
            }
            yield return ArchitectureMotion.Play(0f, 1f, exitDuration, t =>
            {
                float pose = Mathf.SmoothStep(0f, 1f, t);
                exitMotion?.Apply(pose);
                if (completed)
                    completionEffect?.Apply(pose);
            });
            if (CampaignScenes.OpenWorld(Slot, reveal))
                yield break;
            exitMotion?.Apply(0f);
            completionEffect?.Apply(0f);
            if (puzzle)
            {
                puzzle.InputEnabled = inputBeforeExit;
                puzzle.showPlayHud = hudBeforeExit;
                puzzle.ResetBall();
            }
            SetPhase(puzzle ? CampaignPhase.Preparation : completed ? CampaignPhase.Failed : previous);
        }

        internal void TogglePause()
        {
            if (CampaignSceneParticles.IsRunning)
                return;
            if (Phase == CampaignPhase.Paused)
            {
                RestorePause();
                SetPhase(beforePause);
            }
            else if (Phase is CampaignPhase.Preparation or CampaignPhase.Playing or CampaignPhase.Failed)
            {
                beforePause = Phase;
                timeBeforePause = Time.timeScale;
                Time.timeScale = 0f;
                if (puzzle)
                {
                    inputBeforeExit = puzzle.InputEnabled;
                    puzzle.InputEnabled = false;
                }
                wasPaused = true;
                SetPhase(CampaignPhase.Paused);
            }
        }

        private void RestorePause()
        {
            if (!wasPaused)
                return;
            Time.timeScale = timeBeforePause;
            if (puzzle)
                puzzle.InputEnabled = inputBeforeExit;
            wasPaused = false;
        }

        private void SetPhase(CampaignPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(Phase);
        }

        private void OnRelease(BallBody _) => ReportReleased();
        private void OnComplete(GeometryLevel _) => ReportCompleted();

        private void OnGUI()
        {
            if (Phase != CampaignPhase.Paused)
                return;
            var matrix = GUI.matrix;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                var action = CourtyardHud.PauseMenu(Screen.width / scale, Screen.height / scale, true);
                if (action == PauseAction.Resume) TogglePause();
                if (action == PauseAction.LevelSelect) ReturnToCity();
                if (action == PauseAction.MainMenu && CampaignScenes.OpenMenu()) RestorePause();
            }
            finally
            {
                GUI.matrix = matrix;
            }
        }
    }
}
