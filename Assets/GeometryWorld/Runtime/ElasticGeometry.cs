using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public enum ElasticDragMode { Translate, Rotate }

    public sealed class ElasticGeometry : MonoBehaviour
    {
        public Transform visual;
        public bool draggable = true;
        public ElasticDragMode mode;
        public Vector3 localAxis = Vector3.up;
        [Min(0.01f)] public float maximumDisplacement = 1f;
        public float initialOffset;
        [Min(0f)] public float snapStep;
        [Min(0f)] public float stiffness = 54f;
        [Min(0f)] public float damping = 9f;
        [Range(0f, 1f)] public float overshoot = 0.3f;
        [Range(0f, 0.5f)] public float squashStretch = 0.16f;
        public bool returnOnRelease = true;
        public bool moveCollider;
        public bool scaleCollider;
        public bool bounceBall;
        [Range(0f, 1f)] public float restitution = 0.86f;
        public Cue impactCue = Cue.Bounce;
        public AdaptivePuzzleAudio audioDirector;
        public PuzzleSequence onReturn;

        public float CurrentOffset => offset;
        public bool Locked { get; private set; }
        public event Action<ElasticGeometry, BallBody> BallCompressed;
        public event Action<ElasticGeometry, BallBody> BallRebounded;
        [Range(0f, 0.30f)] public float chargeTime;
        [Min(0f)] public float springSpeed;
        [Range(0f, 1f)] public float tangentRetention = 1f;
        [Range(0f, 0.35f)] public float edgeBias = 0.08f;

        private Vector3 homePosition, homeVisualPosition, originalScale, homeVisualScale;
        private Quaternion homeRotation, homeVisualRotation;
        private Rigidbody body;
        private Plane dragPlane;
        private Vector3 dragOrigin;
        private Vector3 dragAxis;
        private float offset, velocity, target, dragStart;
        private bool held, dragMoved, animating;
        private const float ImpactDuration = 0.65f;
        private Bounds visualBounds;
        private float impactTime = ImpactDuration;
        private float impactAmount;
        private int impactAxis;
        private Vector3 impactAnchor;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            originalScale = transform.localScale;
            if (visual && visual != transform)
            {
                homeVisualPosition = visual.localPosition;
                homeVisualRotation = visual.localRotation;
                homeVisualScale = visual.localScale;
                if (visual.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                    visualBounds = filter.sharedMesh.bounds;
            }
            offset = target = Mathf.Clamp(initialOffset, -maximumDisplacement, maximumDisplacement);
            if (offset != 0f)
                ApplyPose();
        }

        private Vector3 Axis => localAxis.sqrMagnitude < 0.0001f ? Vector3.up : localAxis.normalized;

        public bool BeginDrag(Ray ray, Vector3 cameraForward)
        {
            if (!draggable || Locked)
                return false;
            dragPlane = new Plane(cameraForward, transform.position);
            if (!dragPlane.Raycast(ray, out float distance))
                return false;
            dragOrigin = ray.GetPoint(distance);
            Vector3 worldAxis = transform.TransformDirection(Axis);
            dragAxis = mode == ElasticDragMode.Translate ? Vector3.ProjectOnPlane(worldAxis, cameraForward)
                : Vector3.Cross(worldAxis, cameraForward).normalized;
            if (dragAxis.sqrMagnitude < 0.01f)
                return false;
            dragAxis /= dragAxis.sqrMagnitude;
            dragStart = offset;
            held = true;
            dragMoved = false;
            if (audioDirector)
                audioDirector.Play(Cue.Drag);
            return true;
        }

        public void Drag(Ray ray)
        {
            if (!held || !dragPlane.Raycast(ray, out float distance))
                return;
            float sensitivity = mode == ElasticDragMode.Rotate ? 65f : 1f;
            float displacement = Vector3.Dot(ray.GetPoint(distance) - dragOrigin, dragAxis) * sensitivity;
            if (!dragMoved && Mathf.Abs(displacement) < 0.001f)
                return;
            dragMoved = true;
            SetTargetOffset(dragStart + displacement);
        }

        public void SetTargetOffset(float displacement)
        {
            if (Locked)
                return;
            target = Mathf.Clamp(displacement, -maximumDisplacement, maximumDisplacement);
            animating = true;
        }

        internal void SetLocked(bool locked)
        {
            Locked = locked;
            if (!locked)
                return;
            held = animating = false;
            // Freeze the actual pose, including when Release is pressed during a drag.
            target = offset;
            velocity = 0f;
            if (body && moveCollider)
            {
                Transform parent = transform.parent;
                Vector3 local = homePosition + homeRotation * Axis * offset;
                body.position = parent ? parent.TransformPoint(local) : local;
            }
        }

        public void EndDrag()
        {
            if (!held)
                return;
            held = false;
            if (returnOnRelease)
                SetTargetOffset(0f);
            else if (dragMoved && mode == ElasticDragMode.Translate && snapStep > 0f)
            {
                float rounded = Mathf.Clamp(Mathf.Round(target / snapStep) * snapStep, -maximumDisplacement, maximumDisplacement);
                float end = Mathf.Sign(target) * maximumDisplacement;
                SetTargetOffset(Mathf.Abs(end - target) < Mathf.Abs(rounded - target) ? end : rounded);
            }
        }

        public void Pulse(float strength = 1f)
        {
            velocity += Mathf.Clamp(strength, -2f, 2f) * maximumDisplacement * 6f;
            animating = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!bounceBall || !collision.rigidbody || !collision.rigidbody.TryGetComponent(out BallBody ball) ||
                !ball.Released || ball.InTransition || collision.contactCount == 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 outward = -contact.normal;
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, outward));
            if (impactSpeed < 0.85f)
                return;

            Vector3 surfaceVelocity = body ? body.GetPointVelocity(contact.point) : Vector3.zero;
            Vector3 ballVelocity = ball.Body.linearVelocity;
            if (chargeTime > 0f)
            {
                Vector3 incoming = ball.IncomingVelocity;
                float normalSpeed = -Vector3.Dot(incoming, outward);
                if (normalSpeed < 0.85f)
                    return;
                Vector3 tangent = Vector3.ProjectOnPlane(incoming, outward);
                Vector3 radial = Vector3.ProjectOnPlane(contact.point - transform.position, outward);
                Vector3 rebound = tangent * tangentRetention + outward * (normalSpeed * restitution + springSpeed) +
                    Vector3.ClampMagnitude(radial, 0.65f) * (edgeBias * normalSpeed);
                float sink = Mathf.Min(0.22f, normalSpeed * 0.026f);
                if (ball.Compress(transform, outward, chargeTime, sink, rebound,
                    () => NotifyRebound(ball, normalSpeed)))
                {
                    ShowImpact(outward, normalSpeed);
                    impactAmount = sink / Mathf.Max(0.01f, Mathf.Abs(visualBounds.size[impactAxis] * homeVisualScale[impactAxis]));
                    BallCompressed?.Invoke(this, ball);
                    if (audioDirector)
                        audioDirector.Play(Cue.SlimeCompression);
                }
                return;
            }
            float alreadyOutgoing = Vector3.Dot(ballVelocity - surfaceVelocity, outward);
            float bounceSpeed = impactSpeed * Mathf.Clamp01(restitution);
            if (bounceSpeed > alreadyOutgoing)
                ball.Body.linearVelocity = ballVelocity + outward * (bounceSpeed - alreadyOutgoing);
            NotifyRebound(ball, impactSpeed);
            ShowImpact(outward, impactSpeed);
        }

        private void NotifyRebound(BallBody ball, float speed)
        {
            BallRebounded?.Invoke(this, ball);
            if (audioDirector && impactCue != Cue.Bounce)
                audioDirector.Play(impactCue, chargeTime > 0f ? Mathf.InverseLerp(1f, 11f, speed) : Mathf.Clamp01(speed / 5f));
        }

        private void ShowImpact(Vector3 outward, float impactSpeed)
        {
            if (visual && visual != transform)
            {
                Vector3 normal = visual.InverseTransformDirection(outward);
                impactAxis = Mathf.Abs(normal.x) > Mathf.Abs(normal.y) ? 0 : 1;
                if (Mathf.Abs(normal.z) > Mathf.Abs(normal[impactAxis]))
                    impactAxis = 2;
                impactAnchor = visualBounds.center;
                impactAnchor[impactAxis] = normal[impactAxis] >= 0f ? visualBounds.min[impactAxis] : visualBounds.max[impactAxis];
                impactAmount = Mathf.Clamp(impactSpeed * 0.07f, 0.12f, 0.38f);
                impactTime = 0f;
            }
        }

        private void FixedUpdate()
        {
            bool reacting = impactTime < ImpactDuration;
            if (reacting)
                impactTime += Time.fixedDeltaTime;
            if (!animating && !held)
            {
                if (reacting)
                    ApplyPose();
                return;
            }
            float dt = Time.fixedDeltaTime / 4f;
            for (int i = 0; i < 4; i++)
            {
                velocity += (Mathf.Clamp(stiffness, 1f, 400f) * (target - offset) -
                    Mathf.Clamp(damping, 0.1f, 60f) * velocity) * dt;
                float next = offset + velocity * dt;
                if (overshoot <= 0f && (offset - target) * (next - target) < 0f)
                {
                    next = target;
                    velocity = 0f;
                }
                float limit = maximumDisplacement * (1f + overshoot);
                offset = Mathf.Clamp(next, -limit, limit);
                if (next != offset)
                    velocity = 0f;
            }

            bool settled = !held && Mathf.Abs(offset - target) < 0.001f && Mathf.Abs(velocity) < 0.005f;
            if (settled)
            {
                animating = false;
                offset = target;
                velocity = 0f;
            }
            ApplyPose();
            if (audioDirector)
                audioDirector.ReportMovement(Mathf.Abs(velocity) / Mathf.Max(0.01f, maximumDisplacement * 8f));
            if (settled && onReturn)
                onReturn.Run();
        }

        private void ApplyPose()
        {
            Vector3 translation = mode == ElasticDragMode.Translate ? Axis * offset : Vector3.zero;
            Quaternion rotation = mode == ElasticDragMode.Rotate ? Quaternion.AngleAxis(offset, Axis) : Quaternion.identity;
            bool separateVisual = visual && visual != transform;
            if (moveCollider)
                PlatformMotion.SetPose(transform, body, homePosition + homeRotation * translation, homeRotation * rotation);
            float stretch = Mathf.Clamp01(Mathf.Abs(offset) / Mathf.Max(0.01f, maximumDisplacement)) * squashStretch;
            Vector3 scale = new(1f - stretch * 0.45f, 1f + stretch, 1f - stretch * 0.45f);
            if (scaleCollider && moveCollider)
                transform.localScale = Vector3.Scale(originalScale, scale);
            if (!separateVisual)
                return;

            Vector3 visualPosition = homeVisualPosition;
            Quaternion visualRotation = homeVisualRotation;
            if (!moveCollider)
            {
                visualPosition += transform.InverseTransformVector(transform.TransformDirection(translation));
                visualRotation = rotation * homeVisualRotation;
            }
            Vector3 baseScale = scaleCollider && moveCollider ? homeVisualScale : Vector3.Scale(homeVisualScale, scale);
            float compression = impactTime < ImpactDuration ? CalculateCompression() : 0f;
            Vector3 impactScale = Vector3.one * (1f + compression * 0.45f);
            impactScale[impactAxis] = 1f - compression;
            Vector3 visualScale = Vector3.Scale(baseScale, impactScale);
            // Keep the face opposite the impact fixed while the surface squashes.
            visualPosition += visualRotation * Vector3.Scale(impactAnchor, baseScale - visualScale);
            visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
            visual.localScale = visualScale;
        }

        private float CalculateCompression()
        {
            if (chargeTime > 0f && impactTime < chargeTime)
                return impactAmount * Mathf.Sin(Mathf.PI * impactTime / chargeTime);
            if (chargeTime > 0f)
                return 0f;
            float age = impactTime - chargeTime;
            return impactAmount * Mathf.Exp(-8f * age) * Mathf.Cos(22f * age);
        }
    }
}
