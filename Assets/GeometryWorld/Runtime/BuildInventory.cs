using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BounceBack.GeometryWorld
{
    internal sealed class BuildInventory
    {
        private static readonly string[] Defaults =
            { "Block", "Platform", "Ramp", "Column", "Cylinder", "Sphere", "Elastic Object", "Bounce Pad", "Goal" };
        private static readonly string[] Categories = { "All", "Shapes", "Gadgets" };
        private readonly RuntimeBuildCatalog catalog = RuntimeBuildCatalog.Load();
        private readonly RuntimeBuildCatalog.Item[] slots = new RuntimeBuildCatalog.Item[9];
        private int slot, category;
        private Vector2 scroll;

        internal BuildInventory()
        {
            if (!catalog)
                return;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = catalog.Find(Defaults[i]);
        }

        internal RuntimeBuildCatalog.Item Selected => slots[slot];

        internal void Read(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    KeyControl key = keyboard[(Key)((int)Key.Digit1 + i)];
                    if (key.wasPressedThisFrame)
                        slot = i;
                }
            }
            if (mouse == null)
                return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
                slot = (slot + (wheel > 0f ? -1 : 1) + slots.Length) % slots.Length;
        }

        internal void DrawHotbar(float scale, bool clickable)
        {
            float width = Mathf.Min(684f, Screen.width / scale - 24f);
            float cell = width / slots.Length;
            float left = (Screen.width / scale - width) * 0.5f;
            float top = Screen.height / scale - 64f;
            for (int i = 0; i < slots.Length; i++)
            {
                var rect = new Rect(left + cell * i, top, cell - 3f, 50f);
                Color color = GUI.color;
                GUI.color = i == slot ? new Color(0.59f, 0.95f, 0.78f) : new Color(0.93f, 0.94f, 0.98f);
                string label = slots[i]?.label ?? "";
                if (clickable)
                {
                    if (GUI.Button(rect, $"{i + 1}\n{label}"))
                        slot = i;
                }
                else
                    GUI.Box(rect, $"{i + 1}\n{label}");
                GUI.color = color;
            }
        }

        internal void DrawBackpack(float scale)
        {
            float width = Mathf.Min(620f, Screen.width / scale - 24f);
            float height = Mathf.Min(326f, Screen.height / scale - 216f);
            var panel = new Rect((Screen.width / scale - width) * 0.5f, (Screen.height / scale - height) * 0.42f,
                width, Mathf.Max(105f, height));
            GUI.Box(panel, $"Backpack · Assign to slot {slot + 1}");
            category = GUI.Toolbar(new Rect(panel.x + 12f, panel.y + 28f, panel.width - 24f, 27f), category, Categories);
            if (!catalog)
                return;

            const int columns = 4;
            int count = 0;
            foreach (var item in catalog.items)
                if (Includes(item))
                    count++;
            float cell = (panel.width - 44f) / columns;
            var viewport = new Rect(panel.x + 12f, panel.y + 61f, panel.width - 24f, panel.height - 69f);
            scroll = GUI.BeginScrollView(viewport, scroll,
                new Rect(0f, 0f, cell * columns, Mathf.CeilToInt(count / (float)columns) * 45f));
            int index = 0;
            foreach (var item in catalog.items)
            {
                if (!Includes(item))
                    continue;
                var card = new Rect(index % columns * cell, index / columns * 45f, cell - 5f, 40f);
                if (GUI.Button(card, item.label))
                    slots[slot] = item;
                index++;
            }
            GUI.EndScrollView();
        }

        private bool Includes(RuntimeBuildCatalog.Item item) => item.prefab && (category == 0 || item.gadget == (category == 2));
    }
}
