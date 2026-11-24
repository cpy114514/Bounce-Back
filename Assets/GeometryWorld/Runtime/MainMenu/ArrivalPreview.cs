using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    public sealed class ArrivalPreview : MonoBehaviour
    {
        public Transform sphere;
        public Font displayFont, labelFont;
        private Vector3 rest;
        private GUIStyle heading, back;
        private float arrived;

        private void Start()
        {
            rest = sphere.localPosition;
            arrived = Time.unscaledTime;
            CampaignScenes.UseMainMenu();
        }

        private void Update()
        {
            sphere.localPosition = rest + Vector3.up * (Mathf.Sin(Time.unscaledTime * 1.4f) * .16f);
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true || keyboard?.enterKey.wasPressedThisFrame == true)
                CampaignScenes.OpenMenu();
        }

        private void OnGUI()
        {
            heading ??= new GUIStyle(GUI.skin.label)
                { font = displayFont, alignment = TextAnchor.MiddleCenter, normal = { textColor = MainMenuUI.Ink } };
            back ??= new GUIStyle(heading) { font = labelFont ? labelFont : displayFont };
            heading.fontSize = Mathf.Clamp(Screen.height / 24, 21, 54);
            GUI.Label(new Rect(0, Screen.height * .16f, Screen.width, 70), "Journey Preview", heading);
            float width = Mathf.Clamp(Screen.width * .22f, 154f, 236f);
            Rect button = new((Screen.width - width) * .5f, Screen.height * .78f, width, 42f);
            bool hovered = button.Contains(Event.current.mousePosition);
            MainMenuUI.Rule(button, MainMenuUI.Ink, hovered ? 1f : .82f);
            back.fontSize = Mathf.Clamp(Screen.height / 38, 15, 22);
            back.normal.textColor = MainMenuUI.Paper;
            GUI.Label(button, "Back to menu", back);
            if (GUI.Button(button, GUIContent.none, GUIStyle.none))
                CampaignScenes.OpenMenu();
            float reveal = 1f - Mathf.Clamp01((Time.unscaledTime - arrived) * 2f);
            if (reveal > 0f && Event.current.type == EventType.Repaint)
            {
                Color color = GUI.color;
                GUI.color = new Color(.11f, .12f, .19f, reveal);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = color;
            }
        }
    }
}
