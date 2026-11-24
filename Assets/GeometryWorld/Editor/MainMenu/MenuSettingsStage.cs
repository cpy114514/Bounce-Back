using System.Linq;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MenuSettingsStage
    {
        private static readonly Color Ivory = new Color32(227, 214, 190, 255);
        private static readonly Color Sand = new Color32(187, 166, 145, 255);
        private static readonly Color Jade = new Color32(157, 174, 151, 255);
        private static readonly Color Patina = new Color32(75, 104, 106, 255);
        private static readonly Color Gold = new Color32(162, 122, 78, 255);
        private static readonly Color BlueGray = new Color32(157, 177, 175, 255);

        internal static MenuSettingsDrawer Build(Transform island, bool audio)
        {
            Transform shelf = island.Find("Recessed stone shelf");
            Transform housing = MenuModel.Child(shelf ? shelf : island, audio ? "Harmonic drawer" : "Video drawer");
            var drawer = housing.gameObject.AddComponent<MenuSettingsDrawer>();
            var m = new MenuModel(housing, new MenuAssetMeshes("Settings Stage"), 0);
            var deckColor = audio ? BlueGray : Jade;
            Transform deck = drawer.tray = MenuModel.Child(housing, "Sliding tray", new(0, -.45f, 0));
            m.Box(deck, "Stone tray", new(0, -.35f, -.64f), new(4.86f, .14f, 3.20f), Sand);
            m.Box(deck, "Drawer front", new(0, -.17f, -2.26f), new(4.82f, .37f, .14f), Sand);
            m.Box(deck, "Bronze pull", new(0, -.23f, -2.365f), new(.88f, .09f, .10f), Gold);
            drawer.runners = new Transform[2];
            drawer.catches = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                int side = 2 * i - 1;
                float x = side * 1.89f;
                m.Box(housing, "Rail bearing", new(x, -.65f, -2.18f), new(.32f, .25f, .20f), Patina);
                drawer.runners[i] = m.Box(housing, "Telescoping bronze runner", new(x, -.65f, -1.18f),
                    new(.15f, .13f, 2.15f), Gold);
                Transform hinge = drawer.catches[i] = MenuModel.Child(housing, "Pivoting catch", new(x, -.34f, -2.38f));
                m.Box(hinge, "Latch", new(0, -.13f, 0), new(.17f, .29f, .12f), Gold);
                m.Box(deck, "Stone side guide", new(side * 2.33f, .012f, -.64f), new(.055f, .044f, 3.08f), Ivory);
            }
            drawer.controls = audio ? MakeAudio(m, deck) : MakeVideo(m, deck);
            BuildSlottedSurface(m, deck, drawer.controls, deckColor, audio);
            return drawer;
        }

        private static void BuildSlottedSurface(MenuModel model, Transform tray, MenuControl[] controls,
            Color surface, bool audio)
        {
            var slots = controls.Select((control, index) =>
            {
                Vector3 center = control.transform.localPosition;
                float width = audio && index < 3 ? 4.42f : control.action == MenuControl.Action.Fullscreen ? 1.23f : .94f;
                return new Rect(center.x - width * .5f, center.z - .29f, width, .58f);
            }).ToArray();
            float[] seams = slots.SelectMany(slot => new[] { slot.yMin, slot.yMax })
                .Append(-2.22f).Append(.93f).Distinct().OrderBy(value => value).ToArray();
            for (int i = 1; i < seams.Length; i++)
            {
                float back = seams[i - 1];
                float front = seams[i];
                float mid = .5f * (back + front);
                float left = -2.35f;
                foreach (Rect slot in slots.Where(slot => mid > slot.yMin && mid < slot.yMax).OrderBy(slot => slot.xMin))
                {
                    Strip(left, slot.xMin, back, front);
                    left = slot.xMax;
                }
                Strip(left, 2.35f, back, front);
            }
            foreach (Rect slot in slots)
                model.Box(tray, "Dark well", new(slot.center.x, -.25f, slot.center.y),
                    new(slot.width, .025f, slot.height), Patina);

            void Strip(float left, float right, float back, float front)
            {
                if (right - left > .001f)
                    model.Box(tray, "Stone around lift wells", new((left + right) * .5f, -.055f, (back + front) * .5f),
                        new(right - left, .11f, front - back), surface);
            }
        }

        private static MenuControl[] MakeVideo(MenuModel m, Transform deck)
        {
            var controls = new MenuControl[5];
            for (int i = 0; i < 3; i++)
            {
                var control = Control(deck, (MenuControl.Action)i, new((i - 1) * 1.45f, 0, -.02f),
                    new(1.36f, .78f, .91f), new(0, .05f, -.52f));
                controls[i] = control;
                Plinth(m, control.transform, .87f);
                var shapes = control.model;
                m.Box(shapes, "Footing", new(0, .22f, 0), new(.54f, .10f, .48f), Jade);
                for (int layer = 0; layer <= i; layer++)
                {
                    float size = .46f - layer * .11f;
                    m.Box(shapes, "Stacked limestone", new(0, .36f + .17f * layer, 0), new(size, .18f, size), Ivory);
                }
            }

            var fullscreen = Control(deck, MenuControl.Action.Fullscreen, new(-.88f, 0, -1.43f),
                new(1.52f, .65f, .94f), new(0, .06f, -.60f));
            controls[3] = fullscreen;
            Plinth(m, fullscreen.transform, 1.14f);
            fullscreen.corners = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1 : 1;
                float z = i / 2 == 0 ? -1 : 1;
                Transform bracket = MenuModel.Child(fullscreen.model, "Full screen bracket", new(x * .30f, 0, z * .25f));
                fullscreen.corners[i] = bracket;
                m.Box(bracket, "Horizontal", new(-x * .065f, .27f, 0), new(.19f, .14f, .075f), Ivory);
                m.Box(bracket, "Vertical", new(0, .27f, -z * .06f), new(.075f, .14f, .18f), Ivory);
            }
            m.Box(fullscreen.model, "Inner screen", new(0, .20f, 0), new(.26f, .10f, .19f), Jade);
            controls[4] = Back(m, deck, new(.96f, 0, -1.43f));
            return controls;
        }

        private static MenuControl[] MakeAudio(MenuModel m, Transform deck)
        {
            var controls = new MenuControl[4];
            for (int i = 0; i < 3; i++)
            {
                var action = (MenuControl.Action)((int)MenuControl.Action.Master + i);
                var rail = Control(deck, action, new(0, 0, .30f - .73f * i),
                    new(4.33f, .55f, .73f), new(-2.16f, .30f, 0));
                controls[i] = rail;
                Transform model = rail.model;
                rail.railHalfLength = 1.36f;
                m.Box(model, "Recessed rail bed", new(0, .055f, 0), new(3.14f, .145f, .41f), Patina);
                m.Box(model, "Raised rail", new(0, .173f, 0), new(2.82f, .065f, .09f), Ivory);
                rail.handle = MenuModel.Child(model, "Volume handle", new(0, .235f, 0));
                m.Sphere(rail.handle, "Ivory handle", Vector3.zero, Vector3.one * .28f, Ivory);
                m.Cylinder(rail.handle, "Bronze axle", new(0, -.091f, 0), .118f, .051f, Gold);
                MakeAudioSymbol(m, model, i);
            }
            controls[3] = Back(m, deck, new(0, 0, -1.87f));
            return controls;
        }

        private static void MakeAudioSymbol(MenuModel m, Transform parent, int index)
        {
            Transform icon = MenuModel.Child(parent, "Carved audio symbol", new(-1.90f, .105f, 0));
            m.Cylinder(icon, "Symbol tile", Vector3.zero, .22f, .15f, BlueGray, 8);
            if (index == 0)
            {
                m.Box(icon, "Speaker", new(-.066f, .12f, 0), new(.09f, .06f, .12f), Ivory);
                m.Box(icon, "Horn", new(.02f, .12f, 0), new(.17f, .06f, .18f), Ivory, new(0, 45, 0));
            }
            else if (index == 1)
            {
                m.Cylinder(icon, "Notehead", new(-.053f, .12f, -.061f), .075f, .06f, Ivory, 12);
                m.Box(icon, "Stem", new(.013f, .12f, .029f), new(.045f, .06f, .22f), Ivory);
                m.Box(icon, "Flag", new(.069f, .12f, .11f), new(.13f, .06f, .045f), Ivory);
            }
            else
            {
                for (int i = -1; i <= 1; i++)
                    m.Box(icon, "Wave line", new(i * .081f, .12f, 0),
                        new(.039f, .06f, i == 0 ? .23f : .13f), Ivory);
            }
        }

        private static MenuControl Back(MenuModel m, Transform deck, Vector3 position)
        {
            var control = Control(deck, MenuControl.Action.Back, position, new(1.2f, .57f, .69f), new(0, .06f, -.43f));
            Plinth(m, control.transform, .83f);
            var arrow = control.model;
            m.Box(arrow, "Arrow shaft", new(.047f, .245f, 0), new(.47f, .13f, .095f), Patina);
            m.Box(arrow, "Arrow upper arm", new(-.167f, .245f, .073f), new(.31f, .13f, .088f), Patina, new(0, -45f, 0));
            m.Box(arrow, "Arrow lower arm", new(-.167f, .245f, -.073f), new(.31f, .13f, .088f), Patina, new(0, 45f, 0));
            return control;
        }

        private static void Plinth(MenuModel m, Transform root, float width)
        {
            m.Box(root, "Control pedestal", new(0, .05f, 0), new(width, .13f, .56f), Sand);
            m.Box(root, "Ivory rim", new(0, .139f, 0), new(width * .9f, .063f, .51f), Ivory);
        }

        private static MenuControl Control(Transform deck, MenuControl.Action action, Vector3 position,
            Vector3 size, Vector3 label)
        {
            Transform root = MenuModel.Child(deck, action.ToString(), position);
            var control = root.gameObject.AddComponent<MenuControl>();
            control.action = action;
            control.hitbox = MenuModel.Hitbox(root, new(0, size.y * .33f, 0), size);
            control.model = MenuModel.Child(root, "Control sculpture", Vector3.zero);
            control.label = MenuModel.Child(root, "Caption anchor", label);
            control.corners = System.Array.Empty<Transform>();
            return control;
        }
    }
}
