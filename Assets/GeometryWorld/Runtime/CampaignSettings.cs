using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public static class CampaignSettings
    {
        private const string Prefix = "BounceBack.Settings.";
        private const string MenuQualityKey = Prefix + "HarvestQuality"; // Preserve saved video settings across the menu rename.
        private static bool menuSession;
        private static float saveAfter;

        public enum GraphicsPreset { Low, Medium, High }

        public static GraphicsPreset GraphicsQuality
        {
            get => (GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt(MenuQualityKey,
                PlayerPrefs.GetInt(Prefix + "LowGraphics") != 0 ? 0 : 1), 0, 2);
            set
            {
                PlayerPrefs.SetInt(MenuQualityKey, Mathf.Clamp((int)value, 0, 2));
                PlayerPrefs.SetInt(Prefix + "LowGraphics", value == GraphicsPreset.Low ? 1 : 0);
                ApplyGraphics();
                Changed();
            }
        }

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 0.85f));
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(Prefix + "Volume", AudioListener.volume);
                Changed();
            }
        }

        public static float MusicVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Music", 0.8f));
            set { PlayerPrefs.SetFloat(Prefix + "Music", Mathf.Clamp01(value)); Changed(); }
        }

        public static float EffectsVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Effects", 0.9f));
            set { PlayerPrefs.SetFloat(Prefix + "Effects", Mathf.Clamp01(value)); Changed(); }
        }

        public static bool LowGraphics
        {
            get => PlayerPrefs.GetInt(Prefix + "LowGraphics") != 0;
            set
            {
                PlayerPrefs.DeleteKey(MenuQualityKey);
                PlayerPrefs.SetInt(Prefix + "LowGraphics", value ? 1 : 0);
                ApplyGraphics();
                Changed();
            }
        }

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(Prefix + "ReducedMotion") != 0;
            set { PlayerPrefs.SetInt(Prefix + "ReducedMotion", value ? 1 : 0); Changed(); }
        }

        public static bool ArrowKeys
        {
            get => PlayerPrefs.GetInt(Prefix + "ArrowKeys") != 0;
            set { PlayerPrefs.SetInt(Prefix + "ArrowKeys", value ? 1 : 0); Changed(); }
        }

        internal static void SavePending(bool immediate = false)
        {
            if (saveAfter <= 0f || !immediate && Time.realtimeSinceStartup < saveAfter)
                return;
            saveAfter = 0f;
            PlayerPrefs.Save();
        }

        private static void Changed() => saveAfter = Time.realtimeSinceStartup + 0.45f;

        internal static void BeginMenuSession()
        {
            menuSession = true;
            AudioListener.volume = MasterVolume;
            ApplyGraphics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            menuSession = false;
            saveAfter = 0f;
        }

        internal static void Apply()
        {
            AudioListener.volume = MasterVolume;
            ApplyGraphics();
#if !UNITY_WEBGL && !UNITY_EDITOR
            if (PlayerPrefs.HasKey(Prefix + "Width"))
                Screen.SetResolution(PlayerPrefs.GetInt(Prefix + "Width"), PlayerPrefs.GetInt(Prefix + "Height"),
                    PlayerPrefs.GetInt(Prefix + "Fullscreen") != 0);
#endif
        }

#if !UNITY_WEBGL && !UNITY_EDITOR
        internal static void ApplyDisplay(int width, int height, bool fullscreen)
        {
            Screen.SetResolution(width, height, fullscreen);
            PlayerPrefs.SetInt(Prefix + "Width", width);
            PlayerPrefs.SetInt(Prefix + "Height", height);
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
            Changed();
        }
#endif

        private static void ApplyGraphics()
        {
            var names = QualitySettings.names;
            int level = -1;
            if (menuSession || PlayerPrefs.HasKey(MenuQualityKey))
                level = System.Array.IndexOf(names, "Menu " + GraphicsQuality);
            if (level < 0)
                level = System.Array.IndexOf(names, LowGraphics ? "Mobile" : "PC");
            if (level < 0 && names.Length > 0)
                level = LowGraphics ? 0 : names.Length - 1;
            if (level >= 0 && QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level);
        }

        internal static Vector2 ReadMove(Keyboard keyboard, bool arrows)
        {
            if (keyboard == null)
                return Vector2.zero;
            return arrows
                ? new Vector2(Value(keyboard.rightArrowKey.isPressed) - Value(keyboard.leftArrowKey.isPressed),
                    Value(keyboard.upArrowKey.isPressed) - Value(keyboard.downArrowKey.isPressed))
                : new Vector2(Value(keyboard.dKey.isPressed) - Value(keyboard.aKey.isPressed),
                    Value(keyboard.wKey.isPressed) - Value(keyboard.sKey.isPressed));
        }

        private static int Value(bool pressed) => pressed ? 1 : 0;
    }
}
