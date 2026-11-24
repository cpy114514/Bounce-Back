using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class MainMenu : MonoBehaviour
    {
        public enum View { Intro, Overview, Confirm, General, Audio, Entering }

        public Camera menuCamera;
        public MenuIsland[] islands;
        public Transform titleIsland;
        public MenuControl[] generalControls, audioControls;
        public MenuSettingsDrawer generalDeck, audioDeck;
        public MenuCameraPose overview, opening, general, audio, newGame, continueGame;
        public MenuAtmosphere atmosphere;
        public AudioSource music, effects;
        public AudioClip interactionCue;
        public MainMenuUI presentation;

        internal static bool IntroSeen { get; private set; }
        internal View CurrentView { get; private set; }
        internal int FocusedIsland { get; private set; } = -1;
        internal int ConfirmChoice { get; private set; }
        internal MenuControl FocusedControl { get; private set; }
        internal float OverviewAlpha { get; private set; }
        internal float SubmenuAlpha { get; private set; }
        internal float JourneyVeil => CurrentView == View.Entering
            ? MenuIsland.Ease(Mathf.InverseLerp(.68f, 1f, blend)) : 0f;
        internal bool IsDisplayFullscreen => ReadFullscreen();
        internal bool HasJourney => CampaignProgress.CanResume;
        internal string Status { get; private set; }

        private MenuCameraPose pose, previous, destination;
        private Vector3 titleShore;
        private Quaternion titleHeading;
        private MeshCollider titleSurface;
        private float[] islandDrift;
        private float elapsed, blend, transitionDuration, assembly, generalOpen, audioOpen, pendingFullscreen, statusUntil;
        private bool requestedFullscreen, reducedMotion, dragWithTouch, hasPointer, launchRequested;
        private int keyboardIndex = -1, selectedIsland = -1, hoveredIsland = -1;
        private MenuControl dragging;
        private Vector2 lastPointer;
        private bool lastFullscreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => IntroSeen = false;

        private void Start()
        {
            CampaignScenes.UseMainMenu();
            CampaignSettings.BeginMenuSession();
            reducedMotion = CampaignSettings.ReducedMotion;
            lastFullscreen = ReadFullscreen();
            CurrentView = IntroSeen ? View.Overview : View.Intro;
            assembly = IntroSeen ? 1f : 0f;
            pose = IntroSeen ? overview : opening;
            previous = destination = pose;
            blend = 1f;
            islandDrift = new float[islands.Length + 1];
            if (titleIsland)
            {
                titleShore = titleIsland.localPosition;
                titleHeading = titleIsland.localRotation;
                titleIsland.TryGetComponent(out titleSurface);
            }
            pose.Apply(menuCamera);
            Present(0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            CampaignSettings.SavePending();
            UpdateFullscreen();
            ReadInput();
            elapsed += dt;
            if (CurrentView == View.Intro)
            {
                float hold = reducedMotion ? 0.11f : 0.52f;
                float end = reducedMotion ? 0.7f : 3.05f;
                assembly = Mathf.InverseLerp(hold, end, elapsed);
                pose = MenuCameraPose.Lerp(opening, overview, MenuIsland.Ease(assembly));
                if (assembly >= 1f)
                    FinishIntro();
            }
            else if (blend < 1f)
            {
                blend = Mathf.MoveTowards(blend, 1f, dt / transitionDuration);
                pose = MenuCameraPose.Lerp(previous, destination, MenuIsland.Ease(blend));
            }
            pose.Apply(menuCamera);
            Present(dt);

            if (CurrentView != View.Entering || launchRequested || blend < 1f || !islands[selectedIsland].Settled)
                return;
            launchRequested = true;
            CampaignSettings.SavePending(true);
            bool entered = selectedIsland == 0 ? CampaignScenes.BeginMenuJourney(menuCamera.transform.rotation)
                : CampaignScenes.ResumeMenuJourney(menuCamera.transform.rotation);
            if (!entered)
            {
                Flash(presentation.artwork.journeyUnavailable);
                ReturnToOverview();
            }
        }

        internal void FinishIntro()
        {
            IntroSeen = true;
            assembly = 1f;
            MoveTo(View.Overview, overview, .56f);
        }

        private void Present(float dt)
        {
            float soft = 1f - Mathf.Exp(-8f * dt);
            bool wantsGeneral = CurrentView == View.General;
            bool wantsAudio = CurrentView == View.Audio;
            float drawerStep = dt / (reducedMotion ? .22f : 1.38f);
            generalOpen = Mathf.MoveTowards(generalOpen, wantsGeneral ? 1f : 0f, drawerStep);
            audioOpen = Mathf.MoveTowards(audioOpen, wantsAudio ? 1f : 0f, drawerStep);
            if (generalDeck) generalDeck.Pose(generalOpen);
            if (audioDeck) audioDeck.Pose(audioOpen);
            float recess = MenuIsland.Ease(Mathf.Max(generalOpen, audioOpen));
            float time = Time.unscaledTime;
            if (titleIsland)
                MenuIsland.PoseBase(titleIsland, titleShore, titleHeading, recess, DriftFor(islands.Length, dt), time, islands.Length);
            for (int i = 0; i < islands.Length; i++)
            {
                bool remains = selectedIsland == i;
                bool dock = (i == 2 && (wantsGeneral || generalOpen > 0f)) || (i == 3 && (wantsAudio || audioOpen > 0f));
                bool restore = (CurrentView == View.Overview && i == FocusedIsland) ||
                    (remains && CurrentView is View.Confirm or View.Entering) || dock;
                float annex = i == 2 ? generalOpen : i == 3 ? audioOpen : 0f;
                float rewind = remains && CurrentView == View.Entering && i == 1 ? MenuIsland.Ease(blend) : 0f;
                if (remains && CurrentView == View.Entering && i == 0)
                    annex = MenuIsland.Ease(blend);
                islands[i].Pose(assembly, restore, MenuIsland.Ease(annex), dock ? 0f : recess, rewind,
                    time, dt, reducedMotion, DriftFor(i, dt), i);
            }
            if (generalOpen > 0f)
                PresentControls(generalControls, soft);
            if (audioOpen > 0f)
                PresentControls(audioControls, soft);
            OverviewAlpha = Mathf.MoveTowards(OverviewAlpha,
                CurrentView is View.Overview or View.Confirm ? 1f : 0f, dt * 2.7f);
            SubmenuAlpha = Mathf.MoveTowards(SubmenuAlpha, CanUseControls ? 1f : 0f, dt * 4f);
            if (atmosphere)
                atmosphere.Present(assembly, CurrentView, selectedIsland, reducedMotion);
            if (music)
                music.volume = .38f * CampaignSettings.MusicVolume;
            if (effects)
                effects.volume = CampaignSettings.EffectsVolume;
        }

        private float DriftFor(int index, float dt)
        {
            bool hovering = CurrentView == View.Overview && index == hoveredIsland;
            islandDrift[index] = Mathf.MoveTowards(islandDrift[index], hovering ? 1f : 0f, 2.5f * dt);
            return MenuIsland.Ease(islandDrift[index]);
        }

        private void PresentControls(MenuControl[] controls, float step)
        {
            foreach (var control in controls)
                control.Present(Value(control.action), control == FocusedControl || control == dragging, step);
        }

        internal float Value(MenuControl.Action action) => action switch
        {
            MenuControl.Action.Master => CampaignSettings.MasterVolume,
            MenuControl.Action.Music => CampaignSettings.MusicVolume,
            MenuControl.Action.Effects => CampaignSettings.EffectsVolume,
            MenuControl.Action.Fullscreen => IsDisplayFullscreen ? 1f : 0f,
            MenuControl.Action.Low or MenuControl.Action.Medium or MenuControl.Action.High =>
                (int)CampaignSettings.GraphicsQuality == (int)action ? 1f : 0f,
            _ => 0f
        };

        private bool CanUseControls => blend >= 1f && (CurrentView == View.General && generalOpen >= 1f ||
            CurrentView == View.Audio && audioOpen >= 1f);

        private void ReadInput()
        {
            hoveredIsland = -1;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            var touch = Touchscreen.current?.primaryTouch;
            bool touchStarted = touch?.press.wasPressedThisFrame == true;
            bool mouseStarted = mouse?.leftButton.wasPressedThisFrame == true;
            bool pressed = touchStarted || mouseStarted;
            bool enter = keyboard?.enterKey.wasPressedThisFrame == true || keyboard?.numpadEnterKey.wasPressedThisFrame == true;
            bool space = keyboard?.spaceKey.wasPressedThisFrame == true;
            bool escape = keyboard?.escapeKey.wasPressedThisFrame == true;
            bool tab = keyboard?.tabKey.wasPressedThisFrame == true;
            int direction = keyboard?.shiftKey.isPressed == true ? -1 : 1;
            if (pressed || enter || space || tab)
                StartAudio();
            Vector2 point = touchStarted || dragWithTouch ? touch?.position.ReadValue() ?? lastPointer
                : mouse?.position.ReadValue() ?? lastPointer;
            if (mouse != null)
                hasPointer = true;
            if (pressed || (point - lastPointer).sqrMagnitude > 1f)
                keyboardIndex = -1;
            lastPointer = point;
            if (CurrentView == View.Intro)
            {
                if (escape || space || enter || (pressed && presentation.InSkipArea(point)))
                    FinishIntro();
                return;
            }
            if (CurrentView == View.Confirm)
            {
                if (escape) RespondToReset(false);
                else if (tab || keyboard?.leftArrowKey.wasPressedThisFrame == true || keyboard?.rightArrowKey.wasPressedThisFrame == true)
                    ConfirmChoice = 1 - ConfirmChoice;
                else if (enter || space) RespondToReset(ConfirmChoice == 1);
                return;
            }
            if (escape && (CurrentView is View.General or View.Audio) && !lastFullscreen)
            {
                ReturnToOverview();
                return;
            }
            Ray ray = menuCamera.ScreenPointToRay(point);
            bool overScreen = point.x >= 0f && point.x <= Screen.width && point.y >= 0f && point.y <= Screen.height;
            if (CurrentView == View.Overview && blend >= .8f && generalOpen <= 0f && audioOpen <= 0f)
            {
                if (tab) Cycle(islands.Length, direction);
                int pointerIsland = overScreen && (hasPointer || pressed) ? IslandAt(ray) : -1;
                int caption = overScreen && (hasPointer || pressed) ? presentation.CaptionAt(point) : -1;
                FocusedIsland = keyboardIndex >= 0 ? keyboardIndex : caption >= 0 ? caption : pointerIsland;
                if (!reducedMotion && mouse != null && overScreen && !touchStarted)
                {
                    float distance = pointerIsland >= 0 ? islands[pointerIsland].HoverDistance(ray) : float.PositiveInfinity;
                    if (titleSurface && titleSurface.Raycast(ray, out _, distance))
                        hoveredIsland = islands.Length;
                    else if (distance < float.PositiveInfinity)
                        hoveredIsland = pointerIsland;
                }
                if ((pressed || enter || space) && FocusedIsland >= 0)
                    SelectIsland(FocusedIsland);
            }
            else if (CanUseControls)
            {
                var controls = CurrentView == View.General ? generalControls : audioControls;
                if (dragging)
                {
                    if (dragWithTouch ? touch?.press.isPressed == true : mouse?.leftButton.isPressed == true)
                        AdjustFromPointer(dragging, ray);
                    else
                    {
                        dragging = null;
                        dragWithTouch = false;
                        CampaignSettings.SavePending(true);
                    }
                    return;
                }
                if (tab) Cycle(controls.Length, direction);
                FocusedControl = keyboardIndex >= 0 ? controls[keyboardIndex] : overScreen
                    ? presentation.ControlCaptionAt(controls, point) ?? ControlAt(controls, ray) : null;
                if (!FocusedControl)
                    return;
                if (FocusedControl.IsRail)
                {
                    if (pressed)
                    {
                        dragging = FocusedControl;
                        dragWithTouch = touchStarted;
                        AdjustFromPointer(dragging, ray);
                        PlayCue();
                    }
                    if (keyboardIndex >= 0)
                    {
                        int step = keyboard?.rightArrowKey.wasPressedThisFrame == true ? 1
                            : keyboard?.leftArrowKey.wasPressedThisFrame == true ? -1 : 0;
                        if (step != 0)
                            SetVolume(FocusedControl.action, Mathf.Clamp01(Value(FocusedControl.action) + step * .05f));
                        if (keyboard?.homeKey.wasPressedThisFrame == true) SetVolume(FocusedControl.action, 0f);
                        if (keyboard?.endKey.wasPressedThisFrame == true) SetVolume(FocusedControl.action, 1f);
                    }
                }
                else if (pressed || enter || space)
                    Activate(FocusedControl.action);
            }
        }

        private void Cycle(int count, int direction) => keyboardIndex =
            (Mathf.Max(direction > 0 ? -1 : 0, keyboardIndex) + direction + count) % count;

        private int IslandAt(Ray ray)
        {
            int result = -1;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < islands.Length; i++)
            {
                float distance = MenuControl.Distance(islands[i].hitbox, ray);
                if (distance < nearest)
                {
                    nearest = distance;
                    result = i;
                }
            }
            return result;
        }

        private static MenuControl ControlAt(MenuControl[] controls, Ray ray)
        {
            MenuControl result = null;
            float nearest = float.PositiveInfinity;
            foreach (var control in controls)
            {
                float distance = MenuControl.Distance(control.hitbox, ray);
                if (distance < nearest)
                {
                    nearest = distance;
                    result = control;
                }
            }
            return result;
        }

        internal void SelectIsland(int index)
        {
            if (CurrentView != View.Overview)
                return;
            selectedIsland = index;
            if (index == 1 && !HasJourney)
                return;
            if ((index is 0 or 1) && !CampaignScenes.CanBeginMenuJourney)
            {
                Flash(presentation.artwork.journeyUnavailable);
                return;
            }
            PlayCue();
            switch (index)
            {
                case 0 when CampaignProgress.HasStoredJourney:
                    ConfirmChoice = 0;
                    MoveTo(View.Confirm, overview, .28f);
                    break;
                case 0:
                    Begin(false);
                    break;
                case 1:
                    Begin(true);
                    break;
                case 2:
                    MoveTo(View.General, general, 1.14f);
                    break;
                case 3:
                    MoveTo(View.Audio, audio, 1.14f);
                    break;
            }
        }

        internal void RespondToReset(bool confirmed)
        {
            if (CurrentView != View.Confirm)
                return;
            if (confirmed) Begin(false);
            else ReturnToOverview();
        }

        private void Begin(bool resume)
        {
            launchRequested = false;
            MoveTo(View.Entering, resume ? continueGame : newGame, 1.55f);
        }

        internal void ReturnToOverview()
        {
            CampaignSettings.SavePending(true);
            dragging = null;
            dragWithTouch = false;
            MoveTo(View.Overview, overview, generalOpen > 0f || audioOpen > 0f ? 1.44f : 1.05f);
        }

        private void MoveTo(View view, MenuCameraPose target, float duration)
        {
            CurrentView = view;
            previous = pose;
            destination = target;
            transitionDuration = reducedMotion ? Mathf.Min(.23f, duration) : duration;
            blend = elapsed = 0f;
            keyboardIndex = -1;
            FocusedIsland = -1;
            FocusedControl = null;
        }

        private void Activate(MenuControl.Action action)
        {
            PlayCue();
            switch (action)
            {
                case MenuControl.Action.Low:
                case MenuControl.Action.Medium:
                case MenuControl.Action.High:
                    CampaignSettings.GraphicsQuality = (CampaignSettings.GraphicsPreset)action;
                    CampaignSettings.SavePending(true);
                    break;
                case MenuControl.Action.Fullscreen:
                    ToggleFullscreen();
                    break;
                case MenuControl.Action.Back:
                    ReturnToOverview();
                    break;
            }
        }

        private static void SetVolume(MenuControl.Action action, float value)
        {
            switch (action)
            {
                case MenuControl.Action.Master: CampaignSettings.MasterVolume = value; break;
                case MenuControl.Action.Music: CampaignSettings.MusicVolume = value; break;
                case MenuControl.Action.Effects: CampaignSettings.EffectsVolume = value; break;
            }
        }

        private static void AdjustFromPointer(MenuControl control, Ray ray)
        {
            if (control.TryVolume(ray, out float value))
                SetVolume(control.action, value);
        }

        private void StartAudio()
        {
            if (music && music.clip && !music.isPlaying) music.Play();
        }

        private void PlayCue()
        {
            if (effects && interactionCue)
                effects.PlayOneShot(interactionCue, .26f);
        }

        private void Flash(string message)
        {
            Status = message;
            statusUntil = Time.unscaledTime + 2.6f;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int MenuFullscreenState();
#endif
        private static bool ReadFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return MenuFullscreenState() > 0;
#else
            return Screen.fullScreen;
#endif
        }

        private void ToggleFullscreen()
        {
#if UNITY_EDITOR
            Flash(presentation.artwork.fullscreenEditor);
#else
#if UNITY_WEBGL
            if (MenuFullscreenState() < 0)
            {
                Flash(presentation.artwork.fullscreenUnsupported);
                return;
            }
#endif
            requestedFullscreen = !ReadFullscreen();
            pendingFullscreen = Time.unscaledTime + 1.25f;
            Screen.fullScreen = requestedFullscreen;
#endif
        }

        private void UpdateFullscreen()
        {
            if (Time.unscaledTime > statusUntil)
                Status = null;
            lastFullscreen = ReadFullscreen();
            if (pendingFullscreen <= 0f)
                return;
            if (lastFullscreen == requestedFullscreen)
                pendingFullscreen = 0f;
            else if (Time.unscaledTime >= pendingFullscreen)
            {
                pendingFullscreen = 0f;
                Flash(presentation.artwork.fullscreenFailed);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                FocusedIsland = -1;
                FocusedControl = null;
                dragging = null;
                dragWithTouch = false;
                CampaignSettings.SavePending(true);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) CampaignSettings.SavePending(true);
        }

        private void OnDestroy() => CampaignSettings.SavePending(true);
    }
}
