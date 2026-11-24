using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static BounceBack.GeometryWorld.Editor.SantoriniAuthoring;

namespace BounceBack.GeometryWorld.Editor
{
    /// <summary>Monument Valley dressing for the Campaign Boot menu. Rebuilding replaces the previous dressing.</summary>
    internal static class MenuCourtyardDressing
    {
        private const string ScenePath = "Assets/Scenes/Archive/BounceBackCampaign/Campaign Boot.unity";
        private const string Art = "Assets/Scenes/Archive/BounceBack/Art/";
        private const string Palette = Art + "I-ArtIntegration/Materials/";
        private const string ArtRoot = "Menu Courtyard Dressing";
        private const string Tag = "Dressed ";

        [MenuItem("Tools/Bounce Back/Dress Main Menu Courtyard")]
        public static void Build()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
                throw new InvalidOperationException("Save the Campaign Boot scene before dressing its courtyard.");
            var menu = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<GeometricMenu>()).First(item => item);
            Dress(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the Campaign Boot courtyard.");
        }

        internal static void Dress(GeometricMenu menu)
        {
            var stage = menu.transform.Find("Menu Architecture");
            if (!stage)
                return;
            Clear(menu, stage);
            var art = Child(stage, ArtRoot, Vector3.zero).transform;
            var ivory = Paint("MV_Ivory");
            var shade = Paint("MV_IvoryShade");
            var blue = Paint("MV_DomeBlue");
            var apricot = Paint("MV_Apricot");
            var rose = Paint("MV_Rose");

            var camera = menu.view;
            Vector3 offset = new(8f, 10.2f, -16f);
            Vector3 center = new(0f, -1.1f, 0.12f);
            camera.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(-offset));
            camera.orthographicSize = 8.9f;
            camera.backgroundColor = new Color(0.63f, 0.79f, 0.90f);
            camera.farClipPlane = 1000f;
            camera.allowHDR = true;
            Place(menu.transform, "Background/Santorini Backdrop.prefab", Vector3.zero, Vector3.zero, Vector3.one, Tag + "Backdrop");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.84f, 0.94f);
            RenderSettings.ambientEquatorColor = new Color(0.93f, 0.83f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.60f, 0.52f, 0.66f);

            // Island and terraces. The plaza top is y -0.125; the rear terrace top is y 0.03.
            Place(art, "A-FloatingIsland/Prefabs/A_FloatingIsland.prefab", new Vector3(0f, -0.23f, 0f),
                Vector3.zero, new Vector3(0.98f, 0.62f, 0.92f));
            stage.Find("Plaza").GetComponent<Renderer>().sharedMaterial = ivory;
            Stone(art, "Rear Courtyard Terrace", new Vector3(0f, -0.20f, 2.58f), new Vector3(12.1f, 0.46f, 3.92f), ivory);
            Stone(art, "Blue Terrace Frieze", new Vector3(0f, -0.16f, 0.60f), new Vector3(11.9f, 0.10f, 0.10f), blue);
            Stone(art, "Apricot Plaza Cornice", new Vector3(0f, -0.14f, -4.38f), new Vector3(12.5f, 0.08f, 0.12f), apricot);
            for (int i = 0; i < 3; i++)
                Stone(art, "Courtyard Approach Step", new Vector3(0f, -0.19f - i * 0.035f, 0.12f - i * 0.3f),
                    new Vector3(4.7f - i * 0.24f, 0.28f, 0.34f), shade);
            Place(art, "F-GoalTerrace/Prefabs/FGoalTerrace_Waterfall.prefab", new Vector3(5.55f, -0.12f, -2.6f),
                Vector3.zero, Vector3.one * 1.15f);

            // District towers. Locked districts show their wireframe at runtime, so these appear as progress grows.
            DressTower(menu.city[0], "B-ReleaseTower/Prefabs/BReleaseTower.prefab", new Vector3(0.63f, 0.49f, 0.63f));
            if (menu.city.Length > 1)
            {
                var second = DressTower(menu.city[1], "K-01/Prefabs/K01_Cupola_Tower.prefab", Vector3.one * 0.48f);
                if (second)
                    Place(menu.city[1].solid, "K-01/Prefabs/K01_Blue_Cupola.prefab", second.position + Vector3.up * 2.50f,
                        Vector3.zero, Vector3.one * 0.48f);
            }
            if (menu.city.Length > 2)
            {
                var pavilion = DressTower(menu.city[2], "F-GoalTerrace/Prefabs/FGoalTerrace_Pavilion.prefab", Vector3.one * 1.05f);
                if (pavilion)
                {
                    pavilion.position += Vector3.up * 1.1f;
                    Stone(menu.city[2].solid, Tag + "Pavilion Plinth", menu.city[2].transform.position + Vector3.up * 0.55f,
                        new Vector3(2.2f, 1.1f, 1.5f), shade);
                }
            }
            if (menu.city.Length > 3)
            {
                var fourth = DressTower(menu.city[3], "K-01/Prefabs/K01_Cupola_Tower.prefab", Vector3.one * 0.6f);
                if (fourth)
                    Place(menu.city[3].solid, "K-01/Prefabs/K01_Blue_Cupola.prefab", fourth.position + Vector3.up * 3.12f,
                        Vector3.zero, Vector3.one * 0.6f);
            }

            // Props and life.
            for (int side = -1; side <= 1; side += 2)
            {
                Place(art, "H-Props/Prefabs/H_Bougainvillea_Square.prefab", new Vector3(side * 5.31f, -0.12f, -0.72f),
                    Vector3.zero, Vector3.one * 0.88f);
                Place(art, "K-08/Prefabs/K08_Aegean_Arch_Window.prefab", new Vector3(side * 4.87f, 0.03f, 1.18f),
                    new Vector3(0f, 180f, 0f), new Vector3(0.86f, 0.92f, 1f));
                Place(art, "H-Props/Prefabs/H_Bougainvillea_Round.prefab", new Vector3(side * 5.65f, 0.03f, 4.15f),
                    Vector3.zero, Vector3.one);
                Place(art, "H-Props/Prefabs/H_Terracotta_Amphora.prefab", new Vector3(side * 5.75f, -0.13f, -3.95f),
                    new Vector3(0f, side * 25f, 0f), Vector3.one * 1.1f);
            }
            Place(art, "H-Props/Prefabs/H_Terracotta_Bowl.prefab", new Vector3(-5.15f, -0.13f, -4.05f), Vector3.zero, Vector3.one);
            var gulls = Child(menu.transform, Tag + "Seagulls", Vector3.zero).transform;
            Place(gulls, "H-Props/Prefabs/H_Seagull_Glide.prefab", new Vector3(-2.6f, 6.2f, 1.2f), new Vector3(0f, 90f, 0f), Vector3.one * 1.2f);
            Place(gulls, "H-Props/Prefabs/H_Seagull_Bank.prefab", new Vector3(3.4f, 7.0f, 2.4f), new Vector3(0f, 200f, 0f), Vector3.one * 1.1f);
            Place(gulls, "H-Props/Prefabs/H_Seagull_Upstroke.prefab", new Vector3(7.2f, 5.1f, -0.6f), new Vector3(0f, 300f, 0f), Vector3.one);

            // Glazed pedestals under the four menu devices.
            foreach (var device in menu.devices)
            {
                Vector3 point = device.transform.position;
                Stone(device.transform, Tag + "Pedestal Foot", point + Vector3.down * 0.73f, new Vector3(2.03f, 0.16f, 1.93f), shade);
                var ring = Visual(PrimitiveType.Cylinder, device.transform, Tag + "Brass Pedestal Rim",
                    point - Vector3.up * 0.48f, new Vector3(1.74f, 0.035f, 1.74f), apricot);
                ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Stone(device.transform, Tag + "Rose Pedestal Band", point + Vector3.down * 0.62f, new Vector3(1.9f, 0.06f, 1.8f), rose);
            }
        }

        private static void Clear(GeometricMenu menu, Transform stage)
        {
            var old = stage.Find(ArtRoot);
            if (old)
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (Transform root in new[] { menu.transform }.Concat(menu.city.Select(city => city.solid))
                         .Concat(menu.devices.Select(device => device.transform)))
            {
                if (!root)
                    continue;
                foreach (var child in root.Cast<Transform>().Where(child => child.name.StartsWith(Tag)).ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            foreach (var city in menu.city)
                if (city && city.solid)
                    foreach (var renderer in city.solid.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = true;
        }

        private static Transform DressTower(WorldEntrance district, string asset, Vector3 scale)
        {
            var tower = Place(district.solid, asset, district.transform.position + Vector3.up * 0.03f, Vector3.zero, scale, Tag + "Tower");
            if (tower)
                foreach (var renderer in district.solid.GetComponentsInChildren<Renderer>(true))
                    if (!renderer.transform.IsChildOf(tower))
                        renderer.enabled = false;
            return tower;
        }

        private static Material Paint(string name) => AssetDatabase.LoadAssetAtPath<Material>(Palette + name + ".mat");

        private static Transform Stone(Transform parent, string name, Vector3 center, Vector3 size, Material surface) =>
            Visual(PrimitiveType.Cube, parent, name, center, size, surface).transform;

        private static Transform Place(Transform parent, string path, Vector3 point, Vector3 angles, Vector3 scale,
            string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + path);
            if (!prefab)
                return null;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (name != null)
                model.name = name;
            model.transform.SetPositionAndRotation(point, Quaternion.Euler(angles));
            model.transform.localScale = Vector3.Scale(model.transform.localScale, scale);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            return model.transform;
        }
    }
}
