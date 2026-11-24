using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class GeometricMenu : MonoBehaviour
    {
        public Camera view;
        public MenuDevice[] devices;
        public WorldEntrance[] city;
        public GeometryPose transition;
        public GeometryPose logoMotion;
        public GameObject logo;
        public GeometryMusicLoop music;
        public GUISkin skin;
        public bool campaignBoot;
        [Min(0.15f)] public float transitionDuration = 1.4f;
        internal bool SettingsVisible => settings;
        internal bool NavigationAvailable => progress != null && !busy && !settings && !quitPrompt && !sessionEnded;
        internal MenuDevice FocusedDevice => focus;

        private bool busy, settings, sessionEnded, quitPrompt, reducedMotion;
        private int keyboardChoice = -1, settingsTab;
        private MenuDevice focus;
        private Vector2 lastPointer;
        private CampaignProgress progress;
        private bool opening;
        private Vector3 originalViewPosition;
        private Quaternion originalViewRotation;
        private float originalViewSize;
        private float courtyardLoadStarted = float.NegativeInfinity;
#if !UNITY_WEBGL && !UNITY_EDITOR
        private Resolution[] displayModes;
#endif

        private void Start()
        {
            CampaignScenes.UseFullCampaign(campaignBoot);
            CampaignSettings.Apply();
            reducedMotion = CampaignSettings.ReducedMotion;
            if (view)
            {
                originalViewPosition = view.transform.position;
                originalViewRotation = view.transform.rotation;
                originalViewSize = view.orthographicSize;
            }
            if (music)
                music.ReduceMotion = reducedMotion;
            progress = CampaignProgress.Load();
            foreach (var district in city)
                if (district)
                    district.ShowProgress(progress, true);
#if !UNITY_WEBGL && !UNITY_EDITOR
            displayModes = Screen.resolutions;
#endif
            StartCoroutine(Opening());
        }

        private IEnumerator Opening()
        {
            busy = true;
            opening = true;
            if (logo)
                logo.SetActive(true);
            yield return Animate(logoMotion, 0f, 1f, reducedMotion ? 0.22f : 1.2f);
            if (logo)
                logo.SetActive(false);
            opening = false;
            if (!progress.IsCompleted(0))
            {
                if (campaignBoot)
                {
                    yield return EnterCourtyard(System.Array.Find(devices, device => device && device.action == MenuAction.Start));
                    yield break;
                }
                if (CampaignScenes.Enter(0))
                    yield break;
            }
            busy = false;
        }

        private void Update()
        {
            CampaignSettings.SavePending();
            Pointer pointer = Pointer.current;
            Keyboard keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true && CloseOverlay())
                return;
            if (music && (pointer?.press.wasPressedThisFrame == true || keyboard?.anyKey.wasPressedThisFrame == true))
                music.StartAudio(progress.CompletedCount + 1);
            if (!NavigationAvailable)
                return;

            Vector2 position = pointer?.position.ReadValue() ?? lastPointer;
            if ((position - lastPointer).sqrMagnitude > 0.25f || pointer?.press.wasPressedThisFrame == true)
                keyboardChoice = -1;
            lastPointer = position;
            if (keyboard?.tabKey.wasPressedThisFrame == true)
                FocusNextDevice(keyboard.shiftKey.isPressed ? -1 : 1);

            focus = keyboardChoice < 0 ? Pick(position) : devices[keyboardChoice];
            foreach (var device in devices)
                device.SetFocus(device == focus, reducedMotion);
            if (pointer?.press.wasPressedThisFrame == true || keyboard?.enterKey.wasPressedThisFrame == true)
                ActivateFocused();
        }

        internal bool FocusNextDevice(int direction = 1)
        {
            if (!NavigationAvailable || devices.Length == 0)
                return false;
            keyboardChoice = keyboardChoice < 0 ? (direction < 0 ? devices.Length - 1 : 0) :
                (keyboardChoice + (direction < 0 ? -1 : 1) + devices.Length) % devices.Length;
            focus = devices[keyboardChoice];
            return focus;
        }

        internal bool ActivateFocused()
        {
            if (!NavigationAvailable || !focus)
                return false;
            StartCoroutine(Open(focus.action));
            return true;
        }

        internal bool CloseOverlay()
        {
            if (busy || !(settings || quitPrompt || sessionEnded))
                return false;
            StartCoroutine(ClosePanel());
            return true;
        }

        private void OnDestroy() => CampaignSettings.SavePending(true);
        private void OnApplicationFocus(bool focused) { if (!focused) CampaignSettings.SavePending(true); }

        private MenuDevice Pick(Vector2 position)
        {
            return view && Physics.Raycast(view.ScreenPointToRay(position), out var hit, view.farClipPlane)
                ? hit.collider.GetComponentInParent<MenuDevice>() : null;
        }

        private IEnumerator Open(MenuAction action)
        {
            busy = true;
            if (campaignBoot && (action == MenuAction.Start || action == MenuAction.Continue && !progress.IsCompleted(0)))
            {
                yield return EnterCourtyard(focus);
                yield break;
            }
            yield return AnimateMenu(0f, 1f);
            switch (action)
            {
                case MenuAction.Continue:
                    if (progress.IsCompleted(0) ? CampaignScenes.OpenWorld() : CampaignScenes.Enter(0))
                        yield break;
                    break;
                case MenuAction.LevelSelect:
                    if (CampaignScenes.OpenWorld(0))
                        yield break;
                    break;
                case MenuAction.Start:
                    if (CampaignScenes.Enter(0))
                        yield break;
                    break;
                case MenuAction.Settings:
                    settings = true;
                    busy = false;
                    yield break;
                case MenuAction.Quit:
                    quitPrompt = true;
                    busy = false;
                    yield break;
            }
            yield return ClosePanel();
        }

        private IEnumerator EnterCourtyard(MenuDevice gateway)
        {
            Renderer core = gateway ? gateway.glowRenderer : null;
            if (!reducedMotion && view && core && CampaignScenes.HasSystemsScene)
            {
                var heading = Quaternion.AngleAxis(-5f, Vector3.up) * originalViewRotation;
                float distance = Mathf.Max(4f, Vector3.Dot(core.bounds.center - originalViewPosition,
                    originalViewRotation * Vector3.forward));
                Vector3 wideFocus = originalViewPosition + originalViewRotation * Vector3.forward * distance;
                Vector3 extents = core.bounds.extents;
                float closeSize = Mathf.Min(originalViewSize, Mathf.Max(extents.x, extents.y, extents.z) * CampaignScenes.ArrivalFraming);
                yield return ArchitectureMotion.Play(0f, 1f, transitionDuration, fraction =>
                {
                    gateway.SetFocus(true, false);
                    transition?.Apply(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.86f, fraction)));
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, fraction));
                    Quaternion rotation = Quaternion.Slerp(originalViewRotation, heading, push);
                    Vector3 focusPoint = Vector3.Lerp(wideFocus, core.bounds.center, push);
                    view.transform.SetPositionAndRotation(focusPoint - rotation * Vector3.forward * distance, rotation);
                    view.orthographicSize = Mathf.Lerp(originalViewSize, closeSize, push * push);
                });
                if (CampaignScenes.EnterFromMenu(view.transform.rotation))
                {
                    courtyardLoadStarted = Time.unscaledTime;
                    yield break;
                }
            }
            else
            {
                yield return AnimateMenu(0f, 1f);
                if (CampaignScenes.Enter(0))
                    yield break;
            }
            yield return ClosePanel();
        }

        private IEnumerator ClosePanel()
        {
            busy = true;
            settings = quitPrompt = sessionEnded = false;
            PlayerPrefs.Save();
            yield return AnimateMenu(1f, 0f);
            busy = false;
        }

        private IEnumerator AnimateMenu(float start, float end)
        {
            if (!campaignBoot || !view)
            {
                yield return Animate(transition, start, end, reducedMotion ? 0.2f : transitionDuration);
                yield break;
            }
            Vector3 slide = originalViewRotation * new Vector3(0.42f, 0.20f, 1.2f);
            yield return ArchitectureMotion.Play(start, end, transitionDuration, fraction =>
            {
                float amount = Mathf.SmoothStep(0f, 1f, fraction);
                transition?.Apply(amount);
                float cameraAmount = reducedMotion ? 0f : amount;
                view.transform.position = originalViewPosition + slide * cameraAmount;
                view.orthographicSize = originalViewSize * Mathf.Lerp(1f, 0.88f, cameraAmount);
            });
        }

        private static IEnumerator Animate(GeometryPose pose, float from, float to, float duration)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                if (pose)
                    pose.Apply(Mathf.SmoothStep(from, to, time / duration));
                yield return null;
            }
            if (pose)
                pose.Apply(to);
        }

        private void OnGUI()
        {
            var original = GUI.matrix;
            var originalSkin = GUI.skin;
            if (skin)
                GUI.skin = skin;
            float scale = GeometryLevel.HudScale;
            GUI.matrix = original * Matrix4x4.Scale(Vector3.one * scale);
            try
            {
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (busy)
                {
                    if (campaignBoot && opening)
                        GUI.Label(new Rect(width * 0.5f - 180f, height * 0.69f, 360f, 49f), "Bounce Back", CourtyardHud.Wordmark);
                    if (CampaignScenes.IsLoading && Time.unscaledTime - courtyardLoadStarted > 0.65f)
                        CourtyardHud.TagLabel(new Rect(width * 0.5f - 63f, height - 55f, 126f, 32f), "Loading…");
                    return;
                }
                if (settings || quitPrompt || sessionEnded)
                {
                    CourtyardHud.Scrim(width, height);
                    Rect window = new(width * 0.5f - 175f, height * 0.5f - 137f, 350f, 274f);
                    CourtyardHud.Card(window, true);
                    GUILayout.BeginArea(new Rect(window.x + 18f, window.y + 9f, window.width - 36f, window.height - 22f));
                    GUILayout.Label(settings ? "Settings" : sessionEnded ? "Session ended" : "End this session?",
                        CourtyardHud.CenterTitle, GUILayout.Height(38f));
                    if (settings)
                        DrawSettings();
                    else if (sessionEnded)
                    {
                        if (GUILayout.Button("Return to Menu", CourtyardHud.Primary, GUILayout.Height(32f)))
                            StartCoroutine(ClosePanel());
                    }
                    else
                    {
                        if (GUILayout.Button("End Session", CourtyardHud.Primary, GUILayout.Height(32f)))
                        {
                            PlayerPrefs.Save();
#if !UNITY_WEBGL && !UNITY_EDITOR
                            Application.Quit();
#endif
                            quitPrompt = false;
                            sessionEnded = true;
                        }
                    }
                    if (!sessionEnded && GUILayout.Button("Back", CourtyardHud.Button, GUILayout.Height(31f)))
                        StartCoroutine(ClosePanel());
                    GUILayout.EndArea();
                }
                else if (focus)
                {
                    Vector3 point = view.WorldToScreenPoint(focus.transform.position);
                    CourtyardHud.TagLabel(new Rect(point.x / scale - 70f, (Screen.height - point.y) / scale + 35f, 140f, 30f), focus.Label);
                }
            }
            finally
            {
                GUI.matrix = original;
                GUI.skin = originalSkin;
            }
        }

        private static readonly string[] Tabs = { "Audio", "Graphics", "Controls" };

        private void DrawSettings()
        {
            settingsTab = GUILayout.Toolbar(settingsTab, Tabs, CourtyardHud.Button, GUILayout.Height(31f));
            GUILayout.Space(12f);
            switch (settingsTab)
            {
                case 0:
                    float master = VolumeSlider("Master", CampaignSettings.MasterVolume);
                    float musicVolume = VolumeSlider("Music", CampaignSettings.MusicVolume);
                    float effects = VolumeSlider("Sound effects", CampaignSettings.EffectsVolume);
                    if (master != CampaignSettings.MasterVolume) CampaignSettings.MasterVolume = master;
                    if (musicVolume != CampaignSettings.MusicVolume) CampaignSettings.MusicVolume = musicVolume;
                    if (effects != CampaignSettings.EffectsVolume) CampaignSettings.EffectsVolume = effects;
                    break;
                case 1:
                    bool low = CampaignSettings.LowGraphics;
                    bool adjustedQuality = GUILayout.Toggle(low, "Low graphics quality", CourtyardHud.Toggle);
                    if (adjustedQuality != low)
                        CampaignSettings.LowGraphics = adjustedQuality;
                    bool motion = GUILayout.Toggle(reducedMotion, "Reduce motion", CourtyardHud.Toggle);
                    if (motion != reducedMotion)
                    {
                        CampaignSettings.ReducedMotion = reducedMotion = motion;
                        if (music)
                            music.ReduceMotion = motion;
                    }
#if !UNITY_WEBGL && !UNITY_EDITOR
                    DrawDisplaySettings();
#endif
                    break;
                case 2:
                    if (campaignBoot || CampaignScenes.HasSystemsScene)
                    {
                        GUILayout.Label("A / Left   ·   Previous level", CourtyardHud.Body, GUILayout.Height(20f));
                        GUILayout.Label("D / Right   ·   Next level", CourtyardHud.Body, GUILayout.Height(20f));
                        GUILayout.Label("E / Enter   ·   Enter level", CourtyardHud.Body, GUILayout.Height(20f));
                        GUILayout.Label("Wheel   ·   Zoom     Home   ·   Reset view", CourtyardHud.Body, GUILayout.Height(20f));
                    }
                    else
                    {
                        bool arrows = CampaignSettings.ArrowKeys;
                        bool adjustedKeys = GUILayout.Toggle(arrows, "Use arrow keys instead of WASD", CourtyardHud.Toggle);
                        if (adjustedKeys != arrows)
                            CampaignSettings.ArrowKeys = adjustedKeys;
                        GUILayout.Label("Click a walkway to move. E or Enter opens an entrance.",
                            CourtyardHud.Instruction, GUILayout.Height(35f));
                    }
                    break;
            }
            GUILayout.FlexibleSpace();
        }

        private static float VolumeSlider(string label, float volume)
        {
            GUILayout.Label($"{label}  {Mathf.RoundToInt(volume * 100)}%", CourtyardHud.Body, GUILayout.Height(19f));
            return GUILayout.HorizontalSlider(volume, 0f, 1f);
        }

#if !UNITY_WEBGL && !UNITY_EDITOR
        private void DrawDisplaySettings()
        {
            bool fullscreen = GUILayout.Toggle(Screen.fullScreen, "Fullscreen", CourtyardHud.Toggle);
            int width = Screen.width, height = Screen.height;
            if (displayModes.Length != 0 && GUILayout.Button($"{width} × {height}  ·  Next resolution", CourtyardHud.Button))
            {
                int current = System.Array.FindLastIndex(displayModes, mode => mode.width == width && mode.height == height);
                Resolution next = displayModes[(current + 1) % displayModes.Length];
                width = next.width;
                height = next.height;
            }
            if (fullscreen != Screen.fullScreen || width != Screen.width || height != Screen.height)
                CampaignSettings.ApplyDisplay(width, height, fullscreen);
        }
#endif
    }
}
