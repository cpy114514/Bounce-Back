using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum PuzzleActionType
    {
        Wait, Move, Rotate, SetActive, ElasticPulse, SetConnection, PlayAudio, SetMaterial, SetColor, StartSequence
    }

    [Serializable]
    public sealed class PuzzleAction
    {
        public PuzzleActionType type;
        public GameObject target;
        public Vector3 amount;
        [Min(0f)] public float duration = 0.6f;
        public bool state = true;
        public Material material;
        public Color color = Color.white;
        public Cue cue;
        [Range(0f, 2f)] public float intensity = 1f;
    }

    public sealed class PuzzleSequence : MonoBehaviour
    {
        public bool oneShot;
        public List<PuzzleAction> actions = new();
        private bool running;
        private bool played;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Tint = Shader.PropertyToID("_Color");

        public void Run()
        {
            if (!isActiveAndEnabled || running || (oneShot && played))
                return;
            played = running = true;
            StartCoroutine(RunActions());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            running = false;
        }

        private IEnumerator RunActions()
        {
            foreach (PuzzleAction action in actions)
            {
                if (action == null)
                    continue;
                if (action.type == PuzzleActionType.Wait)
                {
                    if (action.duration > 0)
                        yield return new WaitForSeconds(action.duration);
                    continue;
                }
                if (!action.target)
                    continue;
                if (action.type == PuzzleActionType.Move || action.type == PuzzleActionType.Rotate)
                    yield return Animate(action);
                else
                    Apply(action);
            }
            running = false;
        }

        private static IEnumerator Animate(PuzzleAction action)
        {
            Transform target = action.target.transform;
            var body = action.target.GetComponent<Rigidbody>();
            var motion = action.target.GetComponent<PlatformMotion>();
            if (motion)
                motion.automatic = false;

            Vector3 start = target.localPosition;
            Quaternion rotation = target.localRotation;
            var step = new WaitForFixedUpdate();
            float duration = Mathf.Max(action.duration, Time.fixedDeltaTime);
            float elapsed = 0;
            while (elapsed < duration && target)
            {
                yield return step;
                if (!target)
                    yield break;
                elapsed += Time.fixedDeltaTime;
                float fraction = Mathf.Clamp01(elapsed / duration);
                float t = Mathf.SmoothStep(0f, 1f, fraction);
                Vector3 position = action.type == PuzzleActionType.Move ? start + action.amount * t : start;
                Quaternion heading = action.type == PuzzleActionType.Rotate ? rotation * Quaternion.Euler(action.amount * t) : rotation;
                PlatformMotion.SetPose(target, body, position, heading);
                if (motion && motion.audioDirector)
                    motion.audioDirector.ReportMovement(Mathf.Sin(fraction * Mathf.PI));
            }
            if (motion)
                motion.NotifyMovementCompleted();
        }

        private static void Apply(PuzzleAction action)
        {
            switch (action.type)
            {
                case PuzzleActionType.SetActive:
                    action.target.SetActive(action.state);
                    break;
                case PuzzleActionType.SetConnection:
                    if (action.target.TryGetComponent(out SpaceConnection connection))
                        connection.enabledByLogic = action.state;
                    break;
                case PuzzleActionType.ElasticPulse:
                    if (action.target.TryGetComponent(out ElasticGeometry elasticity))
                        elasticity.Pulse(action.intensity);
                    break;
                case PuzzleActionType.PlayAudio:
                    if (action.target.TryGetComponent(out AdaptivePuzzleAudio audio))
                        audio.Play(action.cue, action.intensity);
                    break;
                case PuzzleActionType.StartSequence:
                    if (action.target.TryGetComponent(out PuzzleSequence sequence))
                        sequence.Run();
                    break;
                case PuzzleActionType.SetMaterial:
                case PuzzleActionType.SetColor:
                    foreach (var renderer in action.target.GetComponentsInChildren<Renderer>(true))
                    {
                        if (action.type == PuzzleActionType.SetMaterial && action.material)
                            renderer.sharedMaterial = action.material;
                        else if (action.type == PuzzleActionType.SetColor)
                        {
                            var properties = new MaterialPropertyBlock();
                            renderer.GetPropertyBlock(properties);
                            properties.SetColor(BaseColor, action.color);
                            properties.SetColor(Tint, action.color);
                            renderer.SetPropertyBlock(properties);
                        }
                    }
                    break;
            }
        }
    }
}
