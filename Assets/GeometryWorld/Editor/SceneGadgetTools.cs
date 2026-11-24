using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class SceneGadgetTools
    {
        private static readonly List<BallSpawn> Spawns = new();
        private static readonly List<PuzzleTrigger> Zones = new();
        private static readonly List<SpatialAnchor> Anchors = new();
        private static readonly List<Marker> Markers = new();
        private static GUIStyle label;

        internal static int ZonePalette(TriggerRole role) => role switch
        {
            TriggerRole.Goal => 3,
            TriggerRole.Checkpoint => 1,
            TriggerRole.Reset => 5,
            _ => 2
        };

        internal static void DrawArrow(Vector3 origin, Vector3 direction, float length, Color color)
        {
            Vector3 tip = origin + direction * length;
            using var tint = new Handles.DrawingScope(color);
            Handles.DrawLine(origin, tip, 2f);
            float head = Mathf.Min(length * 0.35f, HandleUtility.GetHandleSize(tip) * 0.16f);
            Handles.ConeHandleCap(0, tip - direction * (head * 0.5f), Quaternion.LookRotation(direction), head, EventType.Repaint);
        }

        private readonly struct Marker
        {
            internal readonly Transform Transform;
            internal readonly string Name;
            internal readonly Color Color;
            internal readonly Vector3 Center;
            internal readonly Vector3 Size;
            internal readonly bool Sphere;

            internal Marker(Transform transform, string name, int palette, Vector3 center, Vector3 size, bool sphere = false)
            {
                Transform = transform;
                Name = name;
                Color = GeometryAssets.Pastels[palette].Color;
                Center = center;
                Size = size;
                Sphere = sphere;
            }

            internal Vector3 Position => Transform.TransformPoint(Center);
        }

        private static void Collect(GeometryLevel level)
        {
            Markers.Clear();
            level.GetComponentsInChildren(false, Spawns);
            level.GetComponentsInChildren(false, Zones);
            level.GetComponentsInChildren(false, Anchors);
            foreach (var spawn in Spawns)
                Markers.Add(new Marker(spawn.transform, level.spawn == spawn ? "Ball Start" : "Extra Ball Start", 1,
                    spawn.LocalStartPosition, Vector3.one * spawn.diameter, true));
            foreach (var zone in Zones)
            {
                if (!zone.TryGetComponent(out BoxCollider box))
                    continue;
                string name = zone.role switch
                {
                    TriggerRole.Goal => level.goal == zone ? "Goal" : "Spare Goal",
                    TriggerRole.Checkpoint => "Checkpoint",
                    TriggerRole.Reset => "Reset Zone",
                    _ => "Trigger"
                };
                Markers.Add(new Marker(zone.transform, name, ZonePalette(zone.role), box.center, box.size));
            }
            foreach (var anchor in Anchors)
                if (anchor.TryGetComponent(out SphereCollider sphere))
                    Markers.Add(new Marker(anchor.transform, "Anchor", 4, sphere.center,
                        Vector3.one * (sphere.radius * 2f), true));
        }

        internal static void Draw(GeometryLevel level, SceneView view)
        {
            label ??= new GUIStyle(EditorStyles.miniBoldLabel);
            Collect(level);
            var visibility = SceneVisibilityManager.instance;
            foreach (var marker in Markers)
            {
                Vector3 position = marker.Position;
                if (visibility.IsHidden(marker.Transform.gameObject) || view.camera.WorldToViewportPoint(position).z <= 0f)
                    continue;

                using (new Handles.DrawingScope(marker.Color, marker.Transform.localToWorldMatrix))
                {
                    if (marker.Sphere)
                    {
                        float radius = marker.Size.x * 0.5f;
                        Handles.DrawWireDisc(marker.Center, Vector3.right, radius);
                        Handles.DrawWireDisc(marker.Center, Vector3.up, radius);
                        Handles.DrawWireDisc(marker.Center, Vector3.forward, radius);
                    }
                    else
                        Handles.DrawWireCube(marker.Center, marker.Size);
                }

                using (new Handles.DrawingScope(marker.Color))
                    Handles.DotHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.045f, EventType.Repaint);
                label.normal.textColor = marker.Color;
                Handles.Label(position + view.camera.transform.up * (HandleUtility.GetHandleSize(position) * 0.13f), marker.Name, label);
            }

            var source = Selection.activeGameObject;
            if (!source || !source.transform.IsChildOf(level.transform) || visibility.IsHidden(source))
                return;
            var geometry = source.GetComponentInParent<GeometryObject>();
            if (geometry)
                source = geometry.gameObject;
            SceneGadgetParameters.DrawPreview(level, source);
            var sequence = AssignedSequence(source);
            if (!sequence || sequence.actions == null)
                return;
            using var linkColor = new Handles.DrawingScope(new Color(0.43f, 0.94f, 0.80f, 0.8f));
            foreach (var action in sequence.actions)
                if (action != null && action.target && !visibility.IsHidden(action.target))
                    Handles.DrawDottedLine(source.transform.position, action.target.transform.position, 5f);
        }

        internal static GameObject Pick(GeometryLevel level, SceneView view, Vector2 mouse)
        {
            Collect(level);
            var visibility = SceneVisibilityManager.instance;
            float nearest = 11f * 11f;
            GameObject result = null;
            foreach (var marker in Markers)
            {
                var obj = marker.Transform.gameObject;
                if (visibility.IsHidden(obj) || visibility.IsPickingDisabled(obj) ||
                    view.camera.WorldToViewportPoint(marker.Position).z <= 0f)
                    continue;
                float distance = (HandleUtility.WorldToGUIPoint(marker.Position) - mouse).sqrMagnitude;
                if (distance < nearest)
                {
                    result = obj;
                    nearest = distance;
                }
            }
            return result;
        }

        internal static bool CanLink(GameObject source) => source &&
            (source.GetComponent<PuzzleSwitch>() || source.GetComponent<PuzzleTrigger>());

        private static PuzzleSequence AssignedSequence(GameObject source)
        {
            if (source.TryGetComponent(out PuzzleSwitch triggerSwitch))
                return triggerSwitch.onInteract;
            return source.TryGetComponent(out PuzzleTrigger trigger) ? trigger.onEnter : null;
        }

        internal static bool TryLink(GameObject source, GameObject target, out string message)
        {
            message = "Choose a moving platform, rotating platform, or spring.";
            if (!CanLink(source) || !target || source == target)
                return false;
            if (target.TryGetComponent(out GeometryObject model) && model.editLocked)
            {
                message = "Unlock the target geometry before linking it.";
                return false;
            }

            var motion = target.GetComponent<PlatformMotion>();
            bool moves = motion && motion.localTravel.sqrMagnitude > 0f;
            bool rotates = motion && motion.localEulerTravel.sqrMagnitude > 0f;
            bool pulses = target.GetComponent<ElasticGeometry>();
            if (!moves && !rotates && !pulses)
                return false;

            var sequence = AssignedSequence(source);
            if (sequence && sequence.gameObject != source)
            {
                message = "This gadget uses a shared sequence. Edit that sequence in the Inspector.";
                return false;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            const string change = "Link Puzzle Gadget";
            Undo.SetCurrentGroupName(change);
            if (!sequence)
            {
                sequence = Undo.AddComponent<PuzzleSequence>(source);
                if (source.TryGetComponent(out PuzzleSwitch triggerSwitch))
                {
                    Undo.RecordObject(triggerSwitch, change);
                    triggerSwitch.onInteract = sequence;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(triggerSwitch);
                }
                else
                {
                    var trigger = source.GetComponent<PuzzleTrigger>();
                    Undo.RecordObject(trigger, change);
                    trigger.onEnter = sequence;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
                }
                Undo.RecordObject(sequence, change);
                sequence.oneShot = moves || rotates;
            }
            else
                Undo.RecordObject(sequence, change);

            sequence.actions ??= new List<PuzzleAction>();
            if (moves)
                AddAction(sequence, target, PuzzleActionType.Move, motion.localTravel, motion.duration);
            if (rotates)
                AddAction(sequence, target, PuzzleActionType.Rotate, motion.localEulerTravel, motion.duration);
            if (pulses)
                AddAction(sequence, target, PuzzleActionType.ElasticPulse, Vector3.zero, 0f);
            if (moves || rotates)
            {
                Undo.RecordObject(motion, change);
                motion.automatic = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(sequence);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            message = $"Linked to {target.name}";
            return true;
        }

        private static void AddAction(PuzzleSequence sequence, GameObject target, PuzzleActionType type, Vector3 amount, float duration)
        {
            var action = sequence.actions.Find(item => item != null && item.target == target && item.type == type);
            if (action == null)
                sequence.actions.Add(new PuzzleAction { type = type, target = target, amount = amount, duration = duration });
        }
    }
}
