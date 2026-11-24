using UnityEngine;

namespace BounceBack.GeometryWorld
{
    internal enum PauseAction { None, Resume, LevelSelect, MainMenu }

    internal static class CourtyardHud
    {
        private static readonly Color Ink = new Color32(29, 56, 78, 255);
        private static readonly Color MutedInk = new Color32(74, 91, 103, 255);
        private static readonly Color Ivory = new Color32(252, 246, 232, 255);
        private static readonly Color Edge = new Color32(211, 190, 150, 255);
        private static readonly Color Brass = new Color32(189, 142, 69, 255);
        private static readonly Color Blue = new Color32(39, 85, 119, 255);
        private static readonly RectOffset Corners = new(6, 6, 6, 6);

        private static Font serif;
        private static GUIStyle panel, button, primary, toggle, title, compactTitle, centerTitle, wordmark, caption, body, instruction, small, tag;

        internal static Font Serif => serif ? serif : serif = Resources.Load<Font>("CourtyardUI/SourceSerif4-Regular");
        internal static GUIStyle Panel => panel ??= CreatePanel();
        internal static GUIStyle Button => button ??= CreateButton(false);
        internal static GUIStyle Primary => primary ??= CreateButton(true);
        internal static GUIStyle Toggle => toggle ??= CreateToggle();
        internal static GUIStyle Title => title ??= Label(25, Ink, TextAnchor.MiddleLeft, Serif);
        internal static GUIStyle CompactTitle => compactTitle ??= new GUIStyle(Title) { fontSize = 19, wordWrap = true };
        internal static GUIStyle CenterTitle => centerTitle ??= Label(25, Ink, TextAnchor.MiddleCenter, Serif);
        internal static GUIStyle Wordmark => wordmark ??= Label(35, Ink, TextAnchor.MiddleCenter, Serif);
        internal static GUIStyle Caption => caption ??= Label(11, MutedInk, TextAnchor.MiddleLeft, bold: true);
        internal static GUIStyle Body => body ??= Label(12, Ink, TextAnchor.MiddleLeft);
        internal static GUIStyle Instruction => instruction ??= new GUIStyle(Body) { wordWrap = true };
        internal static GUIStyle Small => small ??= Label(11, MutedInk, TextAnchor.MiddleLeft);
        internal static GUIStyle Tag => tag ??= Label(12, MutedInk, TextAnchor.MiddleCenter);

        internal static void Card(Rect rect, bool trim = false)
        {
            GUI.Box(rect, GUIContent.none, Panel);
            if (trim)
                Fill(new Rect(rect.x + 13f, rect.y + 1f, rect.width - 26f, 2f), Brass);
        }

        internal static void TagLabel(Rect rect, string text)
        {
            Card(rect);
            GUI.Label(rect, text, Tag);
        }

        internal static void Scrim(float width, float height) =>
            Fill(new Rect(0f, 0f, width, height), new Color(0.10f, 0.19f, 0.27f, 0.43f));

        internal static PauseAction PauseMenu(float width, float height, bool showMainMenu)
        {
            Scrim(width, height);
            float cardHeight = showMainMenu ? 202f : 161f;
            Rect card = new(width * 0.5f - 138f, (height - cardHeight) * 0.5f, 276f, cardHeight);
            Card(card, true);
            GUI.Label(new Rect(card.x, card.y + 11f, card.width, 36f), "Paused", CenterTitle);
            Rect action = new(card.x + 23f, card.y + 55f, card.width - 46f, 32f);
            if (GUI.Button(action, "Resume", Primary))
                return PauseAction.Resume;
            action.y += 42f;
            if (GUI.Button(action, "Level Select", Button))
                return PauseAction.LevelSelect;
            action.y += 42f;
            return showMainMenu && GUI.Button(action, "Main Menu", Button) ? PauseAction.MainMenu : PauseAction.None;
        }

        private static void Fill(Rect rect, Color tint)
        {
            Color previous = GUI.color;
            GUI.color *= tint;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static GUIStyle Label(int size, Color color, TextAnchor anchor, Font font = null, bool bold = false)
        {
            return new GUIStyle
            {
                font = font ? font : GUI.skin.font,
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = anchor,
                clipping = TextClipping.Clip,
                normal = { textColor = color }
            };
        }

        private static GUIStyle CreatePanel() => new()
        {
            border = Corners,
            padding = new RectOffset(16, 16, 14, 14),
            normal = { background = Surface(Ivory, Edge) }
        };

        private static GUIStyle CreateButton(bool prominent)
        {
            Color baseColor = prominent ? Blue : new Color32(249, 241, 221, 255);
            Color hoverColor = prominent ? new Color32(52, 109, 146, 255) : new Color32(255, 251, 241, 255);
            Color activeColor = prominent ? Ink : new Color32(231, 212, 175, 255);
            Color baseText = prominent ? Ivory : Ink;
            var style = new GUIStyle
            {
                font = GUI.skin.font,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = Corners,
                padding = new RectOffset(9, 9, 5, 5),
                normal = { background = Surface(baseColor, prominent ? Blue : Edge), textColor = baseText },
                hover = { background = Surface(hoverColor, Brass), textColor = baseText },
                active = { background = Surface(activeColor, Brass), textColor = baseText }
            };
            style.focused.background = style.hover.background;
            style.focused.textColor = baseText;
            style.onNormal.background = style.active.background;
            style.onHover.background = style.active.background;
            style.onNormal.textColor = style.onHover.textColor = baseText;
            return style;
        }

        private static GUIStyle CreateToggle()
        {
            var style = new GUIStyle(GUI.skin.toggle) { fontSize = 12 };
            style.normal.textColor = style.onNormal.textColor = Ink;
            style.hover.textColor = style.onHover.textColor = Blue;
            style.active.textColor = style.onActive.textColor = Ink;
            style.focused.textColor = style.onFocused.textColor = Ink;
            return style;
        }

        private static Texture2D Surface(Color32 color, Color32 border)
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int horizontal = Mathf.Min(x, size - 1 - x);
                int vertical = Mathf.Min(y, size - 1 - y);
                int corner = horizontal + vertical;
                if (corner >= 4)
                    pixels[y * size + x] = horizontal == 0 || vertical == 0 || corner == 4 ? border : color;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
