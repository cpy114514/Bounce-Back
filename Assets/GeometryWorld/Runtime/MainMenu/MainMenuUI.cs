using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class MainMenuUI : MonoBehaviour
    {
        public MainMenu menu;
        public MenuArtwork artwork;

        internal static readonly Color Ink = new Color32(38, 56, 56, 255);
        internal static readonly Color Paper = new Color32(240, 233, 215, 255);
        private static readonly Color Accent = new Color32(121, 68, 49, 255);
        private static readonly Color Muted = new Color32(65, 83, 81, 255);
        private static readonly Color Night = new(.10f, .17f, .18f);
        private GUIStyle serif, centeredTitle, center, right, paragraph;
        private float scale;

        private float DisplayScale => Mathf.Clamp(Mathf.Min(Screen.height / 800f, Screen.width / 1000f), .60f, 2.6f);
        private Rect SkipArea => new(Screen.width - 178f * DisplayScale, Screen.height - 63f * DisplayScale,
            150f * DisplayScale, 45f * DisplayScale);

        internal bool InSkipArea(Vector2 screenPoint) =>
            SkipArea.Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y));

        internal int CaptionAt(Vector2 screenPoint)
        {
            Vector2 point = new(screenPoint.x, Screen.height - screenPoint.y);
            for (int i = 0; i < menu.islands.Length; i++)
                if (CaptionRect(menu.islands[i], out Rect rect) && rect.Contains(point))
                    return i;
            return -1;
        }

        internal MenuControl ControlCaptionAt(MenuControl[] controls, Vector2 screenPoint)
        {
            Vector2 point = new(screenPoint.x, Screen.height - screenPoint.y);
            foreach (var control in controls)
                if (!control.IsRail && Anchor(control.label, out Vector2 anchor) &&
                    ControlCaptionRect(control, anchor).Contains(point))
                    return control;
            return null;
        }

        private void OnGUI()
        {
            if (!menu || !Application.isPlaying)
                return;
            EnsureStyles();
            switch (menu.CurrentView)
            {
                case MainMenu.View.Intro:
                    Text(SkipArea, artwork.skipIntro, center, 16, Ink, .86f);
                    break;
                case MainMenu.View.General:
                    DrawSubmenu(artwork.general, menu.generalControls, menu.SubmenuAlpha);
                    break;
                case MainMenu.View.Audio:
                    DrawSubmenu(artwork.audio, menu.audioControls, menu.SubmenuAlpha);
                    break;
                default:
                    DrawOverview(menu.OverviewAlpha);
                    break;
            }
            if (menu.CurrentView == MainMenu.View.Confirm)
                DrawConfirmation();
            if (menu.JourneyVeil > 0f)
                Rule(new Rect(0, 0, Screen.width, Screen.height), Night, menu.JourneyVeil);
            if (!string.IsNullOrEmpty(menu.Status))
                Text(new Rect(0, Screen.height - 70f * scale, Screen.width, 40f * scale),
                    menu.Status, center, 17, Ink, 1f);
        }

        private void DrawOverview(float alpha)
        {
            if (alpha <= 0f || menu.CurrentView != MainMenu.View.Overview)
                return;
            for (int i = 0; i < menu.islands.Length; i++)
            {
                if (!CaptionRect(menu.islands[i], out Rect rect))
                    continue;
                bool focused = i == menu.FocusedIsland;
                bool unavailable = i == 1 && !menu.HasJourney;
                string label = i switch { 0 => artwork.newGame, 1 => artwork.continueGame, 2 => artwork.general, _ => artwork.audio };
                Text(rect, label, center, 22, unavailable ? Muted : focused ? Accent : Ink, alpha);
                if (focused)
                {
                    DrawMarker(new Vector2(rect.center.x, rect.yMax), alpha);
                    if (unavailable)
                        Text(new Rect(rect.x, rect.yMax + 3f * scale, rect.width, 23f * scale),
                            artwork.noJourney, center, 15, Muted, alpha);
                }
            }
        }

        private bool CaptionRect(MenuIsland island, out Rect rect)
        {
            bool visible = Anchor(island.label, out Vector2 point);
            float islandCenter = menu.menuCamera.WorldToScreenPoint(island.transform.position).x;
            float size = DisplayScale;
            float width = 132f * size;
            float height = 32f * size;
            rect = new Rect(Mathf.Clamp(islandCenter - width * .5f, 8f, Screen.width - width - 8f),
                Mathf.Clamp(point.y + 12f * size, 0f, Screen.height - height - 12f * size), width, height);
            return visible;
        }

        private void DrawSubmenu(string title, MenuControl[] controls, float alpha)
        {
            if (alpha <= 0f)
                return;
            float x = Screen.width * .07f;
            float y = Screen.height * .132f;
            Text(new Rect(x, y, 245f * scale, 58f * scale), title, serif, 43, Ink, alpha);
            float railLeft = Screen.width;
            float railRight = 0f;
            foreach (var control in controls)
            {
                if (!control.IsRail || !Anchor(control.label, out Vector2 label) || !RailEnd(control, out Vector2 end))
                    continue;
                railLeft = Mathf.Min(railLeft, label.x);
                railRight = Mathf.Max(railRight, end.x);
            }
            float gutter = 10f * scale;
            foreach (var control in controls)
            {
                if (!Anchor(control.label, out Vector2 position))
                    continue;
                float value = menu.Value(control.action);
                string label = control.action switch
                {
                    MenuControl.Action.Low => artwork.low,
                    MenuControl.Action.Medium => artwork.medium,
                    MenuControl.Action.High => artwork.high,
                    MenuControl.Action.Fullscreen => value > 0f ? artwork.fullscreenOn : artwork.fullscreenOff,
                    MenuControl.Action.Master => artwork.master,
                    MenuControl.Action.Music => artwork.music,
                    MenuControl.Action.Effects => artwork.effects,
                    _ => artwork.back
                };
                bool selected = value > 0f && (control.action is
                    MenuControl.Action.Low or MenuControl.Action.Medium or MenuControl.Action.High);
                Color color = menu.FocusedControl == control || selected ? Accent : Ink;
                if (control.IsRail)
                {
                    float left = Mathf.Max(gutter, railLeft - 138f * scale);
                    float numberX = Mathf.Min(railRight + gutter, Screen.width - 76f * scale);
                    Text(new Rect(left, position.y - 14f * scale, railLeft - gutter - left, 28f * scale),
                        label, right, 19, color, alpha);
                    if (RailEnd(control, out Vector2 number))
                        Text(new Rect(numberX, number.y - 14f * scale, 66f * scale, 28f * scale),
                            $"{Mathf.RoundToInt(value * 100f)}%", right, 19, color, alpha);
                }
                else
                {
                    Rect caption = ControlCaptionRect(control, position);
                    Text(caption, label, center, 18, color, alpha);
                    if (selected)
                        Rule(new Rect(caption.center.x - 13f * scale, caption.yMax, 26f * scale, 1f), color, alpha);
                }
            }
        }

        private Rect ControlCaptionRect(MenuControl control, Vector2 position)
        {
            float size = DisplayScale;
            float width = (control.action == MenuControl.Action.Fullscreen ? 150f : 82f) * size;
            float offset = control.action switch
            {
                MenuControl.Action.Back => 35f,
                MenuControl.Action.Fullscreen => 18f,
                _ => 1f
            };
            float height = 26f * size;
            return new Rect(position.x - width * .5f, Mathf.Min(position.y + offset * size, Screen.height - height - 8f),
                width, height);
        }

        private bool RailEnd(MenuControl control, out Vector2 point) =>
            Anchor(control.transform.TransformPoint(new Vector3(2.24f, .30f, 0f)), out point);

        private void DrawConfirmation()
        {
            Rule(new Rect(0, 0, Screen.width, Screen.height), Night, .64f);
            float w = Mathf.Min(Screen.width - 32f * scale, 504f * scale);
            float x = (Screen.width - w) * .5f;
            float y = (Screen.height - 215f * scale) * .5f;
            Rule(new Rect(x, y, w, 215f * scale), Paper, 1f);
            Text(new Rect(x, y + 23f * scale, w, 49f * scale), artwork.confirmTitle, centeredTitle, 34, Ink, 1f);
            Text(new Rect(x + 23f * scale, y + 77f * scale, w - 46f * scale, 49f * scale), artwork.confirmMessage,
                paragraph, 19, Ink, 1f);
            float gap = 8f * scale;
            for (int index = 0; index < 2; index++)
            {
                float width = 152f * scale;
                float middle = Screen.width * .5f;
                var rect = new Rect(index == 0 ? middle - width - gap : middle + gap,
                    y + 148f * scale, width, 42f * scale);
                bool focused = menu.ConfirmChoice == index;
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    menu.RespondToReset(index == 1);
                if (focused) Rule(rect, index == 0 ? Ink : Accent, 1f);
                else if (rect.Contains(Event.current.mousePosition)) Rule(rect, Muted, .14f);
                Text(rect, index == 0 ? artwork.keepJourney : artwork.startOver, center, 19,
                    focused ? Paper : index == 0 ? Ink : Accent, 1f);
            }
        }

        private bool Anchor(Transform anchor, out Vector2 position) => Anchor(anchor.position, out position);

        private bool Anchor(Vector3 anchor, out Vector2 position)
        {
            Vector3 point = menu.menuCamera.WorldToScreenPoint(anchor);
            position = new Vector2(point.x, Screen.height - point.y);
            return point.z > 0f;
        }

        private void DrawMarker(Vector2 position, float alpha)
        {
            if (artwork.focusMarker)
                Sprite(new Rect(position.x - 6f, position.y - 6f, 12f, 12f), artwork.focusMarker, alpha);
            else
                Rule(new Rect(position.x - 12f * scale, position.y, 24f * scale, 1), Accent, alpha);
        }

        private void EnsureStyles()
        {
            scale = DisplayScale;
            if (serif != null)
                return;
            serif = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, font = artwork.displayFont, wordWrap = false };
            centeredTitle = new GUIStyle(serif) { alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(serif) { alignment = TextAnchor.MiddleCenter, font = artwork.labelFont };
            right = new GUIStyle(center) { alignment = TextAnchor.MiddleRight };
            paragraph = new GUIStyle(center) { wordWrap = true };
        }

        private void Text(Rect rect, string value, GUIStyle style, int size, Color color, float opacity)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            style.fontSize = Mathf.RoundToInt(size * scale);
            color.a *= opacity;
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }

        internal static void Rule(Rect rect, Color color, float alpha)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Color original = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }

        private static void Sprite(Rect rect, Sprite sprite, float alpha)
        {
            Texture2D texture = sprite.texture;
            Rect pixels = sprite.textureRect;
            Rect uv = new(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float width = Mathf.Min(rect.width, rect.height * (pixels.width / pixels.height));
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, width, width * pixels.height / pixels.width), texture, uv);
            GUI.color = old;
        }
    }
}
