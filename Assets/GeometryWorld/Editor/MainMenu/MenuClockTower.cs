using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    /// <summary>Assembles the Blender clock tower sections and defines their fallen poses.</summary>
    internal static class MenuClockTower
    {
        private static readonly Vector3[] FallenDials =
        {
            new(1.29f, .315f, -1.47f), new(1.87f, .43f, 1.24f), new(-.90f, .42f, 1.54f), new(-1.72f, .49f, .68f)
        };
        private static readonly Vector3[] FallenDialAngles =
        {
            new(60, -8, 3), new(-6, 1, -43), new(-72, -15, 11), new(3, 21, 73)
        };

        internal static void Build(MenuModel model)
        {
            model.UseModel("Assets/_Project/MainMenu/Models/Clock Tower.fbx");
            const string foundation = "Clock court and stepped foundation";
            model.Baked(model.Detail(foundation, Vector3.zero), foundation);

            Shaft(model, "Ground storey", .29f, .64f, new(-.17f, .61f, .03f), new(0, -5, -2), 0);
            Shaft(model, "Lower ashlar block", .93f, .57f, new(-.18f, 1.17f, .05f), new(1, -6, -4), .035f);
            Piece(model, "Standing lower pilaster", new(.585f, 1.245f, -.585f), new(.21f, 1.298f, -.52f),
                new(1, -11, -8), .076f, .43f);
            Shaft(model, "Middle ashlar block", 1.50f, .58f, new(.78f, .40f, 1.34f), new(17, 39, -14), .09f);
            Shaft(model, "Upper ashlar block", 2.08f, .48f, new(-1.32f, .44f, .92f), new(-36, 22, 18), .14f);

            Piece(model, "Clock balcony stone ring", new(0, 2.65f, 0), new(-.18f, .185f, .18f),
                new(5, 15, -9), .20f, .74f);
            Piece(model, "Clock chamber ashlar core", new(0, 3.135f, 0), new(-.20f, .526f, 1.00f),
                new(14, 17, 12), .23f, .83f);
            ClockFace(model, 0, new(1.31f, .32f, 1.46f), new(-55, -39, 22), .32f);
            ClockFace(model, 1, new(1.53f, .44f, 1.06f), new(4, 35, -72), .37f);
            ClockFace(model, 2, new(-.40f, .27f, 1.68f), new(-68, 10, 11), .39f);
            ClockFace(model, 3, new(-1.90f, .285f, .78f), new(3, 32, 77), .35f);

            Piece(model, "Pierced crown and belfry", new(0, 3.81f, 0), new(.99f, .56f, 1.42f),
                new(81, 17, -12), .41f, .93f);
            Piece(model, "Lead roof slope 1", new(0, 4.067f, 0), new(1.57f, .235f, -.22f),
                new(8, -21, 17), .44f, .98f);
            Piece(model, "Lead roof slope 2", new(0, 4.067f, 0), new(1.51f, .31f, .72f),
                new(6, 36, -17), .457f, .98f);
            Piece(model, "Lead roof slope 3", new(0, 4.067f, 0), new(-.79f, .36f, 1.30f),
                new(25, -7, 3), .474f, .98f);
            Piece(model, "Lead roof slope 4", new(0, 4.067f, 0), new(-1.49f, .265f, .49f),
                new(11, 24, 17), .491f, .98f);
            Piece(model, "Lantern, upper spire and gilded finial", new(0, 4.772f, 0),
                new(.39f, .30f, 1.33f), new(74, 8, 61), .54f, 1.07f);
            Piece(model, "Gothic pinnacles and cresting", new(0, 4.035f, 0),
                new(-.72f, .24f, .92f), new(3, 16, 7), .48f, .92f);
        }

        private static void Shaft(MenuModel model, string name, float bottom, float height, Vector3 ruin,
            Vector3 angles, float delay)
        {
            Piece(model, name, new(0, bottom + height * .5f, 0), ruin, angles, delay, .36f + delay * 2);
        }

        private static void ClockFace(MenuModel model, int side, Vector3 ruin, Vector3 angles, float delay)
        {
            Quaternion turn = Quaternion.Euler(0, side * 90, 0);
            Vector3 center = new Vector3(0, 3.12f, 0) + turn * new Vector3(0, 0, -.63f);
            Piece(model, $"Fallen sandstone surround {side + 1}", center, ruin, angles, delay, .88f);
            string dial = $"Detached clockwork dial {side + 1}";
            string hour = $"Clock {side + 1} Hour Hand";
            string minute = $"Clock {side + 1} Minute Hand";
            Transform clockwork = model.Piece(dial, center, FallenDials[side],
                FallenDialAngles[side], delay + .055f, .93f);
            model.Baked(clockwork, dial, hour, minute);
            ClockHand(model, clockwork, turn, hour, -54, -.225f);
            ClockHand(model, clockwork, turn, minute, 56, -.242f);
        }

        private static void ClockHand(MenuModel model, Transform clockwork, Quaternion turn, string name,
            float angle, float depth)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(clockwork, false);
            pivot.localPosition = turn * new Vector3(0, -.018f, depth);
            pivot.localRotation = turn * Quaternion.Euler(0, 0, angle);
            model.Baked(pivot, name);
            model.ClockHand(pivot, Vector3.forward);
        }

        private static Transform Piece(MenuModel model, string name, Vector3 assembled, Vector3 ruin,
            Vector3 angles, float delay, float arc)
        {
            Transform piece = model.Piece(name, assembled, ruin, angles, delay, arc);
            model.Baked(piece, name);
            return piece;
        }
    }
}
