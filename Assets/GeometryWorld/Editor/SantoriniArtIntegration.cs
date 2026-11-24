using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    public static class SantoriniArtIntegration
    {
        const string ArtRoot = "Assets/Scenes/Archive/BounceBack/Art";
        const string Folder = ArtRoot + "/I-ArtIntegration";
        const float Tolerance = 0.02f;
        const string CaptureFlag = "BounceBack.IArtCapture";

        static readonly StringBuilder Report = new StringBuilder();
        static readonly List<GameObject> Accepted = new List<GameObject>();
        static MaterialSet paint;
        static bool poolArtPlaced;
        static bool waterfallArtPlaced;

        internal static void Apply(GeometryLevel level)
        {
            Report.Clear();
            Accepted.Clear();
            poolArtPlaced = waterfallArtPlaced = false;
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Preview");
            paint = MaterialSet.Create();
            Transform art = level.transform.Find("Courtyard Art");
            Transform tower = art.Find("Tower Art");
            Transform stairs = art.Find("Staircase Art");
            Transform courtyard = art.Find("Courtyard and Water Art");
            Transform terrace = art.Find("Goal Terrace Art");

            PaintBlockout(level);
            PlaceTower(tower);
            PlaceAqueduct(stairs, level);
            PlaceIsland(courtyard);
            PlacePoolStone(courtyard);
            PlaceGate(courtyard, level);
            PlaceTerrace(terrace, level);
            PlaceWater(courtyard, terrace, level);
            PlaceProps(level, tower, terrace, courtyard);
            EnforcePalette(level);
            LightAndVolume(level);
            PlaceBackdrop(level);
            PlaceMist(courtyard);
            Note("Triangles accepted " + Accepted.Sum(Triangles));
            Debug.Log("I_ART_REPORT_BEGIN\n" + Report + "I_ART_REPORT_END");
        }

        static void PlaceTower(Transform tower)
        {
            const string combined = ArtRoot + "/B-ReleaseTower/Prefabs/BReleaseTower.prefab";
            GameObject placed = Place(combined, tower, new Vector3(-5.64f, -0.22f, -3.65f), Quaternion.identity, "B-ReleaseTower");
            if (!placed)
            {
                var group = new GameObject("B-ReleaseTower");
                group.transform.SetParent(tower, false);
                group.transform.position = new Vector3(-5.64f, -0.22f, -3.65f);
                group.transform.rotation = Quaternion.identity;
                bool any = false;
                foreach (string part in new[] { "BReleaseTower_Body", "BReleaseTower_Platform", "BReleaseTower_BellTower" })
                {
                    var piece = Place(ArtRoot + "/B-ReleaseTower/Prefabs/" + part + ".prefab", group.transform, group.transform.position,
                        Quaternion.identity, part);
                    any |= piece;
                }
                placed = any ? group : null;
                if (!any)
                    UnityEngine.Object.DestroyImmediate(group);
            }
            var bounds = placed ? WorldBounds(placed) : default;
            bool covers = placed && bounds.Contains(new Vector3(-5.64f, 2.27f, -3.65f)) && bounds.max.y >= 4.40f;
            if (!covers)
            {
                if (placed)
                    UnityEngine.Object.DestroyImmediate(placed);
                Note("B-ReleaseTower kept whitebox. bounds=" + (placed ? bounds.ToString() : "missing"));
                return;
            }
            Accept(placed);
            HideNamed(tower, "White Tower", "Launch Coping Visual", "Tower Foot", "Glazed Cornice", "Start Rail");
            foreach (var renderer in tower.GetComponentsInChildren<Renderer>(true))
                if (renderer && renderer.transform.GetComponentInParent<Transform>() && AncestorNamed(renderer.transform, "Recessed Arch")
                    && !AncestorNamed(renderer.transform, "B-ReleaseTower"))
                    renderer.enabled = false;
            Note($"B-ReleaseTower placed. triangles={Triangles(placed)} bounds={bounds}");
        }

        static void PlaceAqueduct(Transform stairs, GeometryLevel level)
        {
            string aqueductPrefab = ArtRoot + "/C-AqueductWalk/Prefabs/C-AqueductWalk.prefab";
            string aqueductFbx = ArtRoot + "/C-AqueductWalk/FBX/C-AqueductWalk.fbx";
            var placed = Place(AssetDatabase.LoadAssetAtPath<GameObject>(aqueductPrefab) ? aqueductPrefab : aqueductFbx, stairs,
                new Vector3(-3.20f, 4.27f, -2.10f), Quaternion.identity, "C-AqueductWalk");
            if (!placed)
            {
                Note("C-AqueductWalk missing. Stair whitebox kept.");
                return;
            }
            var deck = Filters(placed, "Deck");
            var parapet = Filters(placed, "Parapet");
            var arcade = Filters(placed, "Arcade");
            var stairsMesh = Filters(placed, "Donkey");
            float worstTop = 0f, worstBottom = 0f, worstParapet = 0f;
            int ramps = 0, rampsPassed = 0, parapets = 0, parapetsPassed = 0;
            var rampNames = new List<string>();
            var parapetNames = new List<string>();
            foreach (var box in level.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.name is "Straight Stair Ramp" or "Pool Drop Ramp" || box.name.StartsWith("Turn Ramp "))
                {
                    float top = CornerError(Face(box, true), deck);
                    float bottom = CornerError(Face(box, false), deck);
                    float rise = MaxAbove(box, deck);
                    worstTop = Mathf.Max(worstTop, top);
                    worstBottom = Mathf.Max(worstBottom, bottom);
                    bool pass = top <= Tolerance;
                    ramps++;
                    if (pass)
                        rampsPassed++;
                    rampNames.Add(box.name + " Visual");
                    Note($"C {box.name} top={top:F5} bottom={bottom:F5} rise={rise:F5} {(pass ? "top ok" : "top fails")}");
                }
                else if (box.name.StartsWith("Turn Parapet "))
                {
                    float error = CornerError(Corners(box), parapet);
                    worstParapet = Mathf.Max(worstParapet, error);
                    parapets++;
                    if (error <= Tolerance)
                        parapetsPassed++;
                    parapetNames.Add(box.name + " Visual");
                    Note($"C {box.name} corner={error:F5} {(error <= Tolerance ? "ok" : "fails")}");
                }
            }
            var bounds = WorldBounds(placed);
            if (ramps == 0 || rampsPassed != ramps)
            {
                UnityEngine.Object.DestroyImmediate(placed);
                Note($"C summary top={worstTop:F5} bottom={worstBottom:F5} parapet={worstParapet:F5} ramps={rampsPassed}/{ramps} parapets={parapetsPassed}/{parapets} bounds={bounds}. Deck failed, module removed, whitebox kept. Bottom above {Tolerance:F2} is not a pass.");
                return;
            }
            HideNamed(level.transform, rampNames.ToArray());
            HideNamed(stairs, "Limestone Tread");
            if (parapets > 0 && parapetsPassed == parapets)
                HideNamed(level.transform, parapetNames.ToArray());
            else
                DisableFilters(parapet);
            int blocked = DisableIfAboveRoad(arcade, level) + DisableIfAboveRoad(stairsMesh, level);
            if (placed.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled))
                Accept(placed);
            else
                UnityEngine.Object.DestroyImmediate(placed);
            Note($"C summary top={worstTop:F5} bottom={worstBottom:F5} parapet={worstParapet:F5} ramps={rampsPassed}/{ramps} parapets={parapetsPassed}/{parapets} clearanceOff={blocked} bounds={bounds}. Bottom above {Tolerance:F2} is not a pass.");
        }

        static void PlaceIsland(Transform courtyard)
        {
            string islandPrefab = ArtRoot + "/A-FloatingIsland/Prefabs/A_FloatingIsland.prefab";
            string islandFbx = ArtRoot + "/A-FloatingIsland/FBX/A_FloatingIsland.fbx";
            var placed = Place(AssetDatabase.LoadAssetAtPath<GameObject>(islandPrefab) ? islandPrefab : islandFbx, courtyard,
                new Vector3(0f, -0.22f, 0.05f), Quaternion.identity, "A-FloatingIsland");
            if (!placed)
            {
                Note("A-FloatingIsland not delivered. Foundation whitebox kept.");
                return;
            }
            var bounds = WorldBounds(placed);
            var crown = FindDeep(courtyard, "Foundation Crown");
            float error = float.PositiveInfinity;
            if (crown)
            {
                Vector3 half = crown.localScale * 0.5f;
                Vector3 center = crown.position;
                var corners = new[]
                {
                    center + new Vector3(half.x, half.y, half.z),
                    center + new Vector3(half.x, half.y, -half.z),
                    center + new Vector3(-half.x, half.y, half.z),
                    center + new Vector3(-half.x, half.y, -half.z)
                };
                error = CornerError(corners, placed.GetComponentsInChildren<MeshFilter>(true));
            }
            bool mirrored = bounds.min.x < -7.2f;
            bool pass = error <= Tolerance && !mirrored;
            Note($"A-FloatingIsland top={error:F5} boundsMin={bounds.min} boundsMax={bounds.max} mirrored={mirrored} {(pass ? "placed" : "whitebox kept")}");
            if (!pass)
            {
                UnityEngine.Object.DestroyImmediate(placed);
                return;
            }
            Accept(placed);
            HideNamed(courtyard, "Floating Limestone Foundation", "Lower Limestone Course", "Foundation Crown", "Glazed Foundation Frieze");
        }

        static void PlacePoolStone(Transform courtyard)
        {
            var placed = Place(ArtRoot + "/D-ElasticPool/Prefabs/D-ElasticPool.prefab", courtyard,
                new Vector3(-1.4f, 0.45f, 2.05f), Quaternion.identity, "D-ElasticPool");
            if (!placed)
                placed = Place(ArtRoot + "/D-ElasticPool/FBX/D-ElasticPool.fbx", courtyard,
                    new Vector3(-1.4f, 0.45f, 2.05f), Quaternion.identity, "D-ElasticPool");
            if (!placed)
            {
                Note("D-ElasticPool not delivered. Pool stone whitebox kept.");
                return;
            }
            float error = CornerError(PoolWaterCorners(), placed.GetComponentsInChildren<MeshFilter>(true));
            Note($"D-ElasticPool water-top corner distance={error:F5} {(error <= Tolerance ? "rim placed" : "whitebox kept")}");
            if (error > Tolerance)
            {
                UnityEngine.Object.DestroyImmediate(placed);
                return;
            }
            Accept(placed);
            HideNamed(courtyard, "Pool Basin", "Long Pool Coping", "Short Pool Coping");
        }

        static void PlaceGate(Transform courtyard, GeometryLevel level)
        {
            var wall = FindDeep(level.transform, "Lift Wall");
            var frame = Place(ArtRoot + "/E-LiftGate/Prefabs/LiftGate Frame.prefab", courtyard,
                new Vector3(-1.55f, -0.22f, 3.95f), Quaternion.identity, "E-LiftGate Frame");
            GameObject shutter = null;
            var shutterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/E-LiftGate/Prefabs/LiftGate Shutter.prefab");
            if (wall && shutterAsset && courtyard)
            {
                // The lift wall is a kinematic body that keeps its world pose while the courtyard scales.
                // Parent the shutter to the folding courtyard and copy the wall pose until the wall locks.
                shutter = (GameObject)PrefabUtility.InstantiatePrefab(shutterAsset, courtyard);
                shutter.name = "E-LiftGate Shutter";
                shutter.transform.SetPositionAndRotation(wall.position, Quaternion.identity);
                shutter.transform.localScale = Vector3.one;
                StripPhysics(shutter);
                var followType = Type.GetType("BounceBack.ArtIntegration.LiftShutterFollow, Assembly-CSharp");
                if (followType != null && level.preparationWall)
                {
                    var follow = shutter.AddComponent(followType);
                    var serialized = new SerializedObject(follow);
                    serialized.FindProperty("wall").objectReferenceValue = level.preparationWall;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            bool frameOk = frame && WorldBounds(frame).size.y > 3f;
            bool shutterOk = false;
            if (shutter && wall && wall.TryGetComponent(out BoxCollider box))
            {
                var bounds = WorldBounds(shutter);
                Vector3 meshSize = LargestMeshSize(shutter);
                shutterOk = Vector3.Distance(bounds.center, wall.position) < 0.08f && MatchesAxes(meshSize, 2.60f, 1.25f, 0.29f);
                Note($"E shutter centerDelta={Vector3.Distance(bounds.center, wall.position):F4} mesh={meshSize} world={bounds.size}");
            }
            if (!frameOk && frame)
                UnityEngine.Object.DestroyImmediate(frame);
            if (!shutterOk && shutter)
                UnityEngine.Object.DestroyImmediate(shutter);
            if (frameOk)
            {
                Accept(frame);
                HideNamed(courtyard, "Limestone Lift Pier", "Bronze Runner", "Alignment Lintel");
            }
            if (shutterOk)
            {
                Accept(shutter);
                HideNamed(wall, "Glazed Stone Shutter", "Shutter Cornice", "Glazed Shutter Panel");
            }
            Note($"E-LiftGate frame={(frameOk ? "placed" : "whitebox")} shutter={(shutterOk ? "follows Lift Wall and folds with the courtyard" : "whitebox")}");
        }

        static void PlaceTerrace(Transform terrace, GeometryLevel level)
        {
            bool any = false;
            foreach (string name in new[] { "FGoalTerrace_Terrace", "FGoalTerrace_Cascade", "FGoalTerrace_Pavilion", "FGoalTerrace_Railing", "FGoalTerrace_Waterfall" })
            {
                string path = ArtRoot + "/F-GoalTerrace/FBX/" + name + ".fbx";
                var prefab = ArtRoot + "/F-GoalTerrace/Prefabs/" + name + ".prefab";
                var placed = Place(File.Exists(ToFull(prefab)) || AssetDatabase.LoadAssetAtPath<GameObject>(prefab) ? prefab : path,
                    terrace, terrace.position, terrace.rotation, name);
                if (!placed)
                    continue;
                if (name == "FGoalTerrace_Terrace")
                {
                    var landing = ColliderNamed(level.transform, "Landing Terrace");
                    if (landing)
                    {
                        float scale = landing.size.x / 3.4f;
                        Vector3 position = placed.transform.position;
                        position.x = landing.bounds.center.x + (terrace.position.x - 2.30f) * scale;
                        placed.transform.position = position;
                        placed.transform.localScale = new Vector3(scale, 1f, 1f);
                    }
                }
                if (name == "FGoalTerrace_Railing")
                {
                    var landing = ColliderNamed(level.transform, "Landing Terrace");
                    var bounds = WorldBounds(placed);
                    if (landing && bounds.size.x > 0.01f)
                    {
                        float scale = (landing.size.x - 0.10f) / bounds.size.x;
                        Vector3 position = placed.transform.position;
                        position.x = landing.bounds.center.x + (position.x - bounds.center.x) * scale;
                        placed.transform.position = position;
                        placed.transform.localScale = new Vector3(scale, 1f, 1f);
                    }
                }
                float error = ModuleError(level, name, placed);
                bool pass = error <= Tolerance || name is "FGoalTerrace_Pavilion" or "FGoalTerrace_Railing" or "FGoalTerrace_Waterfall";
                if (name is "FGoalTerrace_Pavilion" or "FGoalTerrace_Railing" or "FGoalTerrace_Waterfall")
                {
                    var bounds = WorldBounds(placed);
                    pass = bounds.size.magnitude > 0.2f && bounds.center.x > -2f && bounds.center.x < 12f;
                    Note($"F {name} decorative bounds={bounds} {(pass ? "placed" : "dropped")}");
                }
                else
                    Note($"F {name} collision error={error:F5} {(pass ? "placed" : "whitebox kept")}");
                if (!pass)
                {
                    UnityEngine.Object.DestroyImmediate(placed);
                    continue;
                }
                Accept(placed);
                any = true;
                if (name == "FGoalTerrace_Terrace")
                    HideNamed(level.transform, "Landing Terrace Visual");
                if (name == "FGoalTerrace_Cascade")
                {
                    HideNamed(level.transform, "Cascade Lip Ramp Visual");
                    foreach (var filter in placed.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.name.Contains("Floor_Collision") || filter.name.Contains("Cascade_Floor"))
                            filter.GetComponent<Renderer>().enabled = false;
                }
                if (name == "FGoalTerrace_Railing")
                    HideNamed(terrace, "Cascade Baluster", "Cascade Rail");
                if (name == "FGoalTerrace_Waterfall")
                    HideNamed(terrace, "Waterfall Cornice", "Fall Ribbon");
            }
            if (!any)
                Note("F-GoalTerrace not placed. Terrace whitebox kept.");
        }

        static float ModuleError(GeometryLevel level, string name, GameObject placed)
        {
            var filters = placed.GetComponentsInChildren<MeshFilter>(true);
            if (name == "FGoalTerrace_Terrace")
            {
                var box = ColliderNamed(level.transform, "Landing Terrace");
                return box ? CornerError(Corners(box), filters) : 99f;
            }
            if (name == "FGoalTerrace_Cascade")
            {
                var lip = ColliderNamed(level.transform, "Cascade Lip Ramp");
                var floor = ColliderNamed(level.transform, "Cascade Pressure Plate Floor");
                float lipError = lip ? CornerError(Corners(lip), filters) : 99f;
                float floorError = floor ? CornerError(Corners(floor), filters) : 99f;
                return Mathf.Max(lipError, floorError);
            }
            return 0f;
        }

        static void PlaceWater(Transform courtyard, Transform terrace, GeometryLevel level)
        {
            string poolPrefab = ArtRoot + "/G-StylizedWater/Prefabs/G-PoolWater.prefab";
            string fallPrefab = ArtRoot + "/G-StylizedWater/Prefabs/G-Waterfall.prefab";
            GameObject pool = null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(poolPrefab))
                pool = Place(poolPrefab, courtyard, Vector3.zero, Quaternion.identity, "G-PoolWater");
            else
                pool = ClonePart(ArtRoot + "/G-StylizedWater/FBX/G-StylizedWater.fbx", "G_PoolSurface", courtyard, Vector3.zero, "G-PoolWater");
            var poolBox = ColliderNamed(level.transform, "Elastic Pool Surface");
            float poolError = pool ? CornerError(PoolWaterCorners(), pool.GetComponentsInChildren<MeshFilter>(true)) : 99f;
            if (pool && poolError > Tolerance)
            {
                Note($"G pool rejected, top error={poolError:F5}");
                UnityEngine.Object.DestroyImmediate(pool);
                pool = null;
            }
            else if (pool)
            {
                Accept(pool);
                poolArtPlaced = true;
                AssignWaterMaterials(pool);
                EnsureRipple(pool);
                var spring = poolBox ? poolBox.GetComponent<ElasticGeometry>() : null;
                AddBehaviour(spring ? spring.gameObject : pool, "BounceBack.ArtIntegration.PoolRippleLink");
                HideNamed(courtyard, "Breathing Pool Surface");
                Note($"G pool placed. top error={poolError:F5} triangles={Triangles(pool)}");
            }

            var slot = FindDeep(terrace, "Waterfall Slot");
            GameObject falls = null;
            if (slot && AssetDatabase.LoadAssetAtPath<GameObject>(fallPrefab))
                falls = Place(fallPrefab, slot, slot.position, Quaternion.identity, "G-Waterfall");
            else if (slot)
            {
                falls = new GameObject("G-Waterfall");
                falls.transform.SetParent(slot, false);
                falls.transform.localPosition = Vector3.zero;
                falls.transform.localRotation = Quaternion.identity;
                bool ribbon = ClonePart(ArtRoot + "/G-StylizedWater/FBX/G-StylizedWater.fbx", "G_WaterfallRibbon", falls.transform, slot.position, "G_WaterfallRibbon");
                bool mist = ClonePart(ArtRoot + "/G-StylizedWater/FBX/G-StylizedWater.fbx", "G_WaterfallMist", falls.transform, slot.position, "G_WaterfallMist");
                if (!ribbon && !mist)
                {
                    UnityEngine.Object.DestroyImmediate(falls);
                    falls = null;
                }
            }
            if (falls)
            {
                var bounds = WorldBounds(falls);
                bool aligned = Mathf.Abs(bounds.max.y - 0.43f) < 0.08f && Mathf.Abs(bounds.center.z - 5.24f) < 0.4f;
                Note($"G waterfall bounds={bounds} {(aligned ? "placed" : "rejected")}");
                if (!aligned)
                    UnityEngine.Object.DestroyImmediate(falls);
                else
                {
                    Accept(falls);
                    waterfallArtPlaced = true;
                    AssignWaterMaterials(falls);
                    HideNamed(terrace, "Fall Ribbon");
                    foreach (var filter in terrace.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.name.Contains("Static_Water"))
                            filter.GetComponent<Renderer>().enabled = false;
                }
            }
            if (!poolArtPlaced)
            {
                var spring = poolBox ? poolBox.GetComponent<ElasticGeometry>() : null;
                if (spring && AddBehaviour(spring.gameObject, "BounceBack.ArtIntegration.SimplePoolRings"))
                    Note("G pool absent. Simple procedural rings attached to the elastic pool.");
                else
                    Note("G pool absent and ripple link unavailable. Breathing pool whitebox kept.");
            }
        }

        static void PlaceProps(GeometryLevel level, Transform tower, Transform terrace, Transform courtyard)
        {
            var props = new (string name, Transform parent, Vector3 position)[]
            {
                ("H_Blue_Shutters_Pair", tower, new Vector3(-6.05f, 1.55f, -4.58f)),
                ("H_Blue_Shutters_Pair", tower, new Vector3(-5.25f, 2.85f, -4.58f)),
                ("H_Copper_Bell", tower, new Vector3(-6.91f, 6.35f, -4.33f)),
                ("H_Bougainvillea_Square", terrace, new Vector3(2.88f, 0.92f, 6.82f)),
                ("H_Bougainvillea_Round", terrace, new Vector3(4.10f, 0.92f, 6.85f)),
                ("H_Terracotta_Amphora", terrace, new Vector3(3.76f, 0.92f, 6.84f)),
                ("H_Terracotta_Bowl", terrace, new Vector3(3.35f, 0.92f, 6.80f)),
                ("H_Domed_Chimney", courtyard, new Vector3(5.45f, -0.22f, -4.35f)),
                ("H_Roof_Cupola", courtyard, new Vector3(4.35f, -0.22f, -4.55f))
            };
            SantoriniAuthoring.Visual(PrimitiveType.Cube, terrace, "Planter Ledge", new Vector3(3.40f, 0.81f, 6.78f),
                new Vector3(1.65f, 0.22f, 0.60f), paint.IvoryShade);
            int placedCount = 0;
            var seen = new HashSet<string>();
            foreach (var prop in props)
            {
                string prefab = ArtRoot + "/H-Props/Prefabs/" + prop.name + ".prefab";
                string fbx = ArtRoot + "/H-Props/FBX/" + prop.name + ".fbx";
                string path = AssetDatabase.LoadAssetAtPath<GameObject>(prefab) ? prefab : fbx;
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                {
                    if (seen.Add(prop.name))
                        Note("H " + prop.name + " not delivered.");
                    continue;
                }
                var item = Place(path, prop.parent, prop.position, Quaternion.identity, prop.name);
                if (!item)
                    continue;
                if (BlocksCourse(item, level))
                {
                    Note($"H {prop.name} at {prop.position} intersects the ball corridor. White space kept.");
                    UnityEngine.Object.DestroyImmediate(item);
                    continue;
                }
                Accept(item);
                placedCount++;
            }
            PlaceSeagulls(tower);
            Note("H props placed " + placedCount + ". Seagulls orbit the tower with no colliders.");
        }

        static void PlaceSeagulls(Transform tower)
        {
            var type = Type.GetType("BounceBack.ArtIntegration.SeagullOrbit, Assembly-CSharp");
            if (type == null)
            {
                Note("SeagullOrbit script did not compile. No birds added.");
                return;
            }
            var hub = new GameObject("Seagulls").transform;
            hub.SetParent(tower, false);
            hub.position = new Vector3(-5.64f, 4.45f, -3.65f);
            hub.rotation = Quaternion.identity;
            var birds = new[]
            {
                ("H_Seagull_Glide", 3.1f, 2.55f, 12f, 0f),
                ("H_Seagull_Upstroke", 3.7f, 3.15f, 16f, 120f),
                ("H_Seagull_Bank", 2.7f, 1.95f, 9f, 240f)
            };
            foreach (var bird in birds)
            {
                string prefab = ArtRoot + "/H-Props/Prefabs/" + bird.Item1 + ".prefab";
                string fbx = ArtRoot + "/H-Props/FBX/" + bird.Item1 + ".fbx";
                string path = AssetDatabase.LoadAssetAtPath<GameObject>(prefab) ? prefab : fbx;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset)
                    continue;
                var rig = new GameObject(bird.Item1);
                rig.transform.SetParent(hub, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, rig.transform);
                StripPhysics(model);
                Remap(model);
                var orbit = rig.AddComponent(type);
                var serialized = new SerializedObject(orbit);
                serialized.FindProperty("radius").floatValue = bird.Item2;
                serialized.FindProperty("height").floatValue = bird.Item3;
                serialized.FindProperty("degreesPerSecond").floatValue = bird.Item4;
                serialized.FindProperty("phase").floatValue = bird.Item5;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Accept(rig);
            }
        }

        static void PaintBlockout(GeometryLevel level)
        {
            foreach (var renderer in level.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !IsBlockout(renderer.sharedMaterial))
                    continue;
                string name = renderer.gameObject.name;
                Material material = paint.Ivory;
                if (name == "Glazed Spring Pad")
                    material = paint.Mint;
                else if (IsMechanismMark(name))
                    material = paint.Gold;
                else if (name.Contains("Inset") || name.Contains("Arch"))
                    material = paint.Dome;
                else if (name.Contains("Water") || name.Contains("Fall") || name.Contains("Foam") || name.Contains("Breathing"))
                    material = paint.Water;
                else if (name.Contains("Bronze") || name.Contains("Cornice") || name.Contains("Frieze"))
                    material = paint.Apricot;
                else if (name.Contains("Foot") || name.Contains("Shadow") || name.Contains("Basin") || name.Contains("Stone"))
                    material = paint.Rose;
                else if (name.Contains("Lock") || name.Contains("Rail") || name.Contains("Pier") || name.Contains("Parapet"))
                    material = paint.IvoryShade;
                renderer.sharedMaterial = material;
            }
            if (level.StarterPiston)
                foreach (var renderer in level.StarterPiston.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = paint.Mint;
            var inlay = FindDeep(level.transform, "Gold Inlay");
            if (inlay)
                foreach (var renderer in inlay.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = paint.Gold;
            level.spawn.material = paint.Gold;
        }

        static void EnforcePalette(GeometryLevel level)
        {
            Transform piston = level.StarterPiston ? level.StarterPiston.transform : null;
            Transform wall = level.preparationWall ? level.preparationWall.transform : null;
            Transform goal = level.goal ? level.goal.transform : null;
            foreach (var renderer in level.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i])
                        continue;
                    bool mint = materials[i] == paint.Mint || materials[i].name == "MV_Mint";
                    bool gold = materials[i] == paint.Gold || materials[i].name == "MV_Gold";
                    if (mint && renderer.name != "Glazed Spring Pad" &&
                        !Under(renderer.transform, piston) && !Under(renderer.transform, wall))
                    {
                        materials[i] = paint.Ivory;
                        changed = true;
                    }
                    else if (gold && !Under(renderer.transform, goal) && !Under(renderer.transform, piston)
                        && !IsMechanismMark(renderer.name) && !AncestorNamed(renderer.transform, "FGoalTerrace_Pavilion"))
                    {
                        materials[i] = paint.Apricot;
                        changed = true;
                    }
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        static bool IsMechanismMark(string name) =>
            name is "Alignment Diamond" or "Spring Guide Alignment Mark" or "Lift Height Marker" or "Lift Guide Alignment Mark";

        static void LightAndVolume(GeometryLevel level)
        {
            var lights = level.GetComponentsInChildren<Light>(true).Where(item => item.type == LightType.Directional).ToArray();
            Light key = lights.FirstOrDefault();
            if (!key)
                key = new GameObject("Key Light").AddComponent<Light>();
            if (key.transform.parent != level.transform)
                key.transform.SetParent(level.transform, false);
            for (int i = 1; i < lights.Length; i++)
                lights[i].enabled = false;
            // Travel direction is 45° down from the camera's rear-left, so -Z is lit and +X falls off.
            key.transform.rotation = Quaternion.LookRotation(new Vector3(-0.15f, -0.71f, 0.69f).normalized, Vector3.up);
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.95f, 0.87f);
            key.intensity = 1.25f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.62f;
            key.enabled = true;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.96f, 0.78f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.98f, 0.84f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.28f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.96f, 0.93f, 0.88f);
            RenderSettings.fogStartDistance = 36f;
            RenderSettings.fogEndDistance = 160f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var host = FindDeep(level.transform, "Courtyard Volume");
            if (!host)
                host = new GameObject("Courtyard Volume").transform;
            host.SetParent(level.transform, false);
            var volume = host.GetComponent<Volume>() ?? host.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            string profilePath = Folder + "/Materials/CourtyardVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            var bloom = profile.TryGet(out Bloom existingBloom) ? existingBloom : profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.15f);
            bloom.scatter.Override(0.62f);
            var grading = profile.TryGet(out ColorAdjustments existingGrade) ? existingGrade : profile.Add<ColorAdjustments>(true);
            grading.postExposure.Override(0.06f);
            grading.colorFilter.Override(new Color(1f, 0.98f, 0.95f));
            grading.saturation.Override(6f);
            var vignette = profile.TryGet(out Vignette existingVignette) ? existingVignette : profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.38f);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;

            Camera camera = level.gameplayCamera;
            camera.allowHDR = true;
            camera.farClipPlane = 1000f;
            camera.nearClipPlane = 0.3f;
            camera.backgroundColor = new Color(0.63f, 0.79f, 0.90f);
            var extra = camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = true;
            extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            Note("Scene light, gradient ambient, linear fog, bloom, warm grade and vignette are local to this scene.");
        }

        static void PlaceBackdrop(GeometryLevel level)
        {
            const string path = ArtRoot + "/Background/Santorini Backdrop.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset)
            {
                Note("Background prefab missing. Camera clear color remains.");
                return;
            }
            var existing = FindDeep(level.transform, "Santorini Backdrop");
            if (existing)
                return;
            var backdrop = (GameObject)PrefabUtility.InstantiatePrefab(asset, level.transform);
            backdrop.transform.SetParent(level.transform, false);
            backdrop.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Note("Background prefab instanced at the level root so the fold does not scale the sky.");
        }

        static void PlaceMist(Transform courtyard)
        {
            var shader = Shader.Find("BounceBack/Courtyard Mist");
            if (!shader)
            {
                Note("Mist shader missing.");
                return;
            }
            var colors = new[]
            {
                new Color(0.98f, 0.96f, 0.92f, 0.22f),
                new Color(0.94f, 0.89f, 0.82f, 0.18f),
                new Color(0.78f, 0.86f, 0.94f, 0.20f)
            };
            float[] heights = { -1.4f, -4.2f, -7.6f };
            for (int i = 0; i < heights.Length; i++)
            {
                string path = Folder + "/Materials/MistSheet" + i + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(shader) { name = "Mist Sheet " + i };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                material.SetColor("_BaseColor", colors[i]);
                EditorUtility.SetDirty(material);
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sheet.name = "Cloud Sheet " + i;
                sheet.transform.SetParent(courtyard, true);
                UnityEngine.Object.DestroyImmediate(sheet.GetComponent<Collider>());
                sheet.transform.position = new Vector3(0f, heights[i], 0.05f);
                sheet.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                sheet.transform.localScale = new Vector3(46f, 36f, 1f);
                var renderer = sheet.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Note("Three height-mist sheets sit under the island and fold with the courtyard.");
        }

        static GameObject Place(string assetPath, Transform parent, Vector3 worldPosition, Quaternion worldRotation, string wrapperName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (!asset || !parent)
                return null;
            var wrapper = new GameObject(wrapperName);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.SetPositionAndRotation(worldPosition, worldRotation);
            wrapper.transform.localScale = Vector3.one;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper.transform);
            // The imported FBX root keeps its axis and unit conversion. Zeroing it mirrors or shrinks the mesh.
            StripPhysics(model);
            Remap(wrapper);
            return wrapper;
        }

        static GameObject ClonePart(string fbxPath, string partName, Transform parent, Vector3 worldPosition, string wrapperName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            Transform source = asset ? asset.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == partName) : null;
            if (!source || !parent)
                return null;
            var wrapper = new GameObject(wrapperName);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            wrapper.transform.localScale = Vector3.one;
            var copy = UnityEngine.Object.Instantiate(source.gameObject, wrapper.transform);
            copy.name = partName;
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            StripPhysics(copy);
            Remap(wrapper);
            return wrapper;
        }

        static void AssignWaterMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("Mist"))
                    renderer.sharedMaterial = paint.Mist;
                else if (renderer.name.Contains("Waterfall") || renderer.name.Contains("Ribbon"))
                    renderer.sharedMaterial = paint.Waterfall;
                else
                    renderer.sharedMaterial = paint.Water;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static void EnsureRipple(GameObject pool)
        {
            var type = Type.GetType("BounceBack.StylizedWater.WaterRipple, Assembly-CSharp");
            if (type == null || pool.GetComponentInChildren(type, true))
                return;
            var renderer = pool.GetComponentInChildren<Renderer>(true);
            if (!renderer)
                return;
            var ripple = pool.AddComponent(type);
            var serialized = new SerializedObject(ripple);
            serialized.FindProperty("waterRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("impactPosition").vector3Value = new Vector3(-1.4f, 0.45f, 2.05f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static bool AddBehaviour(GameObject host, string typeName)
        {
            if (!host)
                return false;
            var type = Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null)
                return false;
            if (!host.GetComponent(type))
                host.AddComponent(type);
            return true;
        }

        static void Remap(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var replacement = paint.Match(materials[i] ? materials[i].name : renderer.name);
                    if (replacement && replacement != materials[i])
                    {
                        materials[i] = replacement;
                        changed = true;
                    }
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        }

        static void StripPhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour)
                    continue;
                string name = behaviour.GetType().Name;
                if (name is "WaterRipple" or "PoolRippleLink" or "SimplePoolRings" or "SeagullOrbit" or "LiftShutterFollow")
                    continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        static int DisableIfAboveRoad(MeshFilter[] filters, GeometryLevel level)
        {
            int disabled = 0;
            var roads = level.GetComponentsInChildren<BoxCollider>(true)
                .Where(box => box.name.Contains("Ramp") || box.name is "Launch Coping" or "Landing Terrace").ToArray();
            foreach (var filter in filters)
            {
                if (!filter.sharedMesh)
                    continue;
                bool hit = false;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (roads.Any(road => AboveRoad(road, world)))
                    {
                        hit = true;
                        break;
                    }
                }
                if (!hit)
                    continue;
                filter.GetComponent<Renderer>().enabled = false;
                disabled++;
            }
            return disabled;
        }

        static bool AboveRoad(BoxCollider road, Vector3 world)
        {
            Vector3 local = road.transform.InverseTransformPoint(world) - road.center;
            float top = road.size.y * 0.5f;
            return Mathf.Abs(local.x) <= road.size.x * 0.5f - 0.05f
                && Mathf.Abs(local.z) <= road.size.z * 0.5f + 0.02f
                && local.y > top + Tolerance
                && local.y < top + 1.3f;
        }

        static bool BlocksCourse(GameObject art, GeometryLevel level)
        {
            var filters = art.GetComponentsInChildren<MeshFilter>(true);
            return level.GetComponentsInChildren<BoxCollider>(true).Any(box =>
                box.name is "Straight Stair Ramp" or "Pool Drop Ramp" or "Launch Coping" or "Landing Terrace"
                or "Cascade Lip Ramp" or "Elastic Pool Surface" || box.name.StartsWith("Turn Ramp ")
                    ? filters.Any(filter => filter.sharedMesh && filter.sharedMesh.vertices.Any(vertex => AboveRoad(box, filter.transform.TransformPoint(vertex))))
                    : false);
        }

        static float MaxAbove(BoxCollider box, MeshFilter[] filters)
        {
            float top = box.size.y * 0.5f;
            float max = 0f;
            foreach (var filter in filters)
            {
                if (!filter.sharedMesh)
                    continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 local = box.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)) - box.center;
                    if (Mathf.Abs(local.x) > box.size.x * 0.5f + Tolerance || Mathf.Abs(local.z) > box.size.z * 0.5f + Tolerance)
                        continue;
                    max = Mathf.Max(max, local.y - top);
                }
            }
            return max;
        }

        static float CornerError(Vector3[] corners, MeshFilter[] filters)
        {
            if (corners == null || corners.Length == 0 || filters == null || filters.Length == 0)
                return 99f;
            float worst = 0f;
            foreach (var corner in corners)
            {
                float best = float.MaxValue;
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh)
                        continue;
                    var vertices = mesh.vertices;
                    var triangles = mesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Vector3 a = filter.transform.TransformPoint(vertices[triangles[i]]);
                        Vector3 b = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                        Vector3 c = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                        best = Mathf.Min(best, DistancePointTriangle(corner, a, b, c));
                    }
                }
                if (best == float.MaxValue)
                    return 99f;
                worst = Mathf.Max(worst, best);
            }
            return worst;
        }

        static float DistancePointTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = point - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
                return Vector3.Distance(point, a);
            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
                return Vector3.Distance(point, b);
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                return Vector3.Distance(point, a + ab * (d1 / (d1 - d3)));
            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
                return Vector3.Distance(point, c);
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                return Vector3.Distance(point, a + ac * (d2 / (d2 - d6)));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return Vector3.Distance(point, b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6))));
            float denom = 1f / (va + vb + vc);
            return Vector3.Distance(point, a + ab * (vb * denom) + ac * (vc * denom));
        }

        static Vector3[] Corners(BoxCollider box)
        {
            Vector3 extents = box.size * 0.5f;
            var corners = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        corners[index++] = box.transform.TransformPoint(box.center + Vector3.Scale(extents, new Vector3(x, y, z)));
            return corners;
        }

        static Vector3[] Face(BoxCollider box, bool top)
        {
            float sign = top ? 1f : -1f;
            return Corners(box).OrderByDescending(corner => sign * Vector3.Dot(corner, box.transform.up)).Take(4).ToArray();
        }

        static Vector3[] PoolWaterCorners()
        {
            Vector3 top = SantoriniSystemsAuthoring.PoolWaterTop;
            Vector3 half = SantoriniSystemsAuthoring.PoolWaterSize * 0.5f;
            return new[]
            {
                top + new Vector3(-half.x, 0f, -half.z),
                top + new Vector3(-half.x, 0f, half.z),
                top + new Vector3(half.x, 0f, -half.z),
                top + new Vector3(half.x, 0f, half.z)
            };
        }

        static MeshFilter[] Filters(GameObject root, string token) =>
            root.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.name.Contains(token)).ToArray();

        static BoxCollider ColliderNamed(Transform root, string name) =>
            root.GetComponentsInChildren<BoxCollider>(true).FirstOrDefault(box => box.name == name);

        static void HideNamed(Transform root, params string[] names)
        {
            if (!root)
                return;
            var set = new HashSet<string>(names);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (set.Contains(renderer.gameObject.name))
                    renderer.enabled = false;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (!root)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found)
                    return found;
            }
            return null;
        }

        static bool AncestorNamed(Transform transform, string name)
        {
            for (var current = transform; current; current = current.parent)
                if (current.name == name)
                    return true;
            return false;
        }

        static bool Under(Transform transform, Transform ancestor) => ancestor && transform && (transform == ancestor || transform.IsChildOf(ancestor));

        static bool IsBlockout(Material material) => material && (material.name.StartsWith("Courtyard") || material.name.StartsWith("Water")
            || material.name.StartsWith("Plate") || material.name.StartsWith("Reflector") || material.name.StartsWith("Pressure")
            || material.name.StartsWith("Previous"));

        static bool MatchesAxes(Vector3 size, float a, float b, float c)
        {
            float[] values = { size.x, size.y, size.z };
            float[] expected = { a, b, c };
            System.Array.Sort(values);
            System.Array.Sort(expected);
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(values[i] - expected[i]) > 0.08f)
                    return false;
            return true;
        }

        static Vector3 LargestMeshSize(GameObject root)
        {
            Vector3 best = Vector3.zero;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh)
                    continue;
                Vector3 scale = filter.transform.lossyScale;
                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                if (size.magnitude > best.magnitude)
                    best = size;
            }
            return best;
        }

        static Bounds WorldBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static int Triangles(GameObject root)
        {
            int count = 0;
            if (!root)
                return 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && filter.GetComponent<Renderer>() is { enabled: true })
                    count += filter.sharedMesh.triangles.Length / 3;
            return count;
        }

        static void Accept(GameObject root)
        {
            if (root)
                Accepted.Add(root);
        }

        static void Note(string line)
        {
            Report.AppendLine(line);
            Debug.Log("I_ART " + line);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string ToFull(string assetPath) => Path.GetFullPath(assetPath);

        sealed class MaterialSet
        {
            public Material Ivory, IvoryShade, Rose, RoseDeep, Lilac, Apricot, Dome, Mint, Water, Gold, Waterfall, Mist;
            public static MaterialSet Create()
            {
                var set = new MaterialSet
                {
                    Ivory = Lit("MV_Ivory", new Color(0.95f, 0.88f, 0.77f), 0.18f),
                    IvoryShade = Lit("MV_IvoryShade", new Color(0.86f, 0.76f, 0.70f), 0.16f),
                    Rose = Lit("MV_Rose", new Color(0.74f, 0.45f, 0.47f), 0.16f),
                    RoseDeep = Lit("MV_RoseDeep", new Color(0.55f, 0.30f, 0.38f), 0.14f),
                    Lilac = Lit("MV_Lilac", new Color(0.50f, 0.45f, 0.66f), 0.14f),
                    Apricot = Lit("MV_Apricot", new Color(0.90f, 0.58f, 0.37f), 0.22f),
                    Dome = Lit("MV_DomeBlue", new Color(0.30f, 0.47f, 0.80f), 0.28f),
                    Mint = Lit("MV_Mint", new Color(0.36f, 0.70f, 0.55f), 0.62f),
                    Gold = Lit("MV_Gold", new Color(0.96f, 0.70f, 0.32f), 0.5f, true),
                    Water = WaterMaterial(),
                    Waterfall = ShaderMaterial("MV_Waterfall", "BounceBack/G Stylized Waterfall", new Color(0.45f, 0.80f, 0.84f)),
                    Mist = ShaderMaterial("MV_Mist", "BounceBack/G Water Mist", new Color(0.80f, 0.91f, 0.91f, 0.65f))
                };
                return set;
            }

            public Material Match(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                if (name.Contains("MV_IvoryShade")) return IvoryShade;
                if (name.Contains("MV_RoseDeep")) return RoseDeep;
                if (name.Contains("MV_Ivory")) return Ivory;
                if (name.Contains("MV_Rose")) return Rose;
                if (name.Contains("MV_Lilac")) return Lilac;
                if (name.Contains("MV_Apricot")) return Apricot;
                if (name.Contains("MV_DomeBlue")) return Dome;
                if (name.Contains("MV_Mint")) return Mint;
                if (name.Contains("MV_Waterfall")) return Waterfall;
                if (name.Contains("MV_Mist")) return Mist;
                if (name.Contains("MV_Water")) return Water;
                if (name.Contains("MV_Gold")) return Gold;
                return null;
            }
        }

        static Material Lit(string name, Color color, float smoothness, bool emit = false)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path) ?? new Material(shader);
            if (!AssetDatabase.Contains(material))
            {
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emit)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.4f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material WaterMaterial()
        {
            var shader = Shader.Find("BounceBack/G Stylized Pool");
            if (!shader || shader.name != "BounceBack/G Stylized Pool")
                return Lit("MV_Water", new Color(0.45f, 0.80f, 0.84f), 0.35f);
            string path = Folder + "/Materials/MV_Water.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path) ?? new Material(shader);
            if (!AssetDatabase.Contains(material))
            {
                material.name = "MV_Water";
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.45f, 0.80f, 0.84f, 1f));
            material.SetVector("_PoolSize", new Vector4(3.25f, 3.65f, 0f, 0f));
            material.SetFloat("_WaveHeight", 0.006f);
            material.SetFloat("_FoamWidth", 0.085f);
            material.SetFloat("_RingWidth", 0.085f);
            material.SetFloat("_Charge", 0f);
            material.SetVector("_ImpactPos", new Vector4(-1.4f, 0.45f, 2.05f, 0f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material ShaderMaterial(string name, string shaderName, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (!shader || shader.name != shaderName)
                return Lit(name, color, 0.2f);
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path) ?? new Material(shader);
            if (!AssetDatabase.Contains(material))
            {
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static int phase;
        static float marker;
        static bool released;
        static RenderTexture captureTarget;
        static string ShotDir => Path.Combine(Application.dataPath, "Scenes/BounceBack/Art/I-ArtIntegration/Preview");

        [InitializeOnLoadMethod]
        static void ResumeCapture()
        {
            if (!SessionState.GetBool(CaptureFlag, false))
                return;
            EditorApplication.playModeStateChanged -= OnCaptureMode;
            EditorApplication.playModeStateChanged += OnCaptureMode;
        }

        public static void CapturePlayShots()
        {
            SessionState.SetBool(CaptureFlag, true);
            Directory.CreateDirectory(ShotDir);
            EditorSceneManager.OpenScene(SantoriniSystemsAuthoring.LevelPath, OpenSceneMode.Single);
            EditorApplication.playModeStateChanged -= OnCaptureMode;
            EditorApplication.playModeStateChanged += OnCaptureMode;
            EditorApplication.EnterPlaymode();
        }

        static void OnCaptureMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;
            phase = 0;
            marker = 0f;
            released = false;
            EditorApplication.update -= TickCapture;
            EditorApplication.update += TickCapture;
        }

        static void TickCapture()
        {
            if (!EditorApplication.isPlaying)
                return;
            var level = UnityEngine.Object.FindFirstObjectByType<GeometryLevel>();
            if (!level || !level.Ball || !level.gameplayCamera)
            {
                if (Time.realtimeSinceStartup > 30f)
                    Finish(2);
                return;
            }
            if (level.TryGetComponent(out PuzzleCameraRig rig) && !level.Completed)
                rig.Cinematic = true;
            var ball = level.Ball.Body;
            switch (phase)
            {
                case 0:
                    if (Time.time > 1.2f)
                    {
                        Shoot(level, "01-formal.png");
                        marker = Time.time;
                        phase = 1;
                    }
                    break;
                case 1:
                    if (Time.time - marker > 0.35f)
                    {
                        Shoot(level, "02-preparation.png");
                        foreach (var mechanism in level.GetComponentsInChildren<ElasticGeometry>())
                            if (mechanism.draggable)
                                mechanism.SetTargetOffset(0f);
                        marker = Time.time;
                        phase = 2;
                    }
                    break;
                case 2:
                    if (Time.time - marker > 0.9f)
                    {
                        level.ReleaseBall();
                        released = true;
                        marker = Time.time;
                        phase = 3;
                    }
                    break;
                case 3:
                    bool airborne = level.Ball.Released && !level.Ball.InTransition && ball.linearVelocity.y > 1.2f
                        && ball.position.y > 0.9f && ball.position.x > -2.2f;
                    if (airborne || Time.time - marker > 6f)
                    {
                        Shoot(level, "03-airborne.png");
                        Debug.Log($"I_ART airborne pos={ball.position} vel={ball.linearVelocity} released={level.Ball.Released}");
                        phase = 4;
                    }
                    break;
                case 4:
                    if (ball.position.x > 1f && ball.linearVelocity.y > 1.35f && !level.Ball.InTransition)
                    {
                        Shoot(level, "05-second-bounce.png");
                        phase = 5;
                    }
                    else if (level.Completed || Time.time - marker > 7f)
                        phase = 5;
                    break;
                case 5:
                    if (level.Completed && level.goal.GetComponent<GoalPressurePlate>().pulses
                        .Any(pulse => pulse.enabled && pulse.transform.localScale.x > 2.2f))
                    {
                        Shoot(level, "06-pressure-plate.png");
                        phase = 6;
                    }
                    else if (Time.time - marker > 8f)
                        phase = 6;
                    break;
                case 6:
                    var art = level.transform.Find("Courtyard Art");
                    if (level.Completed && art && art.localScale.x <= 0.62f)
                    {
                        Shoot(level, "04-fold.png");
                        Debug.Log($"I_ART fold scale={art.localScale} pos={ball.position}");
                        Finish(0);
                    }
                    else if (released && Time.time - marker > 22f)
                    {
                        Debug.Log($"I_ART capture timeout pos={ball.position} completed={level.Completed}");
                        Finish(level.Completed ? 0 : 3);
                    }
                    break;
            }
        }

        static void Shoot(GeometryLevel level, string file)
        {
            Camera camera = level.gameplayCamera;
            if (!captureTarget)
                captureTarget = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            camera.targetTexture = captureTarget;
            camera.aspect = 16f / 9f;
            camera.Render();
            var active = RenderTexture.active;
            RenderTexture.active = captureTarget;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, file), image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            RenderTexture.active = active;
            camera.targetTexture = previous;
            if (level.Ball)
            {
                Vector3 viewport = camera.WorldToViewportPoint(level.Ball.Body.position);
                float pixels = 1080f * level.spawn.diameter / (2f * camera.orthographicSize);
                Debug.Log($"I_ART shot {file} ortho={camera.orthographicSize:F2} ballViewport={viewport} theoreticalPx={pixels:F1}");
            }
        }

        static void DisableFilters(MeshFilter[] filters)
        {
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer)
                    renderer.enabled = false;
            }
        }

        static void Finish(int code)
        {
            EditorApplication.update -= TickCapture;
            SessionState.SetBool(CaptureFlag, false);
            Debug.Log("I_ART_CAPTURE_DONE " + code);
            EditorApplication.Exit(code);
        }
    }
}
