using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MenuPavilion
    {
        internal static void Build(MenuModel model)
        {
            model.UseModel("Assets/_Project/MainMenu/Models/Pavilion.fbx");
            const string dais = "Stone pavilion dais";
            model.Baked(model.Detail(dais, Vector3.zero), dais);

            Piece(model, "Front west column", new Vector3(-1.56f, .28f, -1.02f),
                new Vector3(-1.40f, .49f, -.91f), new Vector3(-77f, -24f, -8f), .02f, .37f);
            Piece(model, "Front east column", new Vector3(1.56f, .28f, -1.02f),
                new Vector3(.76f, .49f, -1.14f), new Vector3(-8f, 12f, -85f), .07f, .37f);
            Piece(model, "West side column", new Vector3(-1.56f, .28f, .03f),
                new Vector3(-1.56f, .28f, .03f), new Vector3(0f, 1f, -1f), .01f, .37f);
            Piece(model, "East side column", new Vector3(1.56f, .28f, .03f),
                new Vector3(1.51f, .46f, -.30f), new Vector3(-80f, 7f, -8f), .09f, .37f);
            // The rear columns, frieze, and rear eave survive as a bay in the ruined pose.
            Piece(model, "Rear west column", new Vector3(-1.56f, .28f, 1.08f),
                new Vector3(-1.56f, .28f, 1.08f), new Vector3(0f, 0f, 0f), 0f, .37f);
            Piece(model, "Rear east column", new Vector3(1.56f, .28f, 1.08f),
                new Vector3(1.56f, .28f, 1.08f), new Vector3(0f, 0f, 0f), .04f, .37f);

            Piece(model, "West tie-beam", new Vector3(-1.55f, 2.04f, .03f),
                new Vector3(-1.55f, 2.01f, .43f), new Vector3(0f, 0f, -.7f), .16f, .48f);
            Piece(model, "East tie-beam", new Vector3(1.55f, 2.04f, .03f),
                new Vector3(1.68f, .54f, .35f), new Vector3(2f, -11f, 8f), .14f, .48f);
            Piece(model, "Rear frieze", new Vector3(0f, 1.953f, 1.08f),
                new Vector3(0f, 1.953f, 1.08f), new Vector3(0f, 0f, 0f), .17f, .48f);
            Piece(model, "Entrance frieze", new Vector3(0f, 1.953f, -1.02f),
                new Vector3(-.36f, .63f, -1.02f), new Vector3(-4f, -20f, 2f), .19f, .48f);

            // The settings reveal only parts the two halves over the entrance.
            Piece(model, "Lower front west eave", new Vector3(-.9393f, 2.241858f, -.9709f),
                new Vector3(-.93f, .63f, .21f), new Vector3(-5f, -13f, 7f), .28f, .57f,
                new Vector3(-.17f, .04f, -.03f), new Vector3(0f, -2f, -4f));
            Piece(model, "Lower front east eave", new Vector3(.9393f, 2.241858f, -.9709f),
                new Vector3(1.26f, .84f, -.49f), new Vector3(-13f, -68f, 12f), .31f, .57f,
                new Vector3(.17f, .04f, -.03f), new Vector3(0f, 2f, 4f));
            Piece(model, "Lower east hip eave", new Vector3(1.8786f, 2.232175f, 0f),
                new Vector3(1.48f, .57f, .36f), new Vector3(2f, -12f, -8f), .34f, .57f);
            Piece(model, "Lower rear eave", new Vector3(0f, 2.232175f, .9709f),
                new Vector3(0f, 2.222f, .971f), new Vector3(0f, .6f, 0f), .25f, .57f);
            Piece(model, "Lower west hip eave", new Vector3(-1.8786f, 2.232175f, 0f),
                new Vector3(-1.48f, .68f, .30f), new Vector3(-9f, -12f, -5f), .29f, .57f);

            Piece(model, "Open clerestory and latticework", new Vector3(0f, 2.78f, 0f),
                new Vector3(.39f, .49f, .24f), new Vector3(19f, -16f, 7f), .38f, .64f);
            Piece(model, "Upper front west eave", new Vector3(-.6051f, 3.178522f, -.6857f),
                new Vector3(.63f, 1.16f, -.30f), new Vector3(-9f, -51f, 4f), .48f, .57f);
            Piece(model, "Upper front east eave", new Vector3(.6051f, 3.178522f, -.6857f),
                new Vector3(1.15f, 1.12f, .56f), new Vector3(-7f, -42f, 17f), .51f, .57f);
            Piece(model, "Upper east hip eave", new Vector3(1.2102f, 3.168839f, 0f),
                new Vector3(1.48f, .67f, -.64f), new Vector3(7f, -22f, -4f), .47f, .57f);
            Piece(model, "Upper rear eave", new Vector3(0f, 3.168839f, .6857f),
                new Vector3(.57f, .56f, -.90f), new Vector3(-12f, -32f, 3f), .44f, .57f);
            Piece(model, "Upper west hip eave", new Vector3(-1.2102f, 3.168839f, 0f),
                new Vector3(1.24f, 1.07f, .26f), new Vector3(-11f, -27f, 6f), .49f, .57f);
            Piece(model, "Ridge crown with turned finials", new Vector3(0f, 3.69f, 0f),
                new Vector3(-.43f, .48f, -.83f), new Vector3(63f, 25f, -4f), .59f, .73f);
        }

        private static void Piece(MenuModel model, string name, Vector3 assembled, Vector3 ruin, Vector3 ruinEuler,
            float delay, float arc, Vector3 openingOffset = default, Vector3 openingEuler = default)
        {
            model.Baked(model.Piece(name, assembled, ruin, ruinEuler, delay, arc, openingOffset, openingEuler), name);
        }
    }
}
