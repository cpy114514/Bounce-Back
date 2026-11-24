using System;
using System.Collections.Generic;
using System.Linq;
using BounceBack.Geometry;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class GeometryAssets
    {
        internal const string Library = "Assets/GeometryWorldLibrary";
        internal const string Models = Library + "/Models";
        internal const string Materials = Library + "/Materials";
        internal const string Prefabs = Library + "/Prefabs";

        internal static readonly (string Name, Color Color)[] Pastels =
        {
            ("Ivory", new Color32(248, 231, 209, 255)),
            ("Mint", new Color32(137, 204, 183, 255)),
            ("Lilac", new Color32(166, 157, 220, 255)),
            ("Apricot", new Color32(243, 173, 140, 255)),
            ("Sky", new Color32(140, 185, 219, 255)),
            ("Rose", new Color32(227, 157, 175, 255)),
            ("Midnight", new Color32(58, 57, 95, 255))
        };

        internal static string EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string folder = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = $"{folder}/{pieces[i]}";
                if (!AssetDatabase.IsValidFolder(next) &&
                    string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, pieces[i])))
                    throw new InvalidOperationException($"Unity could not create {next}.");
                folder = next;
            }
            return folder;
        }

        internal static Material Preset(int index)
        {
            var preset = Pastels[index];
            return CreateMaterial(preset.Name, preset.Color, 0f, 0.32f, 1f, reuse: true);
        }

        internal static Material EdgeMaterial(Color color) =>
            CreateMaterial("Ink " + ColorUtility.ToHtmlStringRGBA(color), color, 0f, 0.32f, color.a, reuse: true);

        internal static Material CreateMaterial(string name, Color color, float metallic, float smoothness, float opacity,
            bool reuse = false, string directory = Materials, Shader shaderOverride = null)
        {
            var shader = shaderOverride ? shaderOverride : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("The project’s Lit shader could not be found.");

            string suggested = $"{EnsureFolder(directory)}/{name}.mat";
            if (reuse)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(suggested);
                if (existing != null)
                    return existing;
            }

            var material = new Material(shader) { name = name };
            color.a = opacity;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (opacity < 0.999f)
                SetTransparent(material);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(suggested));
            return material;
        }

        private static void SetTransparent(Material material)
        {
            bool urp = material.HasProperty("_Surface");
            material.SetFloat(urp ? "_Surface" : "_Mode", urp ? 1f : 2f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword(urp ? "_SURFACE_TYPE_TRANSPARENT" : "_ALPHABLEND_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        internal static GeometryMeshAsset CreateModel(string name, MeshTopology topology, Material[] materials,
            GeometryDrawing source = null, string folder = Models)
        {
            Mesh mesh = new Mesh { name = name + " Mesh" };
            try
            {
                topology.WriteMesh(mesh, materials.Length);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }

            var asset = ScriptableObject.CreateInstance<GeometryMeshAsset>();
            asset.name = name;
            asset.topology = topology;
            asset.mesh = mesh;
            asset.materials = materials;
            asset.sourceDrawing = source;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnsureFolder(folder)}/{name}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(mesh, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        internal static GeometryObject Instantiate(GeometryMeshAsset model, Transform parent, Vector3 position,
            bool decorative, bool collider, bool undo = true)
        {
            var root = new GameObject(model.name);
            if (parent)
                root.transform.SetParent(parent, false);
            root.transform.position = position;
            var geometry = root.AddComponent<GeometryObject>();
            var surface = new GameObject("Surface");
            surface.transform.SetParent(root.transform, false);
            geometry.surface = surface.AddComponent<MeshFilter>();
            geometry.surfaceRenderer = surface.AddComponent<MeshRenderer>();
            geometry.model = model;
            geometry.decorative = decorative;
            Sync(geometry, true);
            if (collider && !decorative)
                SetCollider(geometry, true, false);
            if (undo)
                Undo.RegisterCreatedObjectUndo(root, "Place Geometry");
            return geometry;
        }

        internal static void SetCollider(GeometryObject geometry, bool enabled, bool undo = true)
        {
            if (enabled && geometry.gameplayCollider == null)
            {
                bool moving = geometry.GetComponent<Rigidbody>();
                if (moving)
                {
                    var box = undo ? Undo.AddComponent<BoxCollider>(geometry.gameObject) : geometry.gameObject.AddComponent<BoxCollider>();
                    box.center = geometry.model.mesh.bounds.center;
                    box.size = Vector3.Max(geometry.model.mesh.bounds.size, Vector3.one * 0.03f);
                    geometry.gameplayCollider = box;
                }
                else
                {
                    var collider = undo ? Undo.AddComponent<MeshCollider>(geometry.gameObject) : geometry.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = geometry.model.mesh;
                    geometry.gameplayCollider = collider;
                }
            }
            if (geometry.gameplayCollider)
            {
                if (undo)
                    Undo.RecordObject(geometry.gameplayCollider, "Set Geometry Collider");
                geometry.gameplayCollider.enabled = enabled;
                PrefabUtility.RecordPrefabInstancePropertyModifications(geometry.gameplayCollider);
            }
            if (undo)
                PrefabUtility.RecordPrefabInstancePropertyModifications(geometry);
        }

        internal static void Sync(GeometryObject geometry, bool resetMaterials)
        {
            if (!geometry || !geometry.model || !geometry.surface || !geometry.surfaceRenderer)
                return;
            geometry.surface.sharedMesh = geometry.model.mesh;
            if (geometry.gameplayCollider is MeshCollider meshCollider)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = geometry.model.mesh;
            }

            Material[] defaults = geometry.model.materials;
            Material[] assigned = geometry.surfaceRenderer.sharedMaterials;
            if (!resetMaterials && assigned.Length == defaults.Length)
                return;
            var slots = (Material[])defaults.Clone();
            if (!resetMaterials)
                Array.Copy(assigned, slots, Mathf.Min(assigned.Length, slots.Length));
            geometry.surfaceRenderer.sharedMaterials = slots;
        }

        internal static IEnumerable<GeometryObject> Instances(GeometryMeshAsset asset) =>
            UnityEngine.Object.FindObjectsByType<GeometryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(geometry => geometry.model == asset && !EditorUtility.IsPersistent(geometry));

        internal static void CommitModel(GeometryMeshAsset asset, MeshTopology topology, Material[] materials, string label)
        {
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { asset, asset.mesh }, label);
            topology.WriteMesh(asset.mesh, materials.Length);
            asset.topology = topology;
            asset.materials = materials;
            foreach (GeometryObject instance in Instances(asset))
            {
                if (instance.surfaceRenderer && instance.surfaceRenderer.sharedMaterials.Length != materials.Length)
                    Undo.RecordObject(instance.surfaceRenderer, label);
                Sync(instance, false);
                if (instance.surfaceRenderer)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.surfaceRenderer);
            }
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.mesh);
        }

        internal static void AssignUnique(GeometryObject instance, bool centerPivot)
        {
            var original = instance.model;
            var topology = original.topology.Copy();
            Vector3 pivot = centerPivot ? original.mesh.bounds.center : Vector3.zero;
            for (int i = 0; i < topology.vertices.Count; i++)
                topology.vertices[i] -= pivot;

            var copy = CreateModel(original.name + " Variant", topology, original.materials, original.sourceDrawing);
            Undo.RecordObjects(new UnityEngine.Object[] { instance, instance.transform, instance.surface }, "Make Model Unique");
            if (instance.gameplayCollider)
                Undo.RecordObject(instance.gameplayCollider, "Make Model Unique");
            instance.model = copy;
            Vector3 travel = instance.surface.transform.TransformVector(pivot);
            instance.transform.position += travel;
            if (instance.gameplayCollider is BoxCollider box)
                box.center -= instance.transform.InverseTransformVector(travel);
            Sync(instance, false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.surface);
            if (instance.gameplayCollider)
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.gameplayCollider);
        }

        internal static string SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success)
                throw new InvalidOperationException($"Unity could not save {path}.");
            return path;
        }

        internal static GameObject PlacePrefab(GameObject asset, Transform parent, Scene scene, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            Undo.RegisterCreatedObjectUndo(instance, "Place Geometry Prefab");
            return instance;
        }
    }
}
