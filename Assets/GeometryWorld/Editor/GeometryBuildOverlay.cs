using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    [Overlay(typeof(SceneView), OverlayId, "Geometry Build", true)]
    internal sealed class GeometryBuildOverlay : IMGUIOverlay, ITransientOverlay
    {
        internal const string OverlayId = "bounceback-geometry-build";
        private static readonly ConstructionProfile[] Shapes =
        {
            new("Block"), new("Platform"), new("Ramp"), new("Column"), new("Cylinder"), new("Sphere")
        };
        private static readonly ConstructionProfile[] Gadgets =
        {
            new("Goal", PuzzleItem.Goal, "Complete the level when the ball enters."),
            new("Bounce", PuzzleItem.BouncePad, "Launch the ball when it touches the pad."),
            new("Switch", PuzzleItem.Switch, "Run a sequence when the player uses the switch."),
            new("Move", PuzzleItem.MovingPlatform, "Move a platform back and forth."),
            new("Rotate", PuzzleItem.RotatingPlatform, "Rotate a platform."),
            new("Trigger", PuzzleItem.Trigger, "Run a sequence when the ball enters."),
            new("Slime", PuzzleItem.ElasticObject, "Bounces the ball and squashes on impact. Drag it during play or pulse it from a linked gadget."),
            new("Checkpt.", PuzzleItem.Checkpoint, "Change where the ball respawns."),
            new("Reset", PuzzleItem.ResetZone, "Reset the ball when it enters."),
            new("Anchor", PuzzleItem.Anchor, "Place an endpoint for a spatial connection.")
        };
        private static readonly string[] PaletteTabs = { "Shapes", "Gadgets" };
        private int tab;
        private bool showDetails;
        private Vector2 selectionScroll;

        public bool visible => SceneConstruction.Level;

        public override void OnGUI()
        {
            using var contents = new EditorGUILayout.VerticalScope(GUILayout.Width(264f));
            DrawNavigation();
            tab = GUILayout.Toolbar(tab, PaletteTabs, EditorStyles.miniButton);
            var palette = tab == 0 ? Shapes : Gadgets;
            for (int start = 0; start < palette.Length; start += 4)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                for (int index = start; index < Mathf.Min(start + 4, palette.Length); index++)
                    DrawProfile(palette[index]);
            }
            DrawWorkplane();

            var level = SceneConstruction.Level;
            var selected = Selection.activeGameObject;
            var owner = selected ? (Component)selected.GetComponentInParent<GeometryObject>() : null;
            if (!owner && selected)
                owner = selected.GetComponentInParent<PistonLauncher>();
            if (owner)
                selected = owner.gameObject;
            if (!level || !selected || selected == level.gameObject || !selected.transform.IsChildOf(level.transform))
                return;
            if (selected.TryGetComponent(out GeometryObject geometry) && geometry.editLocked)
                return;
            float height = Mathf.Clamp(containerWindow.position.height - (tab == 0 ? 200f : 230f), 60f, 285f);
            using var scroll = new EditorGUILayout.ScrollViewScope(selectionScroll, GUILayout.MaxHeight(height));
            selectionScroll = scroll.scrollPosition;
            DrawSelection(level, selected);
        }

        private static void DrawNavigation()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool moving = SceneConstruction.Grab && !SceneConstruction.Lift;
                bool lifting = SceneConstruction.Lift;
                bool grab = GUILayout.Toggle(moving,
                    new GUIContent("Grab", "Drag an object to move it. Hold Shift while dragging to lift it."),
                    EditorStyles.miniButtonLeft, GUILayout.Width(50f));
                if (grab != moving)
                    SceneConstruction.SetGrab(grab);

                bool lift = GUILayout.Toggle(lifting,
                    new GUIContent("Lift", "Drag away from or toward the workplane. With no workplane, drag vertically."),
                    EditorStyles.miniButtonMid, GUILayout.Width(46f));
                if (lift != lifting)
                    SceneConstruction.SetGrab(lift, vertical: true);

                EditorSnapSettings.gridSnapEnabled = GUILayout.Toggle(EditorSnapSettings.gridSnapEnabled,
                    new GUIContent("Grid Snap", "Use the Unity movement increments for placement and dragging."),
                    EditorStyles.miniButtonMid, GUILayout.Width(65f));
                SceneConstruction.StickToSurfaces = GUILayout.Toggle(SceneConstruction.StickToSurfaces,
                    new GUIContent("Surface", "Rest new shapes on what the pointer hits. Empty space uses Floor Y."),
                    EditorStyles.miniButtonRight, GUILayout.Width(65f));
            }
        }

        private static void DrawWorkplane()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Workplane", "Pick a visible face for placement. Shapes align to the face."),
                        EditorStyles.miniButtonLeft, GUILayout.Width(72f)))
                    SceneConstruction.PickWorkplane();
                using (new EditorGUI.DisabledScope(!SceneConstruction.UsesWorkplane && !SceneConstruction.PickingPlane))
                    if (GUILayout.Button(new GUIContent("Clear", "Return to surface and floor placement."),
                            EditorStyles.miniButtonRight, GUILayout.Width(40f)))
                        SceneConstruction.ResetWorkplane();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Floor Y", EditorStyles.miniLabel, GUILayout.Width(38f));
                using (new EditorGUI.DisabledScope(SceneConstruction.UsesWorkplane))
                    SceneConstruction.Floor = EditorGUILayout.DelayedFloatField(SceneConstruction.Floor, GUILayout.Width(43f));
            }
            string hint = SceneConstruction.PickingPlane ? "Click a face to set the workplane · Esc cancels" :
                SceneConstruction.AligningPiston ? "Click the top of a block · Esc cancels" :
                SceneConstruction.LinkingGadget ? "Click a platform or spring · Esc cancels" :
                SceneConstruction.RejectedPosition ? "No placement surface · Esc cancels" :
                SceneConstruction.Dragging ? "Release in the scene · Esc cancels" :
                SceneConstruction.Brush != null ? "Click to place · Esc cancels" :
                SceneConstruction.UsesWorkplane ? "Drag onto the active workplane" : "Drag an item into the scene";
            EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
        }

        private void DrawSelection(GeometryLevel level, GameObject selected)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                showDetails = EditorGUILayout.Foldout(showDetails, "Transform", true);
                if (selected.TryGetComponent(out PistonLauncher piston) &&
                    GUILayout.Button(new GUIContent("Snap to Block", "Click a block top to align the ball with the surface and its nearest axis."),
                        EditorStyles.miniButton, GUILayout.Width(92f)))
                    SceneConstruction.BeginPistonAlignment(piston);
                if (SceneGadgetTools.CanLink(selected) &&
                    GUILayout.Button(new GUIContent("Link Action", "Click a moving platform, rotating platform, or spring."),
                        EditorStyles.miniButton, GUILayout.Width(73f)))
                    SceneConstruction.BeginLink(selected);
                bool spareGoal = selected.TryGetComponent(out PuzzleTrigger goal) && goal.role == TriggerRole.Goal && level.goal != goal;
                if (spareGoal && GUILayout.Button("Use as Goal", EditorStyles.miniButton, GUILayout.Width(85f)))
                {
                    Undo.RecordObject(level, "Set Level Goal");
                    level.goal = goal;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(level);
                    SceneView.RepaintAll();
                }
            }
            if (ConstructionDimensions.TryMeasure(selected, out Vector3 dimensions))
            {
                Vector3 size = DrawVector(selected.GetComponent<PuzzleTrigger>() ? "Zone (m)" : "Size (m)", dimensions, "W", "H", "D");
                if (size != dimensions)
                    ConstructionDimensions.Resize(selected, size);
            }
            SceneGadgetParameters.DrawFields(level, selected);
            if (selected.TryGetComponent(out GeometryObject geometry) && GeometryUnitScale.CanNormalize(geometry) &&
                GUILayout.Button(new GUIContent("Normalize Scale", "Convert the mesh to a one-meter basis, keeping its current size and pivot."),
                    EditorStyles.miniButton))
                EditorApplication.delayCall += () =>
                {
                    if (geometry)
                        GeometryUnitScale.Normalize(geometry);
                };
            if (!showDetails)
                return;

            Transform target = selected.transform;
            Vector3 position = DrawVector("Position", target.position);
            Vector3 heading = DrawVector("Rotation", target.eulerAngles);
            if (position != target.position || heading != target.eulerAngles)
            {
                Undo.RecordObject(target, "Set Construction Transform");
                target.SetPositionAndRotation(position, Quaternion.Euler(heading));
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button(new GUIContent("Scale Handles", "Use Unity's native scale handles. Press W or E to move or rotate."),
                    EditorStyles.miniButton))
                SceneConstruction.UseScaleTool();
        }

        internal static Vector3 DrawVector(string label, Vector3 value, string x = "X", string y = "Y", string z = "Z")
        {
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(46f));
            value.x = DrawAxis(x, value.x);
            value.y = DrawAxis(y, value.y);
            value.z = DrawAxis(z, value.z);
            return value;
        }

        private static float DrawAxis(string label, float value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(10f));
            float entered = EditorGUILayout.FloatField(value, GUILayout.Width(48f));
            return float.IsFinite(entered) ? entered : value;
        }

        private static void DrawProfile(ConstructionProfile shape)
        {
            var content = new GUIContent(shape.Name, $"{shape.Hint} Drag it into the scene, or click and place it.");
            Rect rect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.Height(27f), GUILayout.Width(57f));
            int control = GUIUtility.GetControlID(FocusType.Keyboard, rect);
            var current = Event.current;
            bool hovered = rect.Contains(current.mousePosition);
            switch (current.GetTypeForControl(control))
            {
                case EventType.Repaint:
                    GUI.skin.button.Draw(rect, content, hovered, GUIUtility.hotControl == control,
                        SceneConstruction.Brush == shape, GUIUtility.keyboardControl == control);
                    break;
                case EventType.MouseDown when current.button == 0 && hovered:
                    GUIUtility.hotControl = GUIUtility.keyboardControl = control;
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    SceneConstruction.StartPaletteDrag(shape);
                    current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == control:
                    GUIUtility.hotControl = 0;
                    if (hovered)
                        SceneConstruction.SelectBrush(shape);
                    current.Use();
                    break;
                case EventType.KeyDown when GUIUtility.keyboardControl == control &&
                    current.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space:
                    SceneConstruction.SelectBrush(shape);
                    current.Use();
                    break;
            }
        }
    }
}
