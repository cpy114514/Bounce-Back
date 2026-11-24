using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    [DisallowMultipleComponent]
    public sealed class GeometryLevel : MonoBehaviour
    {
        public string levelTitle = "Untitled Level";
        [TextArea] public string designerNotes;
        [TextArea] public string playHint;
        public Camera gameplayCamera;
        public BallSpawn spawn;
        public PuzzleTrigger goal;
        public AdaptivePuzzleAudio audioDirector;
        public float fallHeight = -12f;
        public bool showPlayHud = true;
        public GUISkin hudSkin;
        public bool allowRuntimeBuilding = true;
        public bool requireStarterPiston = true;
        [Min(0f)] public float attemptTimeLimit;
        public BallRetryEffect retryEffect;
        [Min(0f)] public float stoppedDuration;
        [Min(0f)] public float stoppedSpeed = 0.15f;
        public ElasticGeometry preparationWall;
        public bool lockWallOnRelease = true;
        public GameObject wallLock;

        public BallBody Ball { get; private set; }
        public bool Completed { get; private set; }
        public bool Retrying => retryEffect && retryEffect.IsPlaying;
        internal bool InputEnabled { get; set; } = true;
        internal bool IsDraggingMechanism => dragging && InputEnabled;
        public event Action<GeometryLevel> GoalReached;
        public event Action<BallBody> BallReleased;
        public static event Action<GeometryLevel> EditorRestartRequested;
        public static event Action<GeometryLevel, BuildBlockState[]> SaveBuildRequested;
        internal static bool CanSaveBuild => SaveBuildRequested != null;

        private Transform checkpoint;
        private ElasticGeometry dragging;
        private PuzzleCameraRig cameraRig;
        private RuntimeBlockBuilder builder;
        private readonly PlatformHints platformHints = new();
        private float releaseTime;
        private float stoppedTime;
        public PistonLauncher StarterPiston => spawn ? spawn.GetComponent<PistonLauncher>() : null;
        private static readonly Rect HudPanel = new(12f, 12f, 330f, 68f);
        private bool HasCourtyardHud => cameraRig && cameraRig.frameCourtyard && !hudSkin && !allowRuntimeBuilding;
        private bool ShowCourtyardHints => !Completed && Ball && !Ball.Released && !Retrying;
        private bool CanShowPlatformHint => showPlayHud && InputEnabled && !Completed && !Retrying &&
            (!Ball || !Ball.Released) && !dragging && builder?.Active != true;

        private void Start()
        {
            TryGetComponent(out cameraRig);
            if (allowRuntimeBuilding)
                builder = new RuntimeBlockBuilder(this);
            if (!spawn)
                return;
            Ball = spawn.Create(this);
            ResetBall(false);
            if (spawn.autoRelease && !StarterPiston)
                ReleaseBall();
        }

        private void OnDisable() => platformHints.Stop();

        private void OnDestroy()
        {
            platformHints.Dispose();
            builder?.Dispose();
        }

        internal void SaveBuiltBlocks(BuildBlockState[] blocks) => SaveBuildRequested?.Invoke(this, blocks);

        public void RefreshBuiltGadgetReferences(GameObject piece)
        {
            if (!goal || !goal.gameObject.activeInHierarchy)
            {
                goal = null;
                foreach (var zone in GetComponentsInChildren<PuzzleTrigger>())
                    if (zone.role == TriggerRole.Goal)
                    {
                        goal = zone;
                        break;
                    }
            }
            if (!piece || !audioDirector)
                return;
            if (piece.TryGetComponent(out PlatformMotion motion) && !motion.audioDirector)
                motion.audioDirector = audioDirector;
            if (piece.TryGetComponent(out ElasticGeometry elastic) && !elastic.audioDirector)
                elastic.audioDirector = audioDirector;
        }

        public void ReleaseBall()
        {
            if (!InputEnabled || !Ball || Ball.Released || Completed || Retrying)
                return;
            Transform origin = checkpoint ? checkpoint : spawn.transform;
            var piston = StarterPiston;
            if (!checkpoint && piston)
            {
                if (piston.isActiveAndEnabled && piston.HasVisuals)
                    LockPreparationWall();
                piston.Push(this);
                return;
            }
            Launch(origin);
        }

        internal void LaunchFromPiston(PistonLauncher piston)
        {
            if (Ball && !Ball.Released && !Completed && !checkpoint && StarterPiston == piston)
                Launch(piston.transform);
        }

        private void Launch(Transform origin)
        {
            LockPreparationWall();
            stoppedTime = 0f;
            if (retryEffect)
                retryEffect.BeginRecording(Ball);
            Ball.Release(origin.forward * spawn.launchSpeed);
            releaseTime = Time.time;
            if (audioDirector)
            {
                audioDirector.Activate();
                audioDirector.Play(Cue.Release);
            }
            BallReleased?.Invoke(Ball);
        }

        private void LockPreparationWall()
        {
            if (!lockWallOnRelease || !preparationWall || preparationWall.Locked)
                return;
            if (dragging)
                dragging.EndDrag();
            dragging = null;
            preparationWall.SetLocked(true);
            if (wallLock)
                wallLock.SetActive(true);
        }

        public void SetCheckpoint(Transform point) => checkpoint = point;

        public void ResetBall(bool keepCheckpoint = true)
        {
            if (!Ball)
                return;
            if (retryEffect)
                retryEffect.Cancel();
            HoldAtSpawn(keepCheckpoint);
        }

        public void FailBall()
        {
            if (!Ball || !Ball.Released || Completed || Retrying)
                return;
            if (retryEffect && retryEffect.rewindPath)
            {
                retryEffect.Rewind(Ball, () => HoldAtSpawn(true));
                return;
            }
            Vector3 impact = Ball.Body.position;
            if (retryEffect && gameplayCamera)
            {
                Vector3 screenPoint = gameplayCamera.WorldToViewportPoint(impact);
                screenPoint.x = Mathf.Clamp(screenPoint.x, 0.08f, 0.92f);
                screenPoint.y = Mathf.Clamp(screenPoint.y, 0.10f, 0.92f);
                impact = gameplayCamera.ViewportToWorldPoint(screenPoint);
            }
            HoldAtSpawn(true);
            if (retryEffect)
                retryEffect.Play(Ball, impact);
        }

        private void HoldAtSpawn(bool keepCheckpoint)
        {
            if (preparationWall)
                preparationWall.SetLocked(false);
            if (wallLock)
                wallLock.SetActive(false);
            if (!keepCheckpoint)
                checkpoint = null;
            var piston = StarterPiston;
            if (piston)
                piston.ResetStroke();
            Completed = false;
            Vector3 position = checkpoint ? checkpoint.position : spawn.StartPosition;
            Ball.Hold(position, checkpoint ? checkpoint.rotation : spawn.transform.rotation);
        }

        public void ReachGoal()
        {
            if (Completed)
                return;
            Completed = true;
            if (Ball)
                Ball.Hold(Ball.Body.position, Ball.Body.rotation);
            if (audioDirector)
                audioDirector.Play(Cue.Goal);
            GoalReached?.Invoke(this);
        }

        public void Restart()
        {
            if (EditorRestartRequested != null)
                EditorRestartRequested(this);
            else if (gameObject.scene.buildIndex >= 0)
                SceneManager.LoadSceneAsync(gameObject.scene.buildIndex);
            else
                ResetBall(false);
        }

        private void Update()
        {
            if (Ball && Ball.Released && !Ball.InTransition && !Completed)
            {
                stoppedTime = Ball.Speed < stoppedSpeed ? stoppedTime + Time.deltaTime : 0f;
                if (Ball.Body.position.y < fallHeight || (stoppedDuration > 0f && stoppedTime >= stoppedDuration) ||
                    (attemptTimeLimit > 0f && Time.time - releaseTime >= attemptTimeLimit))
                    FailBall();
            }
            else
                stoppedTime = 0f;
            if (!InputEnabled)
                return;
            if (showPlayHud && builder != null && builder.Update())
            {
                if (dragging)
                {
                    dragging.EndDrag();
                    dragging = null;
                }
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                    ReleaseBall();
                if (keyboard.rKey.wasPressedThisFrame)
                    ResetBall();
            }
            ReadPointer();
        }

        private void ReadPointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !gameplayCamera)
                return;
            Vector2 position = pointer.position.ReadValue();
            Ray ray = gameplayCamera.ScreenPointToRay(position);
            if (pointer.press.wasPressedThisFrame && !PointerOverHud(position) &&
                Physics.Raycast(ray, out RaycastHit hit, gameplayCamera.farClipPlane, ~0, QueryTriggerInteraction.Ignore))
            {
                var switchObject = hit.collider.GetComponentInParent<PuzzleSwitch>();
                if (switchObject)
                    switchObject.Interact();
                var piston = hit.collider.GetComponentInParent<PistonLauncher>();
                if (piston && piston == StarterPiston && !checkpoint)
                    ReleaseBall();
                dragging = hit.collider.GetComponentInParent<ElasticGeometry>();
                if (dragging && !dragging.BeginDrag(ray, gameplayCamera.transform.forward))
                    dragging = null;
            }

            if (!dragging)
                return;
            if (pointer.press.isPressed)
                dragging.Drag(ray);
            else
            {
                dragging.EndDrag();
                dragging = null;
            }
        }

        internal static float HudScale => Mathf.Clamp(Mathf.Min(Screen.width / 960f, Screen.height / 600f), 0.7f, 5f);

        private void LateUpdate() => platformHints.Update(CanShowPlatformHint, Time.unscaledDeltaTime);

        private IEnumerable<Transform> MovablePlatforms()
        {
            foreach (var elastic in GetComponentsInChildren<ElasticGeometry>())
                if (elastic.isActiveAndEnabled && elastic.draggable && !elastic.Locked && elastic.maximumDisplacement > 0f)
                    yield return elastic.visual ? elastic.visual : elastic.transform;

            foreach (var motion in GetComponentsInChildren<PlatformMotion>())
                if (motion.isActiveAndEnabled && motion.automatic &&
                    (motion.localTravel != Vector3.zero || motion.localEulerTravel != Vector3.zero))
                    yield return motion.transform;

            foreach (var sequence in GetComponentsInChildren<PuzzleSequence>(true))
                foreach (var action in sequence.actions)
                    if (action != null && action.target && action.target.scene == gameObject.scene &&
                        (action.type == PuzzleActionType.Move || action.type == PuzzleActionType.Rotate) &&
                        action.amount != Vector3.zero &&
                        (action.target.GetComponent<PlatformMotion>() || action.target.GetComponentInChildren<Collider>()))
                        yield return action.target.transform;
        }

        private bool PointerOverHud(Vector2 position)
        {
            var topLeft = new Vector2(position.x, Screen.height - position.y);
            if (!showPlayHud)
                return false;
            float scale = HudScale;
            var point = topLeft / scale;
            float width = Screen.width / scale;
            if (CanShowPlatformHint && PlatformHints.ButtonRect(width, Screen.height / scale).Contains(point))
                return true;
            if (HasCourtyardHud)
                return CourtyardPanel(width).Contains(point) || CampaignPlayground.PauseButtonRect(width).Contains(point);
            return HudPanel.Contains(point) || builder?.PointerOverHud(position) == true;
        }

        private Rect CourtyardPanel(float width) => new(20f, 20f, Mathf.Min(340f, width - 40f),
            ShowCourtyardHints ? 153f : 96f);

        private void DrawCourtyardHud(float width)
        {
            Rect card = CourtyardPanel(width);
            CourtyardHud.Card(card, true);
            var text = new Rect(card.x + 15f, card.y + 8f, card.width - 30f, 30f);
            GUI.Label(text, levelTitle, CourtyardHud.Title);
            text.y += 29f;
            text.height = 15f;
            string status = Completed ? "Complete" : Retrying ? "Rewinding" : Ball && Ball.Released ? "Ball in motion" : "Preparation";
            GUI.Label(text, "Level 1   ·   " + status, CourtyardHud.Caption);

            float buttonWidth = (text.width - 16f) / 3f;
            var action = new Rect(text.x, card.y + 58f, buttonWidth, 28f);
            bool enabled = GUI.enabled;
            var piston = StarterPiston;
            GUI.enabled = enabled && Ball && !Ball.Released && !Completed && !Retrying && (!piston || !piston.IsPushing);
            bool released = GUI.Button(action, "Release  Space", CourtyardHud.Primary);
            GUI.enabled = enabled;
            if (released)
                ReleaseBall();
            action.x += buttonWidth + 8f;
            if (GUI.Button(action, "Reset  R", CourtyardHud.Button))
                ResetBall();
            action.x += buttonWidth + 8f;
            if (GUI.Button(action, "Restart", CourtyardHud.Button))
                Restart();
            if (!ShowCourtyardHints)
                return;
            GUI.Label(new Rect(text.x, card.y + 91f, text.width, 31f), playHint, CourtyardHud.Instruction);
            GUI.Label(new Rect(text.x, card.y + 124f, text.width, 18f), "Right-drag: rotate   ·   Wheel: zoom   ·   Home: reset", CourtyardHud.Small);
        }

        private void OnGUI()
        {
            if (!showPlayHud || !InputEnabled)
                return;
            float scale = HudScale;
            Matrix4x4 matrix = GUI.matrix;
            GUISkin skin = GUI.skin;
            if (hudSkin)
                GUI.skin = hudSkin;
            bool guiEnabled = GUI.enabled;
            GUI.matrix = matrix * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                float width = Screen.width / scale;
                if (CanShowPlatformHint && PlatformHints.DrawButton(PlatformHints.ButtonRect(width, Screen.height / scale)))
                    platformHints.Play(MovablePlatforms());
                if (HasCourtyardHud)
                {
                    DrawCourtyardHud(width);
                    return;
                }
                GUI.Box(HudPanel, Completed ? levelTitle + "  ·  Complete" : levelTitle);
                builder?.DrawHud(scale);
                GUI.enabled = guiEnabled && builder?.Active != true;
                var piston = StarterPiston;
                bool playing = GUI.enabled;
                GUI.enabled = playing && Ball && !Ball.Released && !Completed && !Retrying && (!piston || !piston.IsPushing);
                bool push = GUI.Button(new Rect(22, 43, 99, 26), piston && !checkpoint ? "Push · Space" : "Release · Space");
                GUI.enabled = playing;
                if (push)
                    ReleaseBall();
                if (GUI.Button(new Rect(130, 43, 95, 26), "Reset Ball · R"))
                    ResetBall();
                if (GUI.Button(new Rect(234, 43, 98, 26), "Restart Level"))
                    Restart();
                if (!Completed && !string.IsNullOrWhiteSpace(playHint))
                    GUI.Label(new Rect(22, 85, Screen.width / scale - 44, 40), playHint);
                if (!Completed && cameraRig)
                {
                    if (!cameraRig.frameCourtyard || !Ball || !Ball.Released && !Retrying)
                        GUI.Label(new Rect(22, string.IsNullOrWhiteSpace(playHint) ? 85f : 106f, Screen.width / scale - 44, 24), PuzzleCameraRig.ControlsHint);
                }
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.enabled = guiEnabled;
                GUI.skin = skin;
            }
        }
    }
}
