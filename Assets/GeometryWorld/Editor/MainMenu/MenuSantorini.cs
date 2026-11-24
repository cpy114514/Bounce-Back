using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MenuSantorini
    {
        internal static void Build(MenuModel m)
        {
            m.UseModel("Assets/_Project/MainMenu/Models/Santorini.fbx");
            Detail(m, "Church terrace and processional stair", V(-.43f, 0, 0));
            Detail(m, "Campanile footing", V(1.36f, 0, .77f));

            Piece(m, "Nave west wall", V(-1.45f, .84f, .24f), V(-1.405f, .73f, .42f), V(9, -15, -20), .12f);
            Piece(m, "Apse wall", V(-.43f, 1.00f, 1.23f), V(-.71f, .78f, 1.07f), V(-36, -12, 8), .12f);
            Piece(m, "Sunward wall foundation", V(.59f, .57f, .25f), V(.65f, .57f, .25f), V(0, 3, 3), .08f);
            Piece(m, "Lower fallen window bay", V(.59f, 1.31f, -.19f), V(1.11f, .45f, -.83f), V(11, -24, -68), .29f, .43f);
            Piece(m, "Standing window bay", V(.59f, 1.31f, .68f), V(.69f, 1.16f, .79f), V(-2, 12, 11), .39f, .43f);

            Piece(m, "Portal left pier", V(-1.11f, .99f, -.68f), V(-1.13f, .94f, -.70f), V(0, -2, 2), .16f);
            Piece(m, "Portal right pier", V(.25f, .99f, -.68f), V(.36f, .81f, -.74f), V(2, 7, -5), .23f);
            Piece(m, "Portal left tumbled masonry", V(-1.11f, 1.31f, -.69f), V(-1.62f, .32f, .39f), V(40, -21, 77), .31f, .46f);
            Piece(m, "Portal right tumbled masonry", V(.25f, 1.33f, -.69f), V(.58f, .30f, -1.29f), V(-66, 23, -32), .34f, .52f);
            Piece(m, "Blue cedar door", V(-.43f, .27f, -.665f), V(-.44f, .30f, -.82f), V(-70, 15, -10), .32f);
            Piece(m, "Limestone portal jambs", V(-.43f, .73f, -.825f), V(-.82f, .56f, -.99f), V(15, 9, 29), .30f);
            Piece(m, "Portal arch left voussoirs", V(-.43f, 1.145f, -.827f), V(-.46f, 1.12f, -.849f), V(-2, 3, 4), .43f);
            Piece(m, "Portal arch right voussoirs", V(-.43f, 1.145f, -.827f), V(.41f, .38f, -1.33f), V(-57, -21, 35), .48f);
            Piece(m, "Nave crown and rose medallion", V(-.43f, 1.68f, -.67f), V(-.82f, .44f, .76f), V(-69, 10, 8), .44f);
            Piece(m, "Nave roof slab", V(-.43f, 1.87f, .24f), V(-1.06f, .46f, .72f), V(19, 10, -16), .45f, .55f);
            Piece(m, "Nave roof east fracture", V(.32f, 1.87f, .19f), V(.85f, .47f, .35f), V(48, -19, -17), .50f, .61f);
            Piece(m, "Nave roof fallen rear corner", V(-.26f, 1.87f, 1.02f), V(-1.06f, .41f, 1.17f), V(-28, 15, 13), .46f, .57f);

            Piece(m, "Circular plaster drum", V(-.43f, 2.14f, .27f), V(-.71f, .49f, 1.20f), V(38, 11, -25), .49f, .85f);
            Vector3 center = V(-.43f, 2.325f, .27f);
            Vector3[] fallen = { V(1.02f, .36f, .62f), V(.14f, .25f, 1.36f), V(-1.74f, .42f, .96f), V(-1.69f, .34f, -1.22f), V(-.51f, .33f, -1.35f), V(.68f, .36f, -.98f) };
            Vector3[] angles = { V(63, -12, 37), V(-38, 18, 63), V(49, -28, -55), V(-59, -21, 39), V(73, 30, 15), V(48, -23, -41) };
            for (int i = 0; i < fallen.Length; i++)
            {
                float azimuth = (i * 60 + 30) * Mathf.Deg2Rad;
                var pivot = center + V(Mathf.Cos(azimuth) * .43f, .28f, Mathf.Sin(azimuth) * .43f);
                Piece(m, $"Blue dome masonry sector {i + 1}", pivot, fallen[i], angles[i], .51f + .038f * i, .57f + i * .045f);
            }
            Piece(m, "Dome finial", center + V(0, .865f, 0), V(.76f, .26f, -1.22f), V(21, 25, 71), .77f, .86f);

            Piece(m, "Standing campanile shaft", V(1.36f, .945f, .77f), V(1.42f, .86f, .81f), V(-8, 12, -9), .12f);
            Piece(m, "Bell arcade front", V(1.36f, 1.65f, .46f), V(1.84f, .43f, .12f), V(-43, -27, -62), .40f, .66f);
            Piece(m, "Bell arcade rear", V(1.36f, 1.65f, 1.08f), V(1.65f, .50f, 1.12f), V(-15, 25, 46), .46f, .68f);
            Piece(m, "Campanile cap and bell", V(1.36f, 2.60f, .77f), V(1.76f, .54f, -.43f), V(43, 17, 52), .64f, .91f);
        }

        private static void Detail(MenuModel m, string name, Vector3 position) => m.Baked(m.Detail(name, position), name);

        private static void Piece(MenuModel m, string name, Vector3 pivot, Vector3 ruin, Vector3 rotation, float delay,
            float arc = .6f) => m.Baked(m.Piece(name, pivot, ruin, rotation, delay, arc), name);

        private static Vector3 V(float x, float y, float z) => new(x, y, z);
    }
}
