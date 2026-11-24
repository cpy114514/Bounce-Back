using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BounceBack.GeometryWorld.Editor
{
    internal sealed class MenuModel
    {
        private static Dictionary<string, Material> palette;
        private static string palettePath;

        private readonly Transform root;
        private Transform construction;
        private readonly MenuAssetMeshes assets;
        private readonly System.Random random;
        private readonly MenuArtPalette.District paletteDistrict;
        private readonly List<MenuIsland.Fragment> fragments = new();
        private readonly List<MenuIsland.ClockHand> clocks = new();
        private readonly Dictionary<string, Mesh> primitives = new();
        private Dictionary<string, Transform> sourceGroups;
        private Quaternion sourceRotation;

        internal MenuModel(Transform root, MenuAssetMeshes assets, int seed,
            MenuArtPalette.District paletteDistrict = MenuArtPalette.District.Unchanged)
        {
            this.root = root;
            construction = root;
            this.assets = assets;
            this.paletteDistrict = paletteDistrict;
            random = new System.Random(seed);
        }

        internal void UseModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer)
                throw new InvalidOperationException($"Blender export is missing: {path}");
            if (!importer.isReadable || importer.bakeAxisConversion || importer.importCameras || importer.importLights)
            {
                importer.isReadable = true;
                importer.bakeAxisConversion = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sourceRotation = model.transform.rotation;
            sourceGroups = model.GetComponentsInChildren<Transform>()
                .GroupBy(item => item.name)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.TryGetComponent<MeshFilter>(out _)).First());
        }

        internal void Baked(Transform parent, string name, params string[] separatelyAnimated)
        {
            Transform group = SourceGroup(name);
            var source = new Dictionary<Material, List<CombineInstance>>();
            Matrix4x4 pivot = Matrix4x4.TRS(group.position, SourceHeading(group), Vector3.one);
            var excluded = separatelyAnimated.Select(child => sourceGroups[child]).ToArray();
            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>())
            {
                Transform child = renderer.transform;
                if (excluded.Any(child.IsChildOf) || !child.TryGetComponent(out MeshFilter filter))
                    continue;
                Matrix4x4 relative = pivot.inverse * child.localToWorldMatrix;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    string materialName = renderer.sharedMaterials[i].name;
                    if (!materialName.StartsWith("BB_", StringComparison.Ordinal) ||
                        !ColorUtility.TryParseHtmlString("#" + materialName[3..], out Color color))
                        throw new InvalidOperationException($"Material '{materialName}' on {name} needs a BB_RRGGBB name.");
                    Material material = Surface(MenuArtPalette.ColorFor(materialName[3..], color, paletteDistrict));
                    if (!source.TryGetValue(material, out var instances))
                        source.Add(material, instances = new List<CombineInstance>());
                    instances.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i, transform = relative });
                }
            }
            if (source.Count == 0)
                throw new InvalidOperationException($"Blender group '{name}' has no renderers of its own.");

            var parts = new List<CombineInstance>();
            foreach (var submesh in source.Values)
            {
                var temp = new Mesh();
                temp.CombineMeshes(submesh.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = temp });
            }
            var mesh = new Mesh();
            mesh.CombineMeshes(parts.ToArray(), false, false);
            foreach (var part in parts)
                Object.DestroyImmediate(part.mesh);
            MarkMesh(parent, assets.Store(mesh, name), source.Keys.ToArray());
        }

        internal void ImportedPiece(string name, Vector3 ruined, Vector3 ruinedEuler, float delay, float arc,
            Vector3 openOffset = default)
        {
            Transform source = SourceGroup(name);
            Baked(Piece(name, source.position, ruined, ruinedEuler, delay, arc, openOffset,
                assembledHeading: SourceHeading(source)), name);
        }

        private Transform SourceGroup(string name) => sourceGroups.TryGetValue(name, out Transform group)
            ? group : throw new InvalidOperationException($"Blender group '{name}' is missing.");

        private Quaternion SourceHeading(Transform group) => group.rotation * Quaternion.Inverse(sourceRotation);

        internal Transform Piece(string name, Vector3 assembledPivot, Vector3 ruinedPivot, Vector3 ruinedEuler,
            float delay = 0f, float arc = .6f, Vector3 openOffset = default, Vector3 openEuler = default,
            Quaternion? assembledHeading = null)
        {
            Transform part = Detail(name, assembledPivot);
            Vector3 offset = new(Next(-2.65f, 2.65f), Next(-1.9f, 3.7f), Next(-3.2f, 3.2f));
            fragments.Add(new MenuIsland.Fragment
            {
                part = part,
                assembled = assembledPivot,
                ruined = ruinedPivot,
                ruinedEuler = ruinedEuler,
                assembledRotation = assembledHeading ?? Quaternion.identity,
                delay = delay,
                arc = arc,
                openedOffset = openOffset,
                openedEuler = openEuler,
                scatteredOffset = offset,
                scatteredEuler = new Vector3(Next(-46, 46), Next(-52, 52), Next(-43, 43)),
                assemblyDelay = Next(0f, .43f)
            });
            return part;
        }

        internal void BeginArchitecture(float yaw)
        {
            construction = Child(root, "Blender architecture");
            construction.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        internal Transform Detail(string name, Vector3 position) => Child(construction, name, position);

        internal void ClockHand(Transform transform, Vector3 axis)
        {
            clocks.Add(new MenuIsland.ClockHand
            {
                pivot = transform, rest = transform.localRotation, axis = axis,
                turns = transform.name.Contains("Minute") ? -.81f : -.16f
            });
        }

        internal void Finish(MenuIsland island)
        {
            island.fragments = fragments.ToArray();
            island.hands = clocks.ToArray();
            foreach (var fragment in island.fragments)
                fragment.part.SetLocalPositionAndRotation(fragment.ruined, Quaternion.Euler(fragment.ruinedEuler));
        }

        internal void GatherStaticDetails()
        {
            Gather(root);
            if (construction != root)
                Gather(construction);

            void Gather(Transform group)
            {
                foreach (Transform part in group)
                {
                    if (!part.TryGetComponent<MeshRenderer>(out _) || fragments.Exists(item => item.part == part))
                        continue;
                    fragments.Add(new MenuIsland.Fragment
                    {
                        part = part, assembled = part.localPosition, ruined = part.localPosition,
                        assembledRotation = part.localRotation,
                        scatteredOffset = new Vector3(Next(-2.2f, 2.2f), Next(-2f, 2f), Next(-2.6f, 2.6f)),
                        scatteredEuler = new Vector3(Next(-26, 26), Next(-31, 31), Next(-26, 26)),
                        assemblyDelay = Next(.02f, .28f)
                    });
                }
            }
        }

        internal Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Color color, Vector3 euler = default)
            => Primitive(parent, name, center, size, color, euler, GeometryShape.Cube, 8);

        internal Transform Cylinder(Transform parent, string name, Vector3 center, float radius, float height,
            Color color, int sides = 16, Vector3 euler = default)
            => Primitive(parent, name, center, new Vector3(radius * 2, height, radius * 2), color, euler, GeometryShape.Cylinder, sides);

        internal Transform Sphere(Transform parent, string name, Vector3 center, Vector3 size, Color color)
            => Primitive(parent, name, center, size, color, default, GeometryShape.Sphere, 12);

        private Transform Primitive(Transform parent, string name, Vector3 center, Vector3 size, Color color,
            Vector3 euler, GeometryShape shape, int sides)
        {
            string key = $"{shape} {sides}";
            if (!primitives.TryGetValue(key, out Mesh mesh))
            {
                mesh = new Mesh();
                GeometryGenerators.Generate(shape, Vector3.one, sides, 0f).WriteMesh(mesh, 1);
                primitives.Add(key, mesh = assets.Store(mesh, key));
            }
            Transform child = Child(parent, name, center);
            child.localRotation = Quaternion.Euler(euler);
            child.localScale = size;
            MarkMesh(child, mesh, new[] { Surface(color) });
            return child;
        }

        internal Transform Mesh(Transform parent, string name, Vector3 center, Mesh mesh, Color color, Vector3 euler = default)
        {
            Transform child = Child(parent, name, center);
            child.localRotation = Quaternion.Euler(euler);
            MarkMesh(child, assets.Store(mesh, name), new[] { Surface(color) });
            return child;
        }

        internal static Transform Child(Transform parent, string name, Vector3 localPosition = default)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        internal static BoxCollider Hitbox(Transform target, Vector3 center, Vector3 size)
        {
            var box = target.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.isTrigger = true;
            return box;
        }

        internal static void LoadPalette()
        {
            palettePath = GeometryAssets.EnsureFolder(MainMenuAuthoring.AssetRoot + "/Materials") + "/Architecture Palette.mat";
            palette = AssetDatabase.LoadAllAssetsAtPath(palettePath).OfType<Material>().ToDictionary(asset => asset.name);
        }

        internal static Material Surface(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            if (palette.TryGetValue(key, out var existing))
            {
                if (existing.GetColor("_SpecColor") != Color.black || existing.GetFloat("_Smoothness") != .08f)
                {
                    existing.SetColor("_SpecColor", Color.black);
                    existing.SetFloat("_Smoothness", .08f);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = key, color = color };
            material.SetColor("_SpecColor", Color.black);
            material.SetFloat("_Smoothness", .08f);
            if (palette.Count == 0)
                AssetDatabase.CreateAsset(material, palettePath);
            else
                AssetDatabase.AddObjectToAsset(material, palettePath);
            palette.Add(key, material);
            return material;
        }

        private static void MarkMesh(Transform child, Mesh mesh, Material[] materials)
        {
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    internal sealed class MenuAssetMeshes
    {
        private readonly string path;
        private readonly Dictionary<string, Mesh> meshes;
        private readonly HashSet<string> authored = new();

        internal MenuAssetMeshes(string name)
        {
            path = $"{GeometryAssets.EnsureFolder(MainMenuAuthoring.AssetRoot + "/Models/Runtime")}/{name}.asset";
            meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(mesh => mesh.name);
        }

        internal Mesh Store(Mesh mesh, string name)
        {
            if (!authored.Add(name))
            {
                Object.DestroyImmediate(mesh);
                return meshes[name];
            }
            mesh.name = name;
            if (meshes.TryGetValue(name, out var previous))
            {
                previous.Clear(false);
                previous.indexFormat = mesh.indexFormat;
                previous.vertices = mesh.vertices;
                previous.normals = mesh.normals;
                previous.tangents = mesh.tangents;
                previous.colors32 = mesh.colors32;
                var channel = new List<Vector4>();
                for (int index = 0; index < 8; index++)
                {
                    mesh.GetUVs(index, channel);
                    if (channel.Count > 0)
                        previous.SetUVs(index, channel);
                }
                previous.subMeshCount = mesh.subMeshCount;
                for (int index = 0; index < mesh.subMeshCount; index++)
                    previous.SetIndices(mesh.GetIndices(index), mesh.GetTopology(index), index, false);
                previous.bounds = mesh.bounds;
                previous.UploadMeshData(false);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(previous);
                return previous;
            }
            if (meshes.Count == 0)
                AssetDatabase.CreateAsset(mesh, path);
            else
                AssetDatabase.AddObjectToAsset(mesh, path);
            meshes.Add(name, mesh);
            return mesh;
        }

        internal void RemoveObsolete()
        {
            foreach (string name in meshes.Keys.Where(name => !authored.Contains(name)).ToArray())
            {
                Mesh previous = meshes[name];
                if (AssetDatabase.IsMainAsset(previous))
                    continue;
                Object.DestroyImmediate(previous, true);
                meshes.Remove(name);
            }
        }
    }
}
