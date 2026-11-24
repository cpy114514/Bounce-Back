using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld
{
    [DefaultExecutionOrder(1000)]
    internal sealed class CampaignSceneParticles : MonoBehaviour
    {
        private const int Count = 320;
        private const float ScatterSeconds = 0.48f;
        private const float GatherSeconds = 0.62f;
        private const float GoldenAngle = 2.399963f;
        private static readonly Color CloudColor = new Color32(245, 211, 160, 255);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static CampaignSceneParticles active;

        private readonly List<MeshRenderer> hidden = new();
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[Count];
        private ParticleSystem field;
        private Mesh shard;
        private Frame departing;
        private Camera frameCamera;
        private Frame frameToDraw;
        private float frameSpread;

        internal static bool IsRunning => active;

        internal static bool TryPlay(Func<AsyncOperation> switchScene)
        {
            var source = SceneManager.GetActiveScene();
            Camera view = SceneCamera(source);
            var material = Resources.Load<Material>("BounceBack/Scene Transition Particles");
            if (!view || !material)
                return false;

            List<MeshRenderer> models = SceneModels(source);
            var planes = GeometryUtility.CalculateFrustumPlanes(view);
            models.RemoveAll(model => !model.gameObject.activeInHierarchy ||
                !GeometryUtility.TestPlanesAABB(planes, model.bounds));
            if (models.Count == 0)
                return false;

            var effect = new GameObject("Campaign Scene Particles") { hideFlags = HideFlags.DontSave };
            active = effect.AddComponent<CampaignSceneParticles>();
            DontDestroyOnLoad(effect);
            active.CreateField(material);
            active.StartCoroutine(active.Play(view, models, switchScene));
            return true;
        }

        private IEnumerator Play(Camera view, List<MeshRenderer> models, Func<AsyncOperation> switchScene)
        {
            var from = departing = new Frame(view, models);
            yield return Animate(view, from, false);

            SceneManager.sceneLoaded += OnSceneLoaded;
            AsyncOperation loading = switchScene();
            if (loading == null)
            {
                yield return Animate(view, from, true);
                Finish();
                yield break;
            }
            while (!loading.isDone)
            {
                Camera current = SceneCamera(SceneManager.GetActiveScene());
                if (current)
                    ShowFrame(current, from, 1f);
                yield return null;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            departing = null;
            yield return null;

            Camera nextView = SceneCamera(SceneManager.GetActiveScene());
            if (nextView)
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(nextView);
                var visible = hidden.FindAll(model => model && model.gameObject.activeInHierarchy &&
                    GeometryUtility.TestPlanesAABB(planes, model.bounds));
                if (visible.Count > 0)
                    yield return Animate(nextView, new Frame(nextView, visible), true);
            }
            Finish();
        }

        private IEnumerator Animate(Camera view, Frame frame, bool incoming)
        {
            float duration = incoming ? GatherSeconds : ScatterSeconds;
            for (float elapsed = 0f; elapsed < duration && view; elapsed += Time.unscaledDeltaTime)
            {
                float rhythm = elapsed / duration;
                if (incoming && rhythm >= 0.84f)
                    RestoreModels();
                if (!incoming && rhythm >= 0.12f && hidden.Count == 0)
                    HideModels(frame.Models);
                ShowFrame(view, frame, incoming ? 1f - rhythm : rhythm);
                yield return null;
            }
            if (incoming)
                RestoreModels();
            else if (view)
            {
                HideModels(frame.Models);
                ShowFrame(view, frame, 1f);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;
            RestoreModels();
            HideModels(SceneModels(scene));
            Camera view = SceneCamera(scene);
            if (view && departing != null)
                ShowFrame(view, departing, 1f);
        }

        private void HideModels(List<MeshRenderer> models)
        {
            foreach (var model in models)
            {
                if (!model || model.forceRenderingOff)
                    continue;
                model.forceRenderingOff = true;
                hidden.Add(model);
            }
        }

        private void RestoreModels()
        {
            foreach (var model in hidden)
                if (model)
                    model.forceRenderingOff = false;
            hidden.Clear();
        }

        private void CreateField(Material material)
        {
            shard = new Mesh { name = "Stone transition fragment", hideFlags = HideFlags.DontSave };
            shard.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            shard.uv = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, Vector2.one, Vector2.zero };
            shard.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            shard.RecalculateNormals();

            field = gameObject.AddComponent<ParticleSystem>();
            field.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = field.main;
            main.maxParticles = Count;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = field.emission;
            emission.enabled = false;
            var renderer = field.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = shard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            field.Play();
        }

        private void ShowFrame(Camera view, Frame frame, float spread)
        {
            frameCamera = view;
            frameToDraw = frame;
            frameSpread = spread;
        }

        private void LateUpdate()
        {
            if (active == this && frameCamera && frameToDraw != null)
                Draw(frameCamera, frameToDraw, frameSpread);
        }

        private void Draw(Camera view, Frame frame, float spread)
        {
            float eased = Mathf.SmoothStep(0f, 1f, spread);
            float arc = Mathf.Sin(Mathf.PI * spread);
            float swirl = Mathf.Repeat(Time.unscaledTime * 0.34f, Mathf.PI * 2f);
            float depth = view.nearClipPlane + 0.75f;
            float height = view.orthographic ? 2f * view.orthographicSize :
                2f * depth * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
            for (int i = 0; i < Count; i++)
            {
                float angle = i * GoldenAngle + swirl;
                float radius = Mathf.Sqrt((i + 0.5f) / Count);
                Vector3 drift = new(Mathf.Cos(angle) * 0.29f * radius, Mathf.Sin(angle) * 0.34f * radius, radius * 0.5f);
                Vector3 cloud = new Vector3(0.5f, 0.53f, 0f) + drift;
                Vector3 position = Vector3.Lerp(frame.Points[i], cloud, eased) + Vector3.up * (0.08f * arc * radius);
                position.z = depth + Mathf.Lerp(0.04f, 0.28f, position.z);
                particles[i].position = view.ViewportToWorldPoint(position);
                particles[i].rotation3D = new Vector3(i * 73f, i * 39f + spread * 170f, i * 17f + spread * 100f);
                particles[i].startColor = Color.Lerp(frame.Colors[i], CloudColor, eased);
                float size = 0.025f + 0.975f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(spread / 0.19f));
                particles[i].startSize = height * (0.0032f + (i % 7) * 0.00052f) * size;
                particles[i].startLifetime = particles[i].remainingLifetime = 2f;
            }
            field.SetParticles(particles, Count);
        }

        private void Finish()
        {
            RestoreModels();
            active = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreModels();
            if (active == this)
                active = null;
            if (shard)
                Destroy(shard);
        }

        private static Camera SceneCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            Camera main = Camera.main;
            if (main && main.gameObject.scene == scene)
                return main;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>())
                    if (camera.isActiveAndEnabled)
                        return camera;
            return null;
        }

        private static List<MeshRenderer> SceneModels(Scene scene)
        {
            var models = new List<MeshRenderer>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var model in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (model.enabled && !model.forceRenderingOff && !model.GetComponentInParent<BallBody>() &&
                        (model.gameObject.hideFlags & HideFlags.DontSave) == 0 &&
                        model.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                        models.Add(model);
            return models;
        }

        private sealed class Frame
        {
            internal readonly List<MeshRenderer> Models;
            internal readonly Vector3[] Points = new Vector3[Count];
            internal readonly Color[] Colors = new Color[Count];

            internal Frame(Camera view, List<MeshRenderer> models)
            {
                Models = models;
                float ratio = models.Count / (float)Count;
                var vertices = new Dictionary<Mesh, Vector3[]>();
                var properties = new MaterialPropertyBlock();
                for (int i = 0; i < Count; i++)
                {
                    var model = models[Mathf.Min(models.Count - 1, Mathf.FloorToInt((i + 0.5f) * ratio))];
                    Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
                    if (!vertices.TryGetValue(mesh, out var points))
                    {
                        points = mesh.isReadable && mesh.vertexCount <= 4096 ? mesh.vertices : Array.Empty<Vector3>();
                        vertices.Add(mesh, points);
                    }
                    Vector3 point = points.Length > 0 ? points[(i * 229 + 19) % points.Length] : BoundsPoint(mesh.bounds, i);
                    Vector3 viewport = view.WorldToViewportPoint(model.transform.TransformPoint(point));
                    if (viewport.z <= 0f)
                        viewport = view.WorldToViewportPoint(model.bounds.center);
                    Points[i] = new Vector3(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y), (i % 11) / 10f);

                    var material = model.sharedMaterial;
                    model.GetPropertyBlock(properties);
                    int property = material && material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                    Colors[i] = properties.HasColor(property) ? properties.GetColor(property) :
                        material && material.HasProperty(property) ? material.GetColor(property) : CloudColor;
                }
            }

            private static Vector3 BoundsPoint(Bounds bounds, int index)
            {
                Vector3 sample = new(Mathf.Repeat(index * 0.618034f, 1f), Mathf.Repeat(index * 0.754878f, 1f),
                    Mathf.Repeat(index * 0.569840f, 1f));
                sample[index % 3] = index % 2;
                return bounds.min + Vector3.Scale(sample, bounds.size);
            }
        }
    }
}
