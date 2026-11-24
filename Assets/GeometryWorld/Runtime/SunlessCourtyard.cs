using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    [RequireComponent(typeof(GeometryLevel))]
    public sealed class SunlessCourtyard : MonoBehaviour
    {
        public Camera view;
        public Transform westTurntable, eastHinge, centerLift, rearTurntable;
        public Transform seamStart, seamEnd, centerCheckpoint, finishCheckpoint;
        public Transform[] tablets, sockets, controls, foldPanels;
        public GameObject bridge, rearRoad, foldRoad, beacon;
        public Renderer[] alignmentMarks;
        public Material dormantMark, activeMark;

        private static readonly float[] ViewYaw = { -42f, 0f, 72f, 180f };
        private static readonly float[] ViewPitch = { 29f, 9.462f, 25f, 29f };
        private static readonly Vector3 Focus = new(0f, 0.75f, 0f);
        private const int SeamView = 1;
        private readonly bool[] seated = new bool[4];
        private readonly Vector3[] homes = new Vector3[4];
        private readonly PlatformHints platformHints = new();
        private Renderer[] socketFaces;
        private GeometryLevel level;
        private CampaignLevelAdapter campaign;
        private int dragged = -1;
        private float dragHeight;
        private bool overMatchingSocket;
        private float overviewSize, openingTime, feedbackUntil;
        private float orbitYaw, orbitPitch, scrollOrbitUntil;
        private float zoom = 1f;
        private bool orbiting;
        private bool westReady, eastReady, atCenter, starFound, rearOpen, folded, busy;
        private bool alignmentSeen, locked;
        private string feedback;

        internal bool PreviewAligned => IsAligned(seamStart.position, seamEnd.position);
        internal bool CanCross => westReady && eastReady && seated[2] && !orbiting && IsAligned(seamStart.position, seamEnd.position)
            && Mathf.Abs((seamStart.position.y - seamEnd.position.y) - 0.25f) < 0.035f
            && Vector3.Dot(seamStart.forward, seamEnd.forward) > 0.99f
            && Vector3.Dot(seamStart.forward, (seamEnd.position - seamStart.position).normalized) > 0.98f
            && !busy && HasBridgeClearance();
        internal bool CanFinish => atCenter && starFound && folded && !rearOpen && CanCross;
        internal string PlayPrompt
        {
            get
            {
                if (level.Completed)
                    return "The ball reached the light.";
                if (dragged >= 0)
                    return overMatchingSocket ? "Release to seat the tablet." : "Drag the tablet to its highlighted socket.";
                if (feedback != null && Time.unscaledTime < feedbackUntil)
                    return feedback;
                if (level.Ball && level.Ball.Released)
                    return "Ball rolling...";
                if (busy)
                    return "The stonework is moving.";
                if (!atCenter)
                {
                    if (bridge.activeSelf)
                        return "The bridge is open. Roll the ball.";
                    if (!alignmentSeen)
                        return "Turn the camera and look for matching seams.";
                    if (!seated[0] || !seated[1] || !seated[2])
                        return "Drag the stone tablets onto the matching sockets.";
                    if (!westReady || !eastReady)
                        return "Click the tower dials and watch how the stairs move.";
                    return "Find the angle that lines up the two seams.";
                }
                if (!starFound)
                    return rearOpen ? "Roll the ball across the rear bridge." : "Inspect the back of the courtyard.";
                if (!folded)
                    return "Bring the star tablet to the central socket.";
                if (rearOpen)
                    return "Return the rear mechanism to its starting position.";
                return bridge.activeSelf || locked ? "The exit path is ready. Roll the ball." : "Line up the two seams again.";
            }
        }

        private void Awake()
        {
            level = GetComponent<GeometryLevel>();
            campaign = GetComponent<CampaignLevelAdapter>();
            level.InputEnabled = false;
            socketFaces = new Renderer[sockets.Length];
            for (int i = 0; i < tablets.Length; i++)
            {
                homes[i] = tablets[i].position;
                socketFaces[i] = sockets[i].GetComponentInChildren<Renderer>();
            }
            SetViewInstantly(0);
            overviewSize = view.orthographicSize;
            view.orthographicSize = overviewSize * 1.16f;
        }

        private void Update()
        {
            if (CampaignBlocksPuzzle)
            {
                CancelTabletDrag();
                return;
            }
            if (level.Completed)
                return;
            var keyboard = Keyboard.current;
            ReadOrbitInput(keyboard, Mouse.current, Gamepad.current);
            UpdateView();
            if (!orbiting && !alignmentSeen && (PreviewAligned || IsAligned(new Vector3(-3f, 1f, 0f), new Vector3(-1.5f, 0.75f, 0f))))
            {
                alignmentSeen = true;
                ShowFeedback("The seam marks line up. Now inspect the stonework.");
            }
            if (!level.Ball || !level.Ball.Released)
                bridge.SetActive(CanCross || locked);
            foreach (var mark in alignmentMarks)
                mark.sharedMaterial = alignmentSeen ? activeMark : dormantMark;

            if (keyboard?.rKey.wasPressedThisFrame == true)
                RetryBall();
            if (keyboard?.spaceKey.wasPressedThisFrame == true)
                TryLaunch();
        }

        internal bool HandlePointer(EventType phase, Vector2 cursor)
        {
            if (CampaignBlocksPuzzle)
                return false;
            var guiCursor = new Vector2(cursor.x, Screen.height - cursor.y) / HudScale;
            bool overHud = PromptPanel.Contains(guiCursor) || Toolbar.Contains(guiCursor) ||
                (HasCampaign && PauseButton.Contains(guiCursor)) ||
                (CanShowPlatformHint && HintButton.Contains(guiCursor));
            if (dragged >= 0)
            {
                if (phase != EventType.MouseDrag && phase != EventType.MouseUp)
                    return false;
                overMatchingSocket = !overHud && IsMatchingDrop(dragged, cursor);
                if (phase == EventType.MouseUp)
                {
                    if (overMatchingSocket)
                    {
                        int index = dragged;
                        dragged = -1;
                        SeatTablet(index);
                    }
                    else
                    {
                        CancelTabletDrag();
                        ShowFeedback("The tablet returned to its cradle. Drop it inside the highlighted socket.");
                    }
                }
                else if (overMatchingSocket)
                    tablets[dragged].position = SocketSeat(dragged) + Vector3.up * 0.07f;
                else
                {
                    Ray ray = view.ScreenPointToRay(cursor);
                    if (new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f)).Raycast(ray, out float distance))
                        tablets[dragged].position = ray.GetPoint(distance);
                }
                return true;
            }

            if (phase != EventType.MouseDown || overHud || busy || locked || orbiting ||
                !Physics.Raycast(view.ScreenPointToRay(cursor), out var hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            for (int i = 0; i < tablets.Length; i++)
            {
                if (hit.transform != tablets[i] || seated[i])
                    continue;
                dragged = i;
                dragHeight = tablets[i].position.y + 0.25f;
                overMatchingSocket = false;
                return true;
            }
            for (int i = 0; i < controls.Length; i++)
                if (hit.transform == controls[i])
                {
                    Operate(i);
                    return true;
                }
            return false;
        }

        private Rect SocketScreenBounds(int index)
        {
            Bounds face = socketFaces[index].bounds;
            Vector3 low = face.min;
            Vector3 high = face.max;
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 point = view.WorldToScreenPoint(new Vector3((corner & 1) == 0 ? low.x : high.x,
                    high.y, (corner & 2) == 0 ? low.z : high.z));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private bool IsMatchingDrop(int index, Vector2 cursor)
        {
            Rect face = SocketScreenBounds(index);
            Vector2 center = face.center;
            float padding = Mathf.Max(8f, view.pixelHeight * 0.025f);
            var dropArea = Rect.MinMaxRect(face.xMin - padding, face.yMin - padding, face.xMax + padding, face.yMax + padding);
            if (!dropArea.Contains(cursor))
                return false;
            float distance = (cursor - center).sqrMagnitude;
            for (int i = 0; i < sockets.Length; i++)
                if (i != index && (cursor - SocketScreenBounds(i).center).sqrMagnitude < distance)
                    return false;
            return true;
        }

        private Vector3 SocketSeat(int index) => sockets[index].position + Vector3.up * 0.14f;

        private void CancelTabletDrag()
        {
            if (dragged < 0)
                return;
            tablets[dragged].position = homes[dragged];
            dragged = -1;
            overMatchingSocket = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused)
                return;
            if (orbiting)
                FinishOrbit();
            CancelTabletDrag();
        }

        internal void SeatTablet(int index)
        {
            if (seated[index] || (index == 3 && !starFound))
                return;
            seated[index] = true;
            tablets[index].position = SocketSeat(index);
            ShowFeedback("Tablet seated.");
            if (index == 3)
                StartCoroutine(Unfold());
        }

        internal void Operate(int index)
        {
            if (busy || locked || level.Ball && level.Ball.Released)
                return;
            switch (index)
            {
                case 0 when seated[0] && seated[2] && !westReady:
                    StartCoroutine(TurnWest());
                    break;
                case 1 when seated[1] && !eastReady:
                    StartCoroutine(FlipEast());
                    break;
                case 2 when atCenter && (!rearOpen || starFound):
                    StartCoroutine(TurnRear());
                    break;
                case 0 when !westReady:
                    ShowFeedback("The west dial needs the sun and blank tablets.");
                    break;
                case 1 when !eastReady:
                    ShowFeedback("The east dial needs the moon tablet.");
                    break;
                case 2:
                    ShowFeedback(atCenter ? "Send the ball across the rear bridge first." : "Reach the central platform first.");
                    break;
            }
        }

        internal void SetViewInstantly(int index)
        {
            SetView(index);
            Quaternion rotation = CameraRotation();
            view.transform.SetPositionAndRotation(Focus - rotation * Vector3.forward * 30f, rotation);
        }

        private void SetView(int index)
        {
            if (!CanOrbit)
                return;
            orbiting = false;
            scrollOrbitUntil = 0f;
            index = (index + ViewYaw.Length) % ViewYaw.Length;
            orbitYaw = 90f + ViewYaw[index];
            orbitPitch = ViewPitch[index];
        }

        private bool CanOrbit => !locked && dragged < 0 && (!level.Ball || !level.Ball.Released);

        private Quaternion CameraRotation() => Quaternion.Euler(orbitPitch, orbitYaw, 0f);

        private void ReadOrbitInput(Keyboard keyboard, Mouse mouse, Gamepad gamepad)
        {
            if (keyboard?.homeKey.wasPressedThisFrame == true)
            {
                zoom = 1f;
                SetView(0);
                return;
            }
            Vector2 scroll = mouse != null && !mouse.leftButton.isPressed ? mouse.scroll.ReadValue() : Vector2.zero;
            bool horizontalScroll = Mathf.Abs(scroll.x) > Mathf.Abs(scroll.y);
            if (!horizontalScroll)
                zoom = PuzzleCameraRig.ZoomFromScroll(zoom, scroll.y);
            if (!CanOrbit)
                return;

            bool draggingView = mouse != null && mouse.rightButton.isPressed && !mouse.leftButton.isPressed;
            Vector2 drag = draggingView ? mouse.delta.ReadValue() : Vector2.zero;
            Vector2 stick = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
            float wheel = horizontalScroll ? PuzzleCameraRig.ScrollSteps(scroll.x) : 0f;
            float keys = (keyboard?.eKey.isPressed == true ? 1f : 0f) - (keyboard?.qKey.isPressed == true ? 1f : 0f);
            if (wheel != 0f)
                scrollOrbitUntil = Time.unscaledTime + 0.16f;

            if (drag == Vector2.zero && stick == Vector2.zero && wheel == 0f && keys == 0f)
            {
                if (orbiting && !draggingView && Time.unscaledTime >= scrollOrbitUntil)
                    FinishOrbit();
                return;
            }

            orbiting = true;
            float dt = Time.unscaledDeltaTime;
            float yawSpeed = keys * PuzzleCameraRig.DefaultKeyboardDegreesPerSecond + stick.x * 125f;
            orbitYaw = Mathf.Repeat(orbitYaw + drag.x * PuzzleCameraRig.DefaultMouseDegreesPerPixel
                + yawSpeed * dt + Mathf.Clamp(wheel, -2f, 2f) * 38f, 360f);
            orbitPitch = Mathf.Clamp(orbitPitch - drag.y * PuzzleCameraRig.DefaultMouseDegreesPerPixel - stick.y * 75f * dt, 8f, 70f);
        }

        private void FinishOrbit()
        {
            orbiting = false;
            scrollOrbitUntil = 0f;
            // Only the bridge view needs an exact projection. Other views stay where the player leaves them.
            if (Mathf.Abs(Mathf.DeltaAngle(orbitYaw, 90f)) <= 9f && Mathf.Abs(orbitPitch - ViewPitch[SeamView]) <= 22f)
                SetView(SeamView);
        }

        private int NearestView()
        {
            int nearest = 0;
            float distance = Mathf.Abs(Mathf.DeltaAngle(orbitYaw, 90f + ViewYaw[nearest]));
            for (int i = 0; i < ViewYaw.Length; i++)
            {
                float candidate = Mathf.Abs(Mathf.DeltaAngle(orbitYaw, 90f + ViewYaw[i]));
                if (candidate < distance)
                {
                    nearest = i;
                    distance = candidate;
                }
            }
            return nearest;
        }

        private void UpdateView()
        {
            float blend = 1f - Mathf.Exp(-PuzzleCameraRig.RotationResponsiveness * Time.unscaledDeltaTime);
            Quaternion rotation = Quaternion.Slerp(view.transform.rotation, CameraRotation(), blend);
            view.transform.SetPositionAndRotation(Focus - rotation * Vector3.forward * 30f, rotation);
            openingTime = Mathf.Min(0.9f, openingTime + Time.unscaledDeltaTime);
            float size = overviewSize * zoom * Mathf.SmoothStep(1.16f, 1f, openingTime / 0.9f);
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, size, blend);
        }

        private bool IsAligned(Vector3 a, Vector3 b)
        {
            if (Quaternion.Angle(view.transform.rotation, Quaternion.Euler(ViewPitch[SeamView], 90f, 0f)) > 3f)
                return false;
            Vector3 delta = view.WorldToViewportPoint(a) - view.WorldToViewportPoint(b);
            return new Vector2(delta.x, delta.y).sqrMagnitude < 0.000025f;
        }

        private bool HasBridgeClearance()
        {
            Vector3 a = seamStart.position;
            Vector3 b = seamEnd.position;
            float radius = level.spawn.diameter * 0.5f;
            return !Physics.CheckCapsule(a + Vector3.up * (radius + 0.04f), b + Vector3.up * (radius + 0.04f),
                radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private IEnumerator TurnWest()
        {
            busy = true;
            if (!eastReady)
            {
                yield return Rotate(westTurntable, Quaternion.Euler(0f, 13f, 0f), 0.25f);
                yield return Rotate(westTurntable, Quaternion.identity, 0.32f);
                ShowFeedback("Something is blocking the west stairs.");
            }
            else
            {
                yield return Rotate(westTurntable, Quaternion.Euler(0f, 90f, 0f), 0.7f);
                Vector3 origin = centerLift.localPosition;
                Vector3 destination = origin - Vector3.up * 0.85f;
                float started = Time.time;
                while (Time.time - started < 0.55f)
                {
                    centerLift.localPosition = Vector3.Lerp(origin, destination, Mathf.SmoothStep(0f, 1f, (Time.time - started) / 0.55f));
                    yield return null;
                }
                centerLift.localPosition = destination;
                westReady = true;
            }
            busy = false;
        }

        private IEnumerator FlipEast()
        {
            busy = true;
            yield return Rotate(eastHinge, Quaternion.Euler(180f, 0f, 0f), 0.85f);
            eastReady = true;
            busy = false;
        }

        private IEnumerator TurnRear()
        {
            busy = true;
            rearRoad.SetActive(false);
            yield return Rotate(rearTurntable, rearOpen ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f), 0.8f);
            rearOpen = !rearOpen;
            rearRoad.SetActive(rearOpen);
            level.SetCheckpoint(rearOpen ? centerCheckpoint : finishCheckpoint);
            level.ResetBall();
            busy = false;
        }

        private IEnumerator Unfold()
        {
            busy = true;
            foreach (var panel in foldPanels)
                yield return Rotate(panel, Quaternion.identity, 0.28f);
            foldRoad.SetActive(true);
            folded = true;
            busy = false;
        }

        private static IEnumerator Rotate(Transform part, Quaternion destination, float duration)
        {
            Quaternion origin = part.localRotation;
            float started = Time.time;
            while (Time.time - started < duration)
            {
                part.localRotation = Quaternion.Slerp(origin, destination, Mathf.SmoothStep(0f, 1f, (Time.time - started) / duration));
                yield return null;
            }
            part.localRotation = destination;
        }

        internal void TryLaunch()
        {
            if (CampaignBlocksPuzzle || !level.Ball || level.Ball.Released || level.Completed)
                return;
            if (orbiting)
            {
                ShowFeedback("Let the camera settle before rolling the ball.");
                return;
            }
            if (busy)
            {
                ShowFeedback("Wait for the moving stonework to settle.");
                return;
            }
            bool finishing = CanFinish;
            bool canRoll = locked || finishing || (!atCenter ? CanCross : rearOpen && !starFound);
            if (!canRoll)
            {
                ShowFeedback(atCenter ? "The next path is still closed." : "A gap still blocks the first route. Look for a way across.");
                return;
            }
            if (finishing)
            {
                locked = true;
                beacon.SetActive(true);
            }
            feedback = null;
            level.InputEnabled = true;
            level.ReleaseBall();
            level.InputEnabled = false;
        }

        private void RetryBall()
        {
            level.ResetBall();
            ShowFeedback("Ball returned to the last checkpoint.");
        }

        private void ShowFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + 2.5f;
        }

        private static float HudScale => Mathf.Clamp(Mathf.Min(Screen.width / 850f, Screen.height / 550f), 0.55f, 1.2f);
        private Rect PromptPanel => new(14f, 14f, 472f, level.Completed ? 68f : 102f);
        private static Rect Toolbar => new(14f, Screen.height / HudScale - 58f, 420f, 44f);
        private bool HasCampaign => campaign && campaign.isActiveAndEnabled;
        private bool CampaignBlocksPuzzle => CampaignSceneParticles.IsRunning || HasCampaign &&
            campaign.Phase is CampaignPhase.Paused or CampaignPhase.Returning or CampaignPhase.Completing;
        private Rect PauseButton
        {
            get
            {
                Rect button = CampaignPlayground.PauseButtonRect(Screen.width / HudScale);
                if (button.xMin < PromptPanel.xMax + 8f)
                    button.y = 166f;
                return button;
            }
        }
        private static Rect HintButton
        {
            get
            {
                float width = Screen.width / HudScale;
                Rect button = PlatformHints.ButtonRect(width, Screen.height / HudScale);
                if (button.xMin < Toolbar.xMax + 8f)
                    button.y = 124f;
                return button;
            }
        }
        private bool CanShowPlatformHint => level && level.Ball && !CampaignBlocksPuzzle && !level.Completed && !level.Retrying &&
            !level.Ball.Released && !locked && !busy && dragged < 0;

        private void LateUpdate() => platformHints.Update(CanShowPlatformHint, Time.unscaledDeltaTime);

        private void OnDisable() => platformHints.Stop();

        private void OnDestroy() => platformHints.Dispose();

        private IEnumerable<Transform> MovablePlatforms()
        {
            for (int i = 0; i < tablets.Length; i++)
                if (!seated[i])
                    yield return tablets[i];

            if (!westReady)
            {
                yield return westTurntable;
                yield return centerLift;
            }
            if (!eastReady)
                yield return eastHinge;
            if (atCenter)
                yield return rearTurntable;
            if (starFound && !folded)
                foreach (Transform panel in foldPanels)
                    yield return panel;
        }

        private void OnGUI()
        {
            if (!level || !level.Ball)
                return;
            Event input = Event.current;
            int tabletControl = GUIUtility.GetControlID(FocusType.Passive);
            if ((dragged < 0 || CampaignBlocksPuzzle) && GUIUtility.hotControl == tabletControl)
                GUIUtility.hotControl = 0;
            if (CampaignSceneParticles.IsRunning || CampaignBlocksPuzzle && !level.Completed)
                return;
            if (input.isMouse && input.button == 0 && (GUIUtility.hotControl == 0 || GUIUtility.hotControl == tabletControl) &&
                HandlePointer(input.type, new Vector2(input.mousePosition.x, Screen.height - input.mousePosition.y)))
            {
                GUIUtility.hotControl = dragged >= 0 ? tabletControl : 0;
                input.Use();
            }
            Matrix4x4 matrix = GUI.matrix;
            float scale = HudScale;
            GUI.matrix *= Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (CanShowPlatformHint && PlatformHints.DrawButton(HintButton))
                platformHints.Play(MovablePlatforms());
            if (dragged >= 0)
                DrawSocketTarget(scale);
            CourtyardHud.Card(PromptPanel, true);
            GUI.Label(new Rect(28f, 21f, 444f, 30f), level.levelTitle, CourtyardHud.Title);
            GUI.Label(new Rect(28f, 53f, 444f, 20f), PlayPrompt, CourtyardHud.Instruction);

            if (!level.Completed)
            {
                if (HasCampaign && GUI.Button(PauseButton, "Pause  Esc", CourtyardHud.Button))
                    campaign.TogglePause();
                if (!locked)
                    GUI.Label(new Rect(28f, 76f, 444f, 16f), "Orbit: right-drag, Q / E, sideways swipe or right stick", CourtyardHud.Small);
                GUI.Label(new Rect(28f, 93f, 444f, 16f), "Scroll to zoom   ·   Home to reset view", CourtyardHud.Small);
                float y = Toolbar.y + 6f;
                if (!locked && GUI.Button(new Rect(14f, y, 98f, 32f), "Rotate left", CourtyardHud.Button))
                    SetView(NearestView() - 1);
                if (!locked && GUI.Button(new Rect(116f, y, 104f, 32f), "Rotate right", CourtyardHud.Button))
                    SetView(NearestView() + 1);
                if (GUI.Button(new Rect(228f, y, 111f, 32f), "Space  Roll ball", CourtyardHud.Primary))
                    TryLaunch();
                if (GUI.Button(new Rect(343f, y, 78f, 32f), "R  Retry", CourtyardHud.Button))
                    RetryBall();
            }
            GUI.matrix = matrix;
        }

        private void DrawSocketTarget(float scale)
        {
            Rect face = SocketScreenBounds(dragged);
            const float inset = 5f, width = 2f;
            Rect outline = Rect.MinMaxRect(face.xMin / scale - inset, (Screen.height - face.yMax) / scale - inset,
                face.xMax / scale + inset, (Screen.height - face.yMin) / scale + inset);
            Color previous = GUI.color;
            GUI.color = overMatchingSocket ? new Color32(163, 247, 192, 255) : new Color32(255, 226, 133, 255);
            GUI.DrawTexture(new Rect(outline.xMin, outline.yMin, outline.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.xMin, outline.yMax - width, outline.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.xMin, outline.yMin, width, outline.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(outline.xMax - width, outline.yMin, width, outline.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void FixedUpdate()
        {
            BallBody ball = level.Ball;
            if (!ball || !ball.Released || level.Completed)
                return;
            Vector3 point = ball.Body.position;
            if (!atCenter && point.x >= -0.35f && Mathf.Abs(point.z) < 0.55f && point.y > 0.7f && point.y < 1.5f)
            {
                atCenter = true;
                level.SetCheckpoint(centerCheckpoint);
                level.ResetBall();
                ShowFeedback("Central checkpoint reached.");
            }
            if (rearOpen && !starFound && point.z >= 5.3f && Mathf.Abs(point.x) < 0.55f && point.y > 0.45f && point.y < 1.5f)
            {
                starFound = true;
                tablets[3].gameObject.SetActive(true);
                level.ResetBall();
                ShowFeedback("The star tablet is now within reach.");
            }
        }
    }
}
