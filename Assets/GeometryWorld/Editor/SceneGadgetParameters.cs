using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class SceneGadgetParameters
    {
        private static readonly Color MotionColor = new Color32(140, 220, 255, 255);

        internal static void DrawFields(GeometryLevel level, GameObject selected)
        {
            if (selected.TryGetComponent(out BallSpawn spawn))
            {
                if (selected.TryGetComponent(out PistonLauncher piston))
                    DrawPiston(piston, spawn);
                else
                {
                    EditorGUILayout.LabelField("Ball Release", EditorStyles.miniBoldLabel);
                    using var settings = new SerializedObject(spawn);
                    EditorGUILayout.PropertyField(settings.FindProperty(nameof(BallSpawn.diameter)));
                    EditorGUILayout.PropertyField(settings.FindProperty(nameof(BallSpawn.launchSpeed)));
                    EditorGUILayout.PropertyField(settings.FindProperty(nameof(BallSpawn.autoRelease)));
                    settings.ApplyModifiedProperties();
                }
            }

            if (selected.TryGetComponent(out ElasticGeometry elastic))
                DrawElastic(elastic);

            if (selected.TryGetComponent(out BouncePad pad))
            {
                EditorGUILayout.LabelField("Bounce Pad", EditorStyles.miniBoldLabel);
                float speed = DrawNumber("Speed (m/s)", pad.speed, 0.1f);
                Vector3 direction = GeometryBuildOverlay.DrawVector("Direction", pad.localDirection);
                if (speed != pad.speed || direction != pad.localDirection)
                {
                    Undo.RecordObject(pad, "Edit Bounce Pad");
                    pad.speed = speed;
                    pad.localDirection = direction;
                    RecordChange(pad);
                }
            }

            if (!selected.TryGetComponent(out PlatformMotion motion))
                return;
            EditorGUILayout.LabelField("Platform Motion", EditorStyles.miniBoldLabel);
            bool hasActions = false;
            foreach (var (sequence, action) in ActionsFor(level, motion))
            {
                hasActions = true;
                EditorGUILayout.LabelField($"{sequence.name} · {action.type}", EditorStyles.miniLabel);
                Vector3 amount = GeometryBuildOverlay.DrawVector(action.type == PuzzleActionType.Move ? "Travel" : "Turn (°)", action.amount);
                float duration = DrawNumber("Seconds", action.duration, 0f);
                if (amount != action.amount || duration != action.duration)
                {
                    Undo.RecordObject(sequence, "Edit Platform Action");
                    action.amount = amount;
                    action.duration = duration;
                    RecordChange(sequence);
                }
            }
            if (hasActions)
                return;

            Vector3 travel = GeometryBuildOverlay.DrawVector("Travel", motion.localTravel);
            Vector3 turn = GeometryBuildOverlay.DrawVector("Turn (°)", motion.localEulerTravel);
            float time = DrawNumber("Seconds", motion.duration, 0.1f);
            if (travel == motion.localTravel && turn == motion.localEulerTravel && time == motion.duration)
                return;
            Undo.RecordObject(motion, "Edit Platform Motion");
            motion.localTravel = travel;
            motion.localEulerTravel = turn;
            motion.duration = time;
            RecordChange(motion);
        }

        private static void DrawPiston(PistonLauncher piston, BallSpawn spawn)
        {
            EditorGUILayout.LabelField("Ball and Piston", EditorStyles.miniBoldLabel);
            float diameter = DrawNumber("Ball diameter", spawn.diameter, 0.05f);
            float speed = DrawNumber("Speed (m/s)", spawn.launchSpeed, 0f);
            float stroke = DrawNumber("Stroke", piston.stroke, 0.02f);
            float push = DrawNumber("Push (s)", piston.pushTime, 0.02f);
            float back = DrawNumber("Return (s)", piston.returnTime, 0.02f);
            if (diameter == spawn.diameter && speed == spawn.launchSpeed && stroke == piston.stroke &&
                push == piston.pushTime && back == piston.returnTime)
                return;
            Undo.RecordObject(spawn, "Edit Ball and Piston");
            Undo.RecordObject(piston, "Edit Ball and Piston");
            if (piston.HasVisuals)
                Undo.RecordObjects(new Object[] { piston.head, piston.housing }, "Edit Ball and Piston");
            if (piston.rod)
                Undo.RecordObject(piston.rod, "Edit Ball and Piston");
            spawn.diameter = diameter;
            spawn.launchSpeed = speed;
            piston.stroke = stroke;
            piston.pushTime = push;
            piston.returnTime = back;
            piston.AlignVisuals();
            if (piston.HasVisuals)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(piston.head);
                PrefabUtility.RecordPrefabInstancePropertyModifications(piston.housing);
            }
            if (piston.rod)
                PrefabUtility.RecordPrefabInstancePropertyModifications(piston.rod);
            RecordChange(piston);
            RecordChange(spawn);
        }

        private static IEnumerable<(PuzzleSequence sequence, PuzzleAction action)> ActionsFor(GeometryLevel level, PlatformMotion motion)
        {
            if (motion.automatic)
                yield break;
            foreach (var sequence in level.GetComponentsInChildren<PuzzleSequence>(true))
            {
                if (sequence.actions == null)
                    continue;
                foreach (var action in sequence.actions)
                    if (action != null && action.target == motion.gameObject &&
                        action.type is PuzzleActionType.Move or PuzzleActionType.Rotate)
                        yield return (sequence, action);
            }
        }

        private static float DrawNumber(string label, float value, float minimum)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(83f));
            EditorGUI.BeginChangeCheck();
            float entry = EditorGUILayout.FloatField(value, GUILayout.Width(70f));
            return EditorGUI.EndChangeCheck() && float.IsFinite(entry) ? Mathf.Max(minimum, entry) : value;
        }

        private static void RecordChange(Component component)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            SceneView.RepaintAll();
        }

        private static void DrawElastic(ElasticGeometry elastic)
        {
            EditorGUILayout.LabelField("Elastic Mechanism", EditorStyles.miniBoldLabel);
            using var settings = new SerializedObject(elastic);
            EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.draggable)));
            if (settings.FindProperty(nameof(ElasticGeometry.draggable)).boolValue)
            {
                EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.mode)));
                EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.localAxis)));
                bool rotates = settings.FindProperty(nameof(ElasticGeometry.mode)).enumValueIndex == (int)ElasticDragMode.Rotate;
                string unit = rotates ? "°" : "m";
                var range = settings.FindProperty(nameof(ElasticGeometry.maximumDisplacement));
                EditorGUILayout.PropertyField(range, new GUIContent($"Travel Each Way ({unit})"));
                var initial = settings.FindProperty(nameof(ElasticGeometry.initialOffset));
                EditorGUILayout.Slider(initial, -Mathf.Max(0.01f, range.floatValue), Mathf.Max(0.01f, range.floatValue),
                    new GUIContent($"Initial Offset ({unit})"));
                EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.returnOnRelease)));
                if (!rotates && !settings.FindProperty(nameof(ElasticGeometry.returnOnRelease)).boolValue)
                    EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.snapStep)), new GUIContent("Snap Step (m)"));
                EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.moveCollider)));
            }
            var bounce = settings.FindProperty(nameof(ElasticGeometry.bounceBall));
            EditorGUILayout.PropertyField(bounce);
            if (bounce.boolValue)
                EditorGUILayout.PropertyField(settings.FindProperty(nameof(ElasticGeometry.restitution)));
            settings.ApplyModifiedProperties();
        }

        internal static void DrawPreview(GeometryLevel level, GameObject selected)
        {
            if (selected.GetComponent<PuzzleTrigger>())
                ConstructionPreview.From(selected).Draw(selected.transform.localToWorldMatrix);

            if (selected.TryGetComponent(out ElasticGeometry elastic) && elastic.draggable && elastic.mode == ElasticDragMode.Translate)
            {
                Vector3 axis = elastic.localAxis.sqrMagnitude < 0.0001f ? Vector3.up : elastic.localAxis.normalized;
                Transform root = elastic.transform;
                Vector3 travel = root.TransformDirection(axis) * elastic.maximumDisplacement;
                Vector3 center = root.position;
                Vector3 start = center + root.TransformDirection(axis) * Mathf.Clamp(elastic.initialOffset,
                    -elastic.maximumDisplacement, elastic.maximumDisplacement);
                using var tint = new Handles.DrawingScope(MotionColor);
                Handles.DrawDottedLine(center - travel, center + travel, 4f);
                Handles.Label(start, "Initial rail position");
            }

            if (selected.TryGetComponent(out BouncePad pad))
            {
                Vector3 local = pad.localDirection.sqrMagnitude > 0.0001f ? pad.localDirection.normalized : Vector3.up;
                Vector3 top = Vector3.zero;
                if (ConstructionDimensions.TryBounds(selected, out Bounds bounds))
                    top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                DrawBounce(pad, local, top);
            }

            if (!selected.TryGetComponent(out PlatformMotion motion))
                return;
            var preview = ConstructionPreview.From(selected);
            bool hasActions = false;
            foreach (var (_, action) in ActionsFor(level, motion))
            {
                hasActions = true;
                Vector3 travel = action.type == PuzzleActionType.Move ? action.amount : Vector3.zero;
                Vector3 turn = action.type == PuzzleActionType.Rotate ? action.amount : Vector3.zero;
                DrawMotion(motion.transform, preview, travel, turn);
            }
            if (!hasActions)
                DrawMotion(motion.transform, preview, motion.localTravel, motion.localEulerTravel);
        }

        private static void DrawBounce(BouncePad pad, Vector3 localDirection, Vector3 localOrigin)
        {
            SceneGadgetTools.DrawArrow(pad.transform.TransformPoint(localOrigin), pad.transform.TransformDirection(localDirection),
                Mathf.Clamp(pad.speed * 0.25f, 0.15f, 20f), GeometryAssets.Pastels[3].Color);
        }

        private static void DrawMotion(Transform target, ConstructionPreview preview, Vector3 travel, Vector3 turn)
        {
            if (travel == Vector3.zero && turn == Vector3.zero)
                return;
            Matrix4x4 parent = target.parent ? target.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 start = target.localToWorldMatrix;
            Matrix4x4 end = parent * Matrix4x4.TRS(target.localPosition + travel,
                target.localRotation * Quaternion.Euler(turn), target.localScale);

            using var tint = new Handles.DrawingScope(MotionColor);
            if (travel != Vector3.zero)
            {
                Handles.DrawDottedLine(target.position, end.MultiplyPoint3x4(Vector3.zero), 4f);
                Handles.Label(target.position, "Start");
                Handles.Label(end.MultiplyPoint3x4(Vector3.zero), "End");
            }
            if (turn != Vector3.zero)
            {
                Vector3 pointer = Vector3.right * Mathf.Max(0.55f, preview.Bounds.extents.x);
                Vector3 last = start.MultiplyPoint3x4(pointer);
                const int steps = 24;
                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    var pose = parent * Matrix4x4.TRS(target.localPosition + travel * t,
                        target.localRotation * Quaternion.Euler(turn * t), target.localScale);
                    Vector3 next = pose.MultiplyPoint3x4(pointer);
                    Handles.DrawLine(last, next, 2f);
                    last = next;
                }
            }
            if (preview.Mesh)
            {
                Color ghost = MotionColor;
                ghost.a = 0.28f;
                ConstructionPreview.DrawSolid(preview.Mesh, end * preview.LocalMatrix, ghost);
            }
        }
    }
}
