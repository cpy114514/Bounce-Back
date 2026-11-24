using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class BallRetryEffect : MonoBehaviour
    {
        public GameObject fragmentVisual;
        [Range(4, 24)] public int fragmentCount = 12;
        [Min(0.1f)] public float duration = 1.1f;
        public bool rewindPath;
        public LineRenderer lastAttempt;

        public bool IsPlaying => ball;

        private readonly List<Renderer> hiddenRenderers = new();
        private Transform[] fragments;
        private Mesh fragmentMesh;
        private BallBody ball;
        private Vector3 impact;
        private float elapsed;
        private bool detectionWasEnabled;
        private readonly List<Pose> path = new();
        private BallBody recording;
        private System.Action afterRewind;

        internal void BeginRecording(BallBody target)
        {
            if (!rewindPath)
                return;
            path.Clear();
            if (lastAttempt)
                lastAttempt.positionCount = 0;
            recording = target;
            Record();
        }

        private void FixedUpdate()
        {
            if (recording && recording.Released)
                Record();
        }

        private void Record()
        {
            var pose = new Pose(recording.Body.position, recording.Body.rotation);
            if (path.Count == 0 || (pose.position - path[^1].position).sqrMagnitude >= 0.0009f)
                path.Add(pose);
        }

        internal void Rewind(BallBody target, System.Action finished)
        {
            if (recording)
                Record();
            recording = null;
            ball = target;
            elapsed = 0f;
            afterRewind = finished;
            target.Hold(target.Body.position, target.Body.rotation);
            detectionWasEnabled = target.Body.detectCollisions;
            target.Body.detectCollisions = false;
            if (lastAttempt)
            {
                lastAttempt.positionCount = path.Count;
                float radius = target.Level.spawn.diameter * 0.5f;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 point = path[i].position;
                    if (Physics.Raycast(point, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                        point.y = hit.point.y + 0.016f;
                    else
                        point.y -= radius;
                    lastAttempt.SetPosition(i, point);
                }
            }
        }

        internal void Play(BallBody target, Vector3 position)
        {
            if (!isActiveAndEnabled)
                return;
            Cancel();
            ball = target;
            impact = position;
            elapsed = 0f;
            detectionWasEnabled = ball.Body.detectCollisions;
            ball.Body.detectCollisions = false;
            foreach (var renderer in ball.GetComponentsInChildren<Renderer>())
            {
                if (renderer.forceRenderingOff)
                    continue;
                renderer.forceRenderingOff = true;
                hiddenRenderers.Add(renderer);
            }
            if (fragments == null)
                CreateFragments(hiddenRenderers.Count > 0 ? hiddenRenderers[0].sharedMaterial : null);
            foreach (var piece in fragments)
            {
                piece.position = impact;
                piece.gameObject.SetActive(true);
            }
            if (ball.Level.audioDirector)
                ball.Level.audioDirector.Play(Cue.Retry);
        }

        private void CreateFragments(Material material)
        {
            fragments = new Transform[Mathf.Clamp(fragmentCount, 4, 24)];
            for (int i = 0; i < fragments.Length; i++)
            {
                GameObject piece;
                if (fragmentVisual)
                    piece = Instantiate(fragmentVisual, transform);
                else
                {
                    piece = new GameObject("Ball fragment", typeof(MeshFilter), typeof(MeshRenderer));
                    piece.transform.SetParent(transform, false);
                    piece.GetComponent<MeshFilter>().sharedMesh = fragmentMesh ? fragmentMesh : CreateMesh();
                    if (material)
                        piece.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                fragments[i] = piece.transform;
            }
        }

        private Mesh CreateMesh()
        {
            fragmentMesh = new Mesh { name = "Retry octahedron" };
            fragmentMesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            fragmentMesh.triangles = new[] { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            fragmentMesh.RecalculateNormals();
            return fragmentMesh;
        }

        private void Update()
        {
            if (!ball)
                return;
            elapsed += Time.deltaTime;
            float time = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            if (afterRewind != null)
            {
                if (path.Count > 0)
                {
                    float step = (path.Count - 1) * (1f - Mathf.SmoothStep(0f, 1f, time));
                    int index = Mathf.FloorToInt(step);
                    Pose a = path[index];
                    Pose b = path[Mathf.Min(index + 1, path.Count - 1)];
                    ball.Body.position = Vector3.Lerp(a.position, b.position, step - index);
                    ball.Body.rotation = Quaternion.Slerp(a.rotation, b.rotation, step - index);
                }
                if (time >= 1f)
                {
                    var finished = afterRewind;
                    Cancel();
                    finished();
                }
                return;
            }
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, time));
            float spread = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, time)) * (1f - gather);
            float diameter = ball.Level.spawn.diameter;
            for (int i = 0; i < fragments.Length; i++)
            {
                float angle = 2.39996f * i;
                float height = 2f * (i + 0.5f) / fragments.Length - 1f;
                float ring = Mathf.Sqrt(1f - height * height);
                Vector3 burst = new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
                Transform piece = fragments[i];
                piece.SetPositionAndRotation(Vector3.Lerp(impact, ball.Body.position, gather) + burst * spread * diameter * 1.8f,
                    Quaternion.AngleAxis(i * 27f + time * 280f, burst));
                piece.localScale = Vector3.one * diameter * Mathf.Lerp(0.19f, 0.06f, gather);
            }
            if (time >= 1f)
                Cancel();
        }

        internal void Cancel()
        {
            if (ball)
                ball.Body.detectCollisions = detectionWasEnabled;
            ball = null;
            recording = null;
            afterRewind = null;
            foreach (var renderer in hiddenRenderers)
                if (renderer)
                    renderer.forceRenderingOff = false;
            hiddenRenderers.Clear();
            if (fragments != null)
                foreach (var piece in fragments)
                    if (piece)
                        piece.gameObject.SetActive(false);
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            if (fragmentMesh)
                Destroy(fragmentMesh);
        }
    }
}
