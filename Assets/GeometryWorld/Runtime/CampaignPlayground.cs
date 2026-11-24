using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    [Serializable]
    public sealed class CampaignPlayStage
    {
        public Transform root;
        public GameObject physics;
        public GameObject mechanisms;
        public GameObject goalDisplay;
        public BallSpawn spawn;
        public PuzzleTrigger goal;
        public Transform overview;
        public float overviewSize;
        public float followSize;
        public GeometryPose folding;
    }

    public sealed class CampaignPlayground : MonoBehaviour
    {
        public GeometryLevel level;
        public PuzzleCameraRig rig;
        public CityRail city;
        public CampaignPlayStage santorini;

        public Transform startingCarrier;
        public GoalPressurePlate completionPlate;
        public CourtyardFinale finale;
        public Transform carrierOverlook;
        public Transform carrierTower;
        [Min(0.2f)] public float completionDuration = 4.8f;
        [Min(0.2f)] public float entryDuration = 2.65f;
        [Min(0.2f)] public float returnDuration = 1.35f;

        [Min(0f)] public float settlePlateSeconds = 0.24f;
        [Range(0f, 0.95f)] public float roadReadyAt = 0.28f;
        [Range(0.3f, 0.98f)] public float rolloutAt = 0.86f;
        [Min(1f)] public float foldingCameraSize = 11.6f;
        public Transform CarryingPlatform { get; private set; }

        public bool InPuzzle { get; private set; }
        public bool InTransition { get; private set; }
        public int ActiveLevel { get; private set; }
        public event Action<int> TransitionFinished;
        public event Action<int> LevelUnlocked;
        public event Action<int> ArchitectureLayerMoved;

        private CampaignProgress progress;
        private Vector3 ballOnCarrier;
        private Vector3 towerBallRest;
        private bool paused, ready;
        private float priorTimeScale;

        private IEnumerator Start()
        {
            if (gameObject.scene != SceneManager.GetActiveScene())
                yield break;
            var menuHeading = CampaignScenes.ConsumeMenuArrival();
            if (menuHeading.HasValue)
            {
                InTransition = true;
                level.InputEnabled = false;
                rig.Cinematic = true;
                FrameMenuArrival(menuHeading.Value, 0f);
            }
            yield return null;
            if (!level.Ball)
                yield break;
            towerBallRest = santorini.spawn.StartPosition;
            ready = true;
            progress = CampaignProgress.Load();
            ActiveLevel = 0;
            city.Setup(progress, level.Ball, rig, level.audioDirector);
            city.EnterRequested += EnterStage;
            level.GoalReached += CompleteStage;
            startingCarrier.gameObject.SetActive(false);

            if (CampaignScenes.StartsInCity)
            {
                int position = CampaignScenes.WorldEntrance >= 0 ? CampaignScenes.WorldEntrance : progress.Position;
                position = Mathf.Clamp(position, 0, city.districts.Length - 1);
                PrepareCity(position, true);
                city.Assemble(1f, false);
                city.Open(position, false, true);
            }
            else
            {
                CampaignScenes.SelectLevel(0);
                if (menuHeading.HasValue)
                    yield return ArchitectureMotion.Play(0f, 1f, 1.12f, fraction => FrameMenuArrival(menuHeading.Value, fraction));
                InPuzzle = true;
                if (menuHeading.HasValue)
                {
                    rig.Cinematic = false;
                    level.InputEnabled = true;
                    EndTransition();
                }
            }
        }

        private void FrameMenuArrival(Quaternion heading, float time)
        {
            float pullback = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 1f, time));
            float distance = rig.followOffset.magnitude;
            Quaternion rotation = Quaternion.Slerp(heading, rig.overview.rotation, pullback);
            Vector3 overviewFocus = rig.overview.position + rig.overview.forward * distance;
            Vector3 focus = Vector3.Lerp(santorini.spawn.StartPosition, overviewFocus, pullback);
            float closeSize = Mathf.Max(0.1f, santorini.spawn.diameter * 0.5f * CampaignScenes.ArrivalFraming);
            float size = Mathf.Exp(Mathf.Lerp(Mathf.Log(closeSize), Mathf.Log(rig.overviewSize), pullback));
            rig.Move(focus - rotation * Vector3.forward * distance, rotation, size, true);
        }

        private void OnDestroy()
        {
            if (!ready)
                return;
            SetPause(false);
            city.EnterRequested -= EnterStage;
            level.GoalReached -= CompleteStage;
        }

        private void Update()
        {
            if (InPuzzle && !InTransition && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                SetPause(!paused);
        }

        public void SetPause(bool enabled)
        {
            if (enabled == paused || enabled && (!ready || !InPuzzle || InTransition))
                return;
            if (enabled)
            {
                priorTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                level.InputEnabled = false;
            }
            else
            {
                Time.timeScale = priorTimeScale;
                level.InputEnabled = true;
            }
            paused = enabled;
        }

        public bool ReturnToCity()
        {
            if (!ready || !InPuzzle || InTransition)
                return false;
            SetPause(false);
            StartCoroutine(LeaveStage(false, false));
            return true;
        }

        private void CompleteStage(GeometryLevel completed)
        {
            if (InTransition || !InPuzzle)
                return;
            bool advanced = progress.Complete(ActiveLevel);
            progress.MoveTo(ActiveLevel);
            if (advanced && ActiveLevel == 0)
                progress.Materialize(0);
            progress.Save();
            StartCoroutine(LeaveStage(true, advanced && ActiveLevel == 0));
            if (advanced && ActiveLevel + 1 < CampaignProgress.LevelCount)
                LevelUnlocked?.Invoke(ActiveLevel + 1);
        }

        private void PrepareCity(int index, bool placeAtDock)
        {
            city.gameObject.SetActive(true);
            city.Suspend();
            city.RefreshProgress();
            SetSimulation(santorini, false);
            if (finale)
                finale.SnapFolded();
            else
                santorini.folding.Apply(1f);
            level.InputEnabled = false;
            rig.Cinematic = true;
            if (placeAtDock)
                level.Ball.Hold(city.Dock(index), level.Ball.Body.rotation);
        }

        private IEnumerator LeaveStage(bool completed, bool intro)
        {
            InTransition = true;
            InPuzzle = false;
            level.InputEnabled = false;
            rig.Cinematic = true;
            var ball = level.Ball;
            if (level.retryEffect)
                level.retryEffect.Cancel();
            ball.Hold(ball.Body.position, ball.Body.rotation);
            city.gameObject.SetActive(true);
            city.Suspend();
            city.RefreshProgress();
            city.Assemble(intro ? 0f : 1f, false);

            Vector3 initialOffset = -rig.overview.forward * rig.followOffset.magnitude;
            Vector3 initialFocus = rig.overview.position - initialOffset;
            if (completed && finale)
            {
                finale.Begin();
                finale.FrameBall(rig, initialFocus, initialOffset, rig.view.orthographicSize, ball.Body.position);
                float plateStarted = Time.unscaledTime;
                while (Time.unscaledTime - plateStarted < finale.plateHold)
                    yield return null;
            }
            else if (completed && settlePlateSeconds > 0f)
                yield return new WaitForSecondsRealtime(settlePlateSeconds);

            SetSimulation(santorini, false);
            ArchitectureLayerMoved?.Invoke(0);
            Vector3 dock = city.Dock(0);
            if (completed)
                AttachBall(completionPlate.surface);
            else
            {
                startingCarrier.position = ball.Body.position - Vector3.up * Radius;
                startingCarrier.gameObject.SetActive(true);
                AttachBall(startingCarrier);
            }

            Vector3 carrierOrigin = CarryingPlatform.position;
            Vector3 end = dock - Vector3.up * Radius;
            Vector3 viewStart = rig.view.transform.position;
            Quaternion rotationStart = rig.view.transform.rotation;
            Quaternion rotationEnd = Quaternion.LookRotation(-city.cameraOffset);
            float startSize = rig.view.orthographicSize;
            float duration = completed && finale ? finale.SequenceDuration : Seconds(intro ? completionDuration : returnDuration);
            float rolloutTime = Mathf.Clamp(rolloutAt, roadReadyAt + 0.01f, 0.99f);
            Vector3 rolloutStart = default;
            bool rolloutCaptured = false;
            float sequenceStarted = Time.unscaledTime;
            for (float elapsed = 0f; elapsed < duration; elapsed = completed && finale ?
                Time.unscaledTime - sequenceStarted : elapsed + Time.unscaledDeltaTime)
            {
                if (completed && finale)
                {
                    float beat = elapsed * finale.SequenceRate;
                    finale.Evaluate(beat);
                    if (CarryingPlatform)
                        FollowCarrier();
                    if (!rolloutCaptured && beat >= finale.RolloutStart)
                    {
                        rolloutCaptured = true;
                        rolloutStart = ball.Body.position;
                        CarryingPlatform = null;
                    }
                    float rollout = CourtyardFinale.Ease(Mathf.InverseLerp(finale.RolloutStart, finale.SequenceSeconds, beat));
                    if (rolloutCaptured)
                        CityRail.Roll(ball, Vector3.Lerp(rolloutStart, dock, rollout));
                    city.Assemble(Mathf.InverseLerp(finale.FoldComplete, finale.FoldComplete + finale.roadRevealDuration, beat), true);

                    float pullBack = CourtyardFinale.Ease(Mathf.InverseLerp(finale.FoldStart, finale.FoldComplete, beat));
                    float follow = CourtyardFinale.Ease(Mathf.InverseLerp(finale.FollowStart, finale.RolloutStart, beat));
                    float zoom = Mathf.Lerp(startSize, finale.foldingCameraSize, pullBack);
                    zoom = Mathf.Lerp(zoom, city.cameraSize, follow);
                    Vector3 offset = Quaternion.Slerp(rotationStart, rotationEnd, follow) * Vector3.back * initialOffset.magnitude;
                    Vector3 focus = Vector3.Lerp(initialFocus, dock, Mathf.Max(pullBack * 0.12f, follow));
                    finale.FrameBall(rig, focus, offset, zoom, ball.Body.position);
                }
                else
                {
                    float time = elapsed / duration;
                    if (intro)
                        city.Assemble(Mathf.InverseLerp(0f, roadReadyAt, time), true);
                    float fold = Ease(Mathf.InverseLerp(intro ? roadReadyAt : 0f, completed ? rolloutTime : 1f, time));
                    santorini.folding.Apply(fold);
                    if (!completed)
                        MoveCarrier(carrierOrigin, carrierTower.position, carrierOverlook.position, end, fold);
                    if (CarryingPlatform)
                    {
                        FollowCarrier();
                        if (completed && time >= rolloutTime)
                        {
                            rolloutStart = ball.Body.position;
                            CarryingPlatform = null;
                        }
                    }
                    if (completed && !CarryingPlatform)
                        CityRail.Roll(ball, Vector3.Lerp(rolloutStart, dock, Ease(Mathf.InverseLerp(rolloutTime, 1f, time))));
                    float camera = Ease(time);
                    float zoom = intro ? Mathf.Lerp(startSize, foldingCameraSize, camera) : Mathf.Lerp(startSize, city.cameraSize, camera);
                    rig.Move(Vector3.Lerp(viewStart, dock + city.cameraOffset, camera),
                        Quaternion.Slerp(rotationStart, rotationEnd, camera), zoom);
                }
                yield return null;
            }

            city.Assemble(1f, intro);
            santorini.folding.Apply(1f);
            if (!completed)
                startingCarrier.position = end;
            if (CarryingPlatform)
                FollowCarrier();
            CarryingPlatform = null;
            startingCarrier.gameObject.SetActive(false);
            PrepareCity(0, false);
            city.Open(0, intro, false);
            EndTransition();
        }

        private void EnterStage(int index)
        {
            if (!InTransition && index == 0)
                StartCoroutine(UnfoldStage());
        }

        private IEnumerator UnfoldStage()
        {
            InTransition = true;
            city.Suspend();
            CampaignScenes.SelectLevel(0);
            var district = city.districts[0];
            Vector3 viewStart = rig.view.transform.position;
            Quaternion rotationStart = rig.view.transform.rotation;
            float startSize = rig.view.orthographicSize;
            Vector3 start = level.Ball.Body.position - Vector3.up * Radius;
            Vector3 end = towerBallRest - Vector3.up * Radius;
            startingCarrier.position = start;
            startingCarrier.gameObject.SetActive(true);
            AttachBall(startingCarrier);
            float duration = Seconds(entryDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float open = Ease(elapsed / duration);
                santorini.folding.Apply(1f - open);
                if (district.unfolding)
                    district.unfolding.Apply(open);
                MoveCarrier(start, carrierOverlook.position, carrierTower.position, end, open);
                FollowCarrier();
                rig.Move(Vector3.Lerp(viewStart, santorini.overview.position, open),
                    Quaternion.Slerp(rotationStart, santorini.overview.rotation, open),
                    Mathf.Lerp(startSize, santorini.overviewSize, open));
                yield return null;
            }
            startingCarrier.position = end;
            FollowCarrier();
            CarryingPlatform = null;
            startingCarrier.gameObject.SetActive(false);
            santorini.folding.Apply(0f);
            if (district.unfolding)
                district.unfolding.Apply(0f);
            city.gameObject.SetActive(false);
            SetSimulation(santorini, true);
            rig.Cinematic = false;
            level.ResetBall(false);
            if (level.audioDirector)
                level.audioDirector.SetMusicSuppressed(false);
            if (finale)
                finale.ResetSequence();
            level.InputEnabled = true;
            InPuzzle = true;
            EndTransition();
        }

        private float Radius => level.spawn.diameter * 0.5f;

        private void AttachBall(Transform carrier)
        {
            CarryingPlatform = carrier;
            ballOnCarrier = Quaternion.Inverse(carrier.rotation) * (level.Ball.Body.position - carrier.position);
        }

        private void FollowCarrier() =>
            level.Ball.Body.position = CarryingPlatform.position + CarryingPlatform.rotation * ballOnCarrier;

        private void MoveCarrier(Vector3 from, Vector3 guideOne, Vector3 guideTwo, Vector3 to, float time)
        {
            float inverse = 1f - time;
            startingCarrier.position = from * (inverse * inverse * inverse) + guideOne * (3f * inverse * inverse * time) +
                guideTwo * (3f * inverse * time * time) + to * (time * time * time);
        }

        private void SetSimulation(CampaignPlayStage target, bool running)
        {
            target.physics.SetActive(running);
            foreach (var collider in target.mechanisms.GetComponentsInChildren<Collider>(true))
                collider.enabled = running;
            foreach (var collider in target.spawn.GetComponentsInChildren<Collider>(true))
                collider.enabled = running;
            target.goal.GetComponent<Collider>().enabled = running;
            foreach (var mechanism in target.mechanisms.GetComponentsInChildren<ElasticGeometry>(true))
                if (mechanism.draggable)
                    mechanism.SetLocked(!running);
        }
        private void EndTransition()
        {
            InTransition = false;
            TransitionFinished?.Invoke(ActiveLevel);
            if (level.audioDirector)
                level.audioDirector.Play(Cue.TransitionEnd);
        }

        private static float Ease(float fraction) => Mathf.SmoothStep(0f, 1f, fraction);
        private static float Seconds(float duration) => CampaignSettings.ReducedMotion ? Mathf.Min(0.5f, duration) : duration;

        internal static Rect PauseButtonRect(float width) => new(width - 116f, 20f, 96f, 34f);

        private void OnGUI()
        {
            if (!InPuzzle || InTransition)
                return;
            var matrix = GUI.matrix;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                float width = Screen.width / scale;
                if (!paused && GUI.Button(PauseButtonRect(width), "Pause  Esc", CourtyardHud.Button))
                    SetPause(true);
                if (paused)
                {
                    var action = CourtyardHud.PauseMenu(width, Screen.height / scale, CampaignScenes.FullCampaign);
                    if (action == PauseAction.Resume)
                        SetPause(false);
                    if (action == PauseAction.LevelSelect)
                        ReturnToCity();
                    if (action == PauseAction.MainMenu && CampaignScenes.OpenMenu())
                        SetPause(false);
                }
            }
            finally
            {
                GUI.matrix = matrix;
            }
        }
    }
}
