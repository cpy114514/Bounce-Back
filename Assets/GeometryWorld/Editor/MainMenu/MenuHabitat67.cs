using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MenuHabitat67
    {
        internal static void Build(MenuModel model)
        {
            model.UseModel(MainMenuAuthoring.AssetRoot + "/Models/Habitat 67.fbx");
            Place("Courtyard podium", new(0, 0, 0), Vector3.zero, 0f, 0f);
            Place("West court residence", new(-1.28f, .26f, -.44f), Vector3.zero, .02f, 0f, -.40f);
            Place("East court residence", new(1.21f, .26f, -.38f), Vector3.zero, .04f, 0f, .40f);
            Place("Rear service core", new(.25f, .26f, .98f), Vector3.zero, .06f, 0f);
            Place("West stepped residence", new(-1.48f, .39f, .96f), new(-8, -14, 13), .12f, .44f);
            Place("East cantilever residence", new(1.40f, 1.08f, .07f), new(-7, 13, -9), .18f, .41f);
            Place("Courtyard gallery", new(-.04f, .29f, -1.30f), new(9, 24, -8), .22f, .56f);
            Place("West upper residence", new(-.17f, .38f, -.43f), new(-6, 18, 10), .31f, .69f);
            Place("East lookout", new(1.56f, .34f, 1.18f), new(10, -16, 8), .28f, .57f);
            Place("Roof terrace residence", new(-1.35f, 1.20f, -.41f), new(19, 21, -13), .46f, .73f);

            void Place(string name, Vector3 pose, Vector3 tilt, float delay, float arc, float slide = 0f)
            {
                // The imported groups have Blender's upright axis rotation and face away from the menu camera.
                var upright = Quaternion.Euler(-90f, 0f, 0f);
                var rotation = Quaternion.Euler(tilt) * upright;
                var pivot = new Vector3(-pose.x, pose.y, -pose.z);
                model.ImportedPiece(name, pivot, rotation.eulerAngles, delay, arc, new Vector3(-slide, 0f, 0f));
            }
        }
    }
}
