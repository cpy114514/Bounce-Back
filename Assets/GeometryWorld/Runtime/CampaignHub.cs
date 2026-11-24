using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public enum CampaignPhase { LevelSelect, Entering, Preparation, Playing, Failed, Paused, Completing, Returning }

    [Serializable]
    public sealed class CampaignLevelDestination
    {
        [Tooltip("Optional scene path from Build Profiles. An empty or unavailable scene uses the shared systems demonstration.")]
        public string scenePath;
    }

    [DisallowMultipleComponent]
    public sealed class CampaignHub : MonoBehaviour
    {
        public CityRail city;
        public BallBody ball;
        public PuzzleCameraRig cameraRig;
        public AdaptivePuzzleAudio audioDirector;
        public CampaignLevelDestination[] destinations = Array.Empty<CampaignLevelDestination>();
        public Transform carrier;
        public Transform demonstration;
        public Transform trialStart;
        public Transform trialEnd;
        public Transform trialView;
        [Min(1f)] public float trialSize = 5.5f;
        public GeometryPose trialArchitecture;
        public GeometryPose completionRipple;
        [Min(0.1f)] public float entrySeconds = 2f;
        [Min(0.1f)] public float exitSeconds = 1.1f;
        public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public CampaignPhase Phase { get; private set; }
        public bool Ready { get; private set; }
        public bool HasSceneFallback { get; private set; }
        public bool SystemsPreview { get; private set; }
        public event Action<CampaignPhase> PhaseChanged;

        private CampaignProgress progress;
        private int selected;
        private float runTime;
        private CampaignPhase beforePause;
        private Vector3 demonstrationOrigin;

        private void Start()
        {
            if (!city || !ball || !cameraRig || !carrier || !demonstration || !trialStart || !trialEnd || !trialView ||
                city.waypoints.Length < 2 || city.districts.Length == 0)
            {
                Debug.LogError("Campaign City needs a route, districts, a camera, the carrier and the demonstration anchors.", this);
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            SystemsPreview = CampaignScenes.UsingSystemsPreview || !CampaignScenes.FullCampaign;
#endif
            CampaignScenes.UseFullCampaign(preview: SystemsPreview);
            CampaignSettings.Apply();
            progress = CampaignProgress.Load(city.districts.Length, SystemsPreview);
            ball.Initialize(null, 1f);
            ball.Body.detectCollisions = false;
            cameraRig.Cinematic = true;
            city.Setup(progress, ball, cameraRig, audioDirector);
            city.EnterRequested += OnEnter;
            demonstrationOrigin = demonstration.position;
            city.RefreshProgress();
            var arrival = CampaignScenes.ConsumeHubArrival();
            city.Assemble(arrival.reveal ? 0f : 1f, false);
            selected = Mathf.Clamp(arrival.index < 0 ? progress.Position : arrival.index, 0,
                Mathf.Min(progress.CompletedCount, city.districts.Length - 1));
            ball.Hold(city.Dock(selected), Quaternion.identity);
            city.Frame(city.Dock(selected), true);
            Ready = true;
            if (arrival.reveal)
                StartCoroutine(ShowNewRoad(true));
            else
                RestoreCity(true, progress.CompletedCount > 0);
        }

        private void OnEnable()
        {
            if (Ready)
                RestoreCity(true, progress.CompletedCount > 0);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (Ready && city)
                city.Suspend();
        }

        private void OnDestroy()
        {
            if (city)
                city.EnterRequested -= OnEnter;
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                if (Phase is CampaignPhase.Preparation or CampaignPhase.Playing or CampaignPhase.Failed or CampaignPhase.Paused)
                    TogglePause();
            }
            if (Phase == CampaignPhase.Preparation && keys?.spaceKey.wasPressedThisFrame == true)
                Release();
            if (Phase != CampaignPhase.Playing)
                return;
            runTime = Mathf.MoveTowards(runTime, 1f, Time.unscaledDeltaTime / 1.8f);
            CityRail.Roll(ball, Vector3.Lerp(trialStart.position, trialEnd.position, Ease(runTime)));
            Vector3 offset = trialView.position - trialStart.position;
            cameraRig.Move(ball.Body.position + offset, trialView.rotation, trialSize);
            if (runTime >= 1f)
                Complete();
        }

        public bool Release()
        {
            if (Phase != CampaignPhase.Preparation)
                return false;
            runTime = 0;
            ChangePhase(CampaignPhase.Playing);
            CueSound(Cue.Release);
            return true;
        }

        public bool ReturnToCity()
        {
            if (Phase is not (CampaignPhase.Preparation or CampaignPhase.Playing or CampaignPhase.Failed or CampaignPhase.Paused))
                return false;
            StartCoroutine(Leave(false, false));
            return true;
        }

        public bool ReportFailure()
        {
            if (Phase != CampaignPhase.Playing)
                return false;
            ball.Hold(trialStart.position, Quaternion.identity);
            ChangePhase(CampaignPhase.Failed);
            CueSound(Cue.Retry);
            return true;
        }

        public bool PrepareAgain()
        {
            if (Phase != CampaignPhase.Failed)
                return false;
            ChangePhase(CampaignPhase.Preparation);
            return true;
        }

        private void Complete()
        {
            bool advanced = progress.Complete(selected);
            progress.MoveTo(selected);
            progress.Save();
            StartCoroutine(Leave(true, advanced && selected == 0));
        }

        private void TogglePause()
        {
            if (Phase == CampaignPhase.Paused)
                ChangePhase(beforePause);
            else
            {
                beforePause = Phase;
                ChangePhase(CampaignPhase.Paused);
            }
        }

        private void OnEnter(int index)
        {
            if (Phase != CampaignPhase.LevelSelect)
                return;
            selected = index;
            StartCoroutine(Enter());
        }

        private IEnumerator Enter()
        {
            ChangePhase(CampaignPhase.Entering);
            CueSound(Cue.ArchitectureLayer);
            var district = city.districts[selected];
            string destination = selected < destinations.Length ? destinations[selected]?.scenePath : null;
            bool external = !string.IsNullOrWhiteSpace(destination) && Application.CanStreamedLevelBeLoaded(destination);
            bool showingDemonstration = SystemsPreview || !external;
            HasSceneFallback = !string.IsNullOrWhiteSpace(destination) && !external;
            PrepareTrial();
            demonstration.gameObject.SetActive(showingDemonstration);
            yield return Animate(0f, 1f, entrySeconds, district.unfolding, showingDemonstration);
            if (!showingDemonstration && CampaignScenes.EnterExternal(destination, selected))
                yield break;
            if (!showingDemonstration)
            {
                HasSceneFallback = true;
                demonstration.gameObject.SetActive(true);
                trialArchitecture?.Apply(1f);
            }
            carrier.gameObject.SetActive(false);
            ball.Hold(trialStart.position, ball.Body.rotation);
            ChangePhase(CampaignPhase.Preparation);
            CueSound(Cue.TransitionEnd);
        }

        private IEnumerator Leave(bool completed, bool reveal)
        {
            ChangePhase(completed ? CampaignPhase.Completing : CampaignPhase.Returning);
            CueSound(completed ? Cue.Goal : Cue.ArchitectureLayer);
            if (completed && completionRipple)
            {
                completionRipple.gameObject.SetActive(true);
                yield return ArchitectureMotion.Play(0f, 1f, 0.65f, t => completionRipple.Apply(Ease(t)));
                completionRipple.gameObject.SetActive(false);
            }
            yield return Animate(1f, 0f, exitSeconds, city.districts[selected].unfolding, true);
            if (reveal)
                yield return ShowNewRoad(true);
            else
                RestoreCity(false, progress.CompletedCount > 0);
        }

        private IEnumerator ShowNewRoad(bool rollForward)
        {
            ChangePhase(CampaignPhase.Completing);
            city.Suspend();
            city.RefreshProgress();
            yield return ArchitectureMotion.Play(0f, 1f, 1.3f, t => city.Assemble(t, true));
            RestoreCity(false, true);
            if (rollForward && selected == 0 && progress.IsUnlocked(1))
                city.Step(1, false);
        }

        private void RestoreCity(bool snap, bool showHint)
        {
            carrier.gameObject.SetActive(false);
            demonstration.gameObject.SetActive(false);
            if (completionRipple)
                completionRipple.gameObject.SetActive(false);
            foreach (var district in city.districts)
                district.unfolding?.Apply(0f);
            trialArchitecture?.Apply(0f);
            city.Assemble(1f, false);
            city.RefreshProgress();
            city.Open(selected, showHint, snap);
            ChangePhase(CampaignPhase.LevelSelect);
            CueSound(Cue.TransitionEnd);
        }

        private void PrepareTrial()
        {
            demonstration.position = demonstrationOrigin + city.Dock(selected) - city.Dock(0);
            trialArchitecture?.Apply(0f);
            if (completionRipple)
                completionRipple.gameObject.SetActive(false);
        }

        private IEnumerator Animate(float from, float to, float seconds, GeometryPose building, bool showTrial)
        {
            Vector3 startBall = ball.Body.position;
            Vector3 endBall = to > from ? trialStart.position : city.Dock(selected);
            Vector3 beginView = cameraRig.view.transform.position;
            Quaternion beginRotation = cameraRig.view.transform.rotation;
            float beginSize = cameraRig.view.orthographicSize;
            Vector3 endView = to > from ? trialView.position : endBall + city.cameraOffset;
            Quaternion endRotation = to > from ? trialView.rotation : Quaternion.LookRotation(-city.cameraOffset);
            carrier.gameObject.SetActive(true);
            yield return ArchitectureMotion.Play(0f, 1f, seconds, t =>
            {
                float eased = Ease(t);
                float pose = Mathf.Lerp(from, to, eased);
                building?.Apply(pose);
                if (showTrial)
                    trialArchitecture?.Apply(pose);
                Vector3 point = Vector3.Lerp(startBall, endBall, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.3f);
                carrier.position = point - Vector3.up * (ball.transform.localScale.x * 0.5f);
                ball.Body.position = point;
                cameraRig.Move(Vector3.Lerp(beginView, endView, eased), Quaternion.Slerp(beginRotation, endRotation, eased),
                    Mathf.Lerp(beginSize, to > from ? trialSize : city.cameraSize, eased), true);
            });
        }

        private float Ease(float t) => Mathf.Clamp01(easing == null ? t : easing.Evaluate(t));
        private void CueSound(Cue cue) { if (audioDirector) audioDirector.Play(cue); }

        private void ChangePhase(CampaignPhase next)
        {
            if (Phase == next)
                return;
            Phase = next;
            PhaseChanged?.Invoke(next);
        }

        private void OnGUI()
        {
            if (!Ready || Phase == CampaignPhase.LevelSelect || CampaignSceneParticles.IsRunning)
                return;
            var matrix = GUI.matrix;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                float center = width * 0.5f;
                float bottom = height - 68f;
                if (Phase == CampaignPhase.Preparation || Phase == CampaignPhase.Failed)
                {
                    CourtyardHud.TagLabel(new Rect(center - 137f, bottom - 26f, 274f, 25f), HasSceneFallback ? "Scene unavailable · Systems preview" : "Systems preview · Preparation");
                    if (GUI.Button(new Rect(center - 69f, bottom + 5f, 138f, 32f), Phase == CampaignPhase.Failed ? "Prepare Again" : "Release  Space", CourtyardHud.Primary))
                    {
                        if (Phase == CampaignPhase.Failed)
                            PrepareAgain();
                        else
                            Release();
                    }
                }
                if (Phase is CampaignPhase.Preparation or CampaignPhase.Playing or CampaignPhase.Failed)
                {
                    if (GUI.Button(CampaignPlayground.PauseButtonRect(width), "Pause  Esc", CourtyardHud.Button))
                        TogglePause();
                }
                if (Phase == CampaignPhase.Paused)
                {
                    var action = CourtyardHud.PauseMenu(width, height, true);
                    if (action == PauseAction.Resume) TogglePause();
                    if (action == PauseAction.LevelSelect) ReturnToCity();
                    if (action == PauseAction.MainMenu) CampaignScenes.OpenMenu();
                }
                if (CampaignScenes.IsLoading)
                    CourtyardHud.TagLabel(new Rect(center - 63f, bottom + 12f, 126f, 30f), "Loading…");
            }
            finally
            {
                GUI.matrix = matrix;
            }
        }
    }
}
