using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    [InitializeOnLoad]
    internal static class SceneConstruction
    {
        internal const string DragKey = "BounceBack.GeometryWorld.ConstructionProfile";
        private const string PrefsPrefix = "BounceBack.GeometryWorld.Construction.";
        private static readonly Color Guide = new Color32(128, 239, 208, 255);
        private static ConstructionProfile brush;
        private static ConstructionProfile paletteDrag;
        private static GrabGesture gesture;
        private static SceneView previewView;
        private static ConstructionPreview previewShape;
        private static Bounds previewBounds;
        private static Matrix4x4 previewTransform;
        private static bool hasPreview;
        private static bool rejectedPosition;
        private static bool grab = EditorPrefs.GetBool(PrefsPrefix + "Grab", true);
        private static bool lift = EditorPrefs.GetBool(PrefsPrefix + "Lift", false);
        private static float floor = EditorPrefs.GetFloat(PrefsPrefix + "Floor", 0f);
        private static bool stickToSurfaces = EditorPrefs.GetBool(PrefsPrefix + "Surfaces", true);
        private static Tool previousTool;
        private static bool ownsNativeTool;
        private static ConstructionPlane? workplane;
        private static bool pickingPlane;
        private static GameObject linkSource;
        private static PistonLauncher aligningPiston;

        internal static event Action TakingControl;
        internal static bool MeshEditing { get; set; }
        internal static bool Grab => grab && brush == null && !MeshEditing && !pickingPlane && !linkSource && !aligningPiston;
        internal static bool Lift => Grab && lift;
        internal static ConstructionProfile Brush => brush;
        internal static bool PickingPlane => pickingPlane;
        internal static bool UsesWorkplane => workplane.HasValue;
        internal static bool LinkingGadget => linkSource;
        internal static bool AligningPiston => aligningPiston;
        internal static bool Dragging => paletteDrag != null;
        internal static bool RejectedPosition => rejectedPosition;
        internal static GeometryLevel Level => CanConstruct ? LevelAuthoring.FindLevel(SceneManager.GetActiveScene()) : null;
        private static bool CanConstruct => !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;

        internal static float Floor
        {
            get => floor;
            set
            {
                if (!float.IsFinite(value) || value == floor)
                    return;
                floor = value;
                EditorPrefs.SetFloat(PrefsPrefix + "Floor", value);
                Refresh();
            }
        }

        internal static bool StickToSurfaces
        {
            get => stickToSurfaces;
            set
            {
                if (value == stickToSurfaces)
                    return;
                stickToSurfaces = value;
                EditorPrefs.SetBool(PrefsPrefix + "Surfaces", value);
                Refresh();
            }
        }

        static SceneConstruction()
        {
            SceneView.duringSceneGui += DuringScene;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => ClearTools();
            EditorApplication.playModeStateChanged += _ => ClearTools();
            AssemblyReloadEvents.beforeAssemblyReload += ClearTools;
            AssemblyReloadEvents.beforeAssemblyReload += ConstructionPreview.ReleaseResources;
            Undo.undoRedoPerformed += CancelGrab;
        }

        [MenuItem("Tools/Build Geometry in Scene View")]
        internal static void Focus()
        {
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.Focus();
            if (view.in2DMode)
                view.in2DMode = false;
            view.overlayCanvas.overlaysEnabled = true;
            if (view.TryGetOverlay(GeometryBuildOverlay.OverlayId, out var overlay))
                overlay.displayed = true;
            var level = Level;
            if (!level)
                view.ShowNotification(new GUIContent("Open or create a Geometry World level first."));
            else
            {
                if (!level.GetComponentInChildren<GeometryObject>(true))
                    view.LookAt(level.transform.position, Quaternion.Euler(32f, -35f, 0f), 9f, false, true);
                SetGrab(true);
            }
        }

        internal static void SetGrab(bool value, bool vertical = false)
        {
            if (value)
                StartBuilding();
            grab = value;
            lift = value && vertical;
            EditorPrefs.SetBool(PrefsPrefix + "Grab", value);
            EditorPrefs.SetBool(PrefsPrefix + "Lift", lift);
            brush = null;
            paletteDrag = null;
            pickingPlane = false;
            linkSource = null;
            aligningPiston = null;
            hasPreview = false;
            rejectedPosition = false;
            CancelGrab();
            UpdateNativeTool();
            Refresh();
        }

        internal static void SelectBrush(ConstructionProfile shape)
        {
            StartBuilding();
            CancelGrab();
            pickingPlane = false;
            linkSource = null;
            aligningPiston = null;
            brush = brush == shape ? null : shape;
            paletteDrag = null;
            hasPreview = false;
            rejectedPosition = false;
            UpdateNativeTool();
            Refresh();
        }

        internal static void StartPaletteDrag(ConstructionProfile shape)
        {
            StartBuilding();
            CancelGrab();
            brush = null;
            paletteDrag = shape;
            pickingPlane = false;
            linkSource = null;
            aligningPiston = null;
            hasPreview = false;
            rejectedPosition = false;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(DragKey, shape);
            DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
            DragAndDrop.StartDrag(shape.Name);
            UpdateNativeTool();
            Refresh();
        }

        private static void StartBuilding() => TakingControl?.Invoke();

        internal static void PickWorkplane()
        {
            StartBuilding();
            ExitPlacement();
            CancelGrab();
            pickingPlane = true;
            UpdateNativeTool();
            Refresh();
        }

        internal static void ResetWorkplane()
        {
            pickingPlane = false;
            workplane = null;
            UpdateNativeTool();
            Refresh();
        }

        internal static void BeginLink(GameObject source)
        {
            StartBuilding();
            ExitPlacement();
            CancelGrab();
            linkSource = source;
            UpdateNativeTool();
            Refresh();
        }

        internal static void BeginPistonAlignment(PistonLauncher piston)
        {
            StartBuilding();
            ExitPlacement();
            CancelGrab();
            aligningPiston = piston;
            UpdateNativeTool();
            Refresh();
        }

        internal static void UseScaleTool()
        {
            StartBuilding();
            SetGrab(false);
            Tools.current = Tool.Scale;
        }

        internal static void ExitPlacement()
        {
            if (brush == null && paletteDrag == null && !hasPreview && !pickingPlane && !linkSource && !aligningPiston)
                return;
            brush = null;
            if (paletteDrag != null && ReferenceEquals(DragAndDrop.GetGenericData(DragKey), paletteDrag))
                DragAndDrop.SetGenericData(DragKey, null);
            paletteDrag = null;
            hasPreview = false;
            rejectedPosition = false;
            pickingPlane = false;
            linkSource = null;
            aligningPiston = null;
            UpdateNativeTool();
            Refresh();
        }

        private static void ClearTools()
        {
            CancelGrab();
            ExitPlacement();
            workplane = null;
            RestoreNativeTool();
            Refresh();
        }

        private static void Refresh() => SceneView.RepaintAll();

        private static void UpdateNativeTool()
        {
            if (MeshEditing || (!Grab && brush == null && !pickingPlane && !linkSource && !aligningPiston))
            {
                RestoreNativeTool();
                return;
            }
            if (ownsNativeTool)
                return;

            previousTool = Tools.current == Tool.None ? Tool.Move : Tools.current;
            Tools.current = Tool.None;
            ownsNativeTool = true;
        }

        private static void RestoreNativeTool()
        {
            if (!ownsNativeTool)
                return;
            if (Tools.current == Tool.None)
                Tools.current = previousTool;
            ownsNativeTool = false;
        }

        private static void DuringScene(SceneView view)
        {
            var current = Event.current;
            GeometryLevel level = Level;
            if (current == null || !level)
            {
                RestoreNativeTool();
                return;
            }

            if (ownsNativeTool && Tools.current != Tool.None && gesture == null)
                SetGrab(false);
            UpdateNativeTool();

            int control = GUIUtility.GetControlID("BounceBack.SceneConstruction".GetHashCode(), FocusType.Passive);
            if (current.type == EventType.Repaint)
            {
                if (workplane.HasValue)
                    workplane.Value.Draw(EditorSnapSettings.move);
                SceneGadgetTools.Draw(level, view);
            }
            if (gesture != null && gesture.View == view)
            {
                HandleGrab(view, current);
                return;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape &&
                (brush != null || paletteDrag != null || pickingPlane || linkSource || aligningPiston))
            {
                ExitPlacement();
                current.Use();
                return;
            }
            if (current.type == EventType.KeyDown && current.modifiers == EventModifiers.None &&
                current.keyCode is KeyCode.W or KeyCode.E or KeyCode.R or KeyCode.Q)
            {
                SetGrab(false);
                return;
            }
            if (current.type == EventType.DragExited || current.type == EventType.MouseLeaveWindow)
            {
                if (current.type == EventType.DragExited && paletteDrag != null)
                    ExitPlacement();
                if (previewView == view)
                {
                    hasPreview = false;
                    rejectedPosition = false;
                }
                view.Repaint();
                return;
            }

            if (pickingPlane || linkSource || aligningPiston)
            {
                HandleSetup(view, current, control, level);
                return;
            }

            bool externalDrag = current.type is EventType.DragUpdated or EventType.DragPerform;
            var stamp = externalDrag ? DragAndDrop.GetGenericData(DragKey) as ConstructionProfile : brush;
            if (externalDrag && stamp != paletteDrag)
                stamp = null;
            if (stamp != null)
            {
                DrawPlacement(view, current, control, level, stamp, externalDrag);
                return;
            }

            if (current.type == EventType.Repaint && hasPreview && previewView == view)
                DrawPreview();
            if (Grab && !current.alt && !current.shift && !EditorGUI.actionKey)
            {
                if (current.type == EventType.Layout)
                    HandleUtility.AddDefaultControl(control);
                if (current.type == EventType.MouseDown && current.button == 0 && HandleUtility.nearestControl == control)
                    BeginGrab(view, current, control, level);
            }
        }

        private static void HandleSetup(SceneView view, Event current, int control, GeometryLevel level)
        {
            if (current.alt)
                return;
            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);
            if (current.type != EventType.MouseDown || current.button != 0 || HandleUtility.nearestControl != control)
                return;

            if (pickingPlane)
            {
                if (HandleUtility.PlaceObject(current.mousePosition, out Vector3 hit, out Vector3 normal) &&
                    normal.sqrMagnitude > 0.01f)
                {
                    workplane = new ConstructionPlane(hit, normal);
                    ExitPlacement();
                }
                else
                    view.ShowNotification(new GUIContent("Click a visible surface to set the workplane."));
            }
            else if (aligningPiston)
            {
                var picked = HandleUtility.PickGameObject(current.mousePosition, false);
                var block = picked ? picked.GetComponentInParent<GeometryObject>() : null;
                if (block && block.transform.IsChildOf(level.transform) &&
                    !SceneVisibilityManager.instance.IsPickingDisabled(block.gameObject) &&
                    PistonAlignment.TryAlign(aligningPiston, block, HandleUtility.GUIPointToWorldRay(current.mousePosition)))
                {
                    ExitPlacement();
                    view.ShowNotification(new GUIContent("Piston aligned to block"));
                }
                else
                    view.ShowNotification(new GUIContent("Click the top of a solid block or platform."));
            }
            else
            {
                var picked = HandleUtility.PickGameObject(current.mousePosition, false);
                var geometry = picked ? picked.GetComponentInParent<GeometryObject>() : null;
                GameObject target = geometry ? geometry.gameObject : picked;
                string action = "Choose a moving platform, rotating platform, or spring.";
                if (target && target.transform.IsChildOf(level.transform) &&
                    !SceneVisibilityManager.instance.IsPickingDisabled(target) &&
                    SceneGadgetTools.TryLink(linkSource, target, out action))
                    ExitPlacement();
                view.ShowNotification(new GUIContent(action));
            }
            current.Use();
        }

        private static void DrawPlacement(SceneView view, Event current, int control, GeometryLevel level,
            ConstructionProfile stamp, bool viaDrag)
        {
            if (current.alt || Tools.viewToolActive)
            {
                if (previewView == view && hasPreview)
                {
                    hasPreview = false;
                    view.Repaint();
                }
                return;
            }
            view.wantsMouseMove = true;
            view.wantsMouseEnterLeaveWindow = true;
            if (!viaDrag && current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);
            if (current.type == EventType.Repaint)
            {
                if (hasPreview && previewView == view)
                    DrawPreview();
                return;
            }
            if (!viaDrag && current.type is not (EventType.MouseMove or EventType.MouseDown))
                return;

            Transform parent = GeometryWorldWindow.ParentFor(level);
            var prefab = stamp.Prefab;
            var preview = ConstructionPreview.From(prefab);
            Quaternion rotation = prefab ? prefab.transform.localRotation : Quaternion.identity;
            if (workplane.HasValue)
                rotation = Quaternion.Inverse(parent.rotation) * workplane.Value.Rotation * rotation;
            Vector3 scale = prefab ? prefab.transform.localScale : Vector3.one;
            var orientation = LeafMatrix(parent, Vector3.zero) * Matrix4x4.TRS(Vector3.zero, rotation, scale);
            Vector3 position = default;
            bool valid = preview.Mesh && TryPosition(current.mousePosition, preview.Bounds, Floor, orientation, out position);
            rejectedPosition = !valid;
            if (valid)
            {
                orientation.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
                ShowPreview(view, preview.Bounds, orientation, preview);
            }
            else
            {
                previewView = view;
                hasPreview = false;
                view.Repaint();
            }

            if (viaDrag)
                DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            bool commit = valid && (viaDrag ? current.type == EventType.DragPerform :
                current.type == EventType.MouseDown && current.button == 0 && HandleUtility.nearestControl == control);
            if (commit)
            {
                if (viaDrag)
                    DragAndDrop.AcceptDrag();
                ExitPlacement();
                EditorApplication.delayCall += () =>
                {
                    if (!view || !parent || !level || Level != level)
                        return;
                    try
                    {
                        Selection.activeGameObject = stamp.Place(level, parent, prefab, position, rotation, true);
                    }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException)
                    {
                        view.ShowNotification(new GUIContent(error.Message));
                    }
                };
            }
            else if (viaDrag && current.type == EventType.DragPerform)
                ExitPlacement();
            if (viaDrag || (current.type == EventType.MouseDown && current.button == 0 && HandleUtility.nearestControl == control))
                current.Use();
        }

        internal static void ShowPreview(SceneView view, Bounds bounds, Matrix4x4 matrix, ConstructionPreview shape = default)
        {
            if (hasPreview && previewView && previewView != view)
                previewView.Repaint();
            previewView = view;
            previewShape = shape;
            previewBounds = bounds;
            previewTransform = matrix;
            hasPreview = true;
            view.Repaint();
        }

        private static void DrawPreview()
        {
            if (previewShape.Mesh)
            {
                previewShape.Draw(previewTransform);
                return;
            }

            using var scope = new Handles.DrawingScope(Guide, previewTransform);
            var previousTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.DrawWireCube(previewBounds.center, previewBounds.size);
            Handles.zTest = previousTest;
        }

        internal static Matrix4x4 LeafMatrix(Transform parent, Vector3 position)
        {
            Matrix4x4 matrix = parent ? parent.localToWorldMatrix : Matrix4x4.identity;
            matrix.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
            return matrix;
        }

        internal static bool TryPosition(Vector2 mouse, Bounds bounds, Transform parent, float floorHeight, out Vector3 position)
            => TryPosition(mouse, bounds, floorHeight, LeafMatrix(parent, Vector3.zero), out position);

        private static bool TryPosition(Vector2 mouse, Bounds bounds, float floorHeight, Matrix4x4 orientation, out Vector3 position)
        {
            Vector3 point, normal;
            if (workplane.HasValue)
            {
                var ray = HandleUtility.GUIPointToWorldRay(mouse);
                if (!workplane.Value.Intersect(ray, out point))
                {
                    position = default;
                    return false;
                }
                point = workplane.Value.Snap(point, GridStep);
                position = RestingPosition(bounds, orientation, point, workplane.Value.Normal, Vector3.zero);
                return true;
            }
            if (!StickToSurfaces || !HandleUtility.PlaceObject(mouse, out point, out normal) || normal.sqrMagnitude < 0.001f)
            {
                var ray = HandleUtility.GUIPointToWorldRay(mouse);
                if (!TryHorizontalPoint(ray, floorHeight, out point))
                {
                    position = default;
                    return false;
                }
                normal = Vector3.up;
            }
            position = RestingPosition(bounds, orientation, point, normal, GridStep);
            return true;
        }

        internal static Vector3 RestingPosition(Bounds bounds, Matrix4x4 orientation, Vector3 point, Vector3 normal, Vector3 spacing)
        {
            normal = normal.normalized;
            Vector3 snapped = Round(point, spacing);
            point += Vector3.ProjectOnPlane(snapped - point, normal);
            Vector3 extent = bounds.extents;
            float reach = Mathf.Abs(Vector3.Dot(orientation.MultiplyVector(Vector3.right), normal)) * extent.x +
                Mathf.Abs(Vector3.Dot(orientation.MultiplyVector(Vector3.up), normal)) * extent.y +
                Mathf.Abs(Vector3.Dot(orientation.MultiplyVector(Vector3.forward), normal)) * extent.z;
            float center = Vector3.Dot(orientation.MultiplyVector(bounds.center), normal);
            return point + normal * (reach - center);
        }

        private static Vector3 GridStep => EditorSnapSettings.gridSnapEnabled ? EditorSnapSettings.move : Vector3.zero;

        internal static Vector3 Round(Vector3 point, Vector3 increment) => new(
            increment.x > 0f ? Mathf.Round(point.x / increment.x) * increment.x : point.x,
            increment.y > 0f ? Mathf.Round(point.y / increment.y) * increment.y : point.y,
            increment.z > 0f ? Mathf.Round(point.z / increment.z) * increment.z : point.z);

        internal static bool TryHorizontalPoint(Ray ray, float height, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            bool hit = plane.Raycast(ray, out float distance);
            point = hit ? ray.GetPoint(distance) : default;
            return hit;
        }

        private static void BeginGrab(SceneView view, Event current, int control, GeometryLevel level)
        {
            var picked = HandleUtility.PickGameObject(current.mousePosition, false);
            var model = picked ? picked.GetComponentInParent<GeometryObject>() : null;
            var marker = SceneGadgetTools.Pick(level, view, current.mousePosition);
            var piston = picked ? picked.GetComponentInParent<PistonLauncher>() : null;
            var target = marker ? marker : piston ? piston.gameObject : model ? model.gameObject : null;
            if (!target || !target.transform.IsChildOf(level.transform))
            {
                Selection.activeGameObject = picked;
                current.Use();
                return;
            }
            if ((!marker && model && model.editLocked) || SceneVisibilityManager.instance.IsHidden(target) ||
                SceneVisibilityManager.instance.IsPickingDisabled(target))
            {
                current.Use();
                return;
            }
            Selection.activeGameObject = target;
            gesture = new GrabGesture(view, target.transform, control, current.mousePosition, Lift, workplane);
            GUIUtility.hotControl = control;
            current.Use();
        }

        private static void HandleGrab(SceneView view, Event current)
        {
            if (!gesture.Target || GUIUtility.hotControl != gesture.Control ||
                current.type is EventType.Ignore or EventType.MouseLeaveWindow ||
                current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CancelGrab();
                if (current.type == EventType.KeyDown)
                    current.Use();
                return;
            }
            if (current.type == EventType.MouseDrag)
            {
                gesture.Update(current.mousePosition, Lift || current.shift, GridStep);
                view.Repaint();
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0)
            {
                var target = gesture.Target;
                Vector3 final = target.position;
                CancelGrab();
                if (final != target.position)
                {
                    Undo.RecordObject(target, "Drag Geometry");
                    target.position = final;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    Undo.FlushUndoRecordObjects();
                }
                view.Repaint();
                current.Use();
            }
        }

        private static void CancelGrab()
        {
            if (gesture == null)
                return;
            if (gesture.Target)
                gesture.Target.position = gesture.Start;
            if (GUIUtility.hotControl == gesture.Control)
                GUIUtility.hotControl = 0;
            gesture = null;
        }

        private sealed class GrabGesture
        {
            internal readonly SceneView View;
            internal readonly Transform Target;
            internal readonly int Control;
            internal readonly Vector3 Start;
            private readonly Vector2 pointerStart;
            private readonly Vector3 gridOrigin;
            private readonly Quaternion gridRotation;
            private readonly Vector3 up;
            private Vector2 pointerAnchor;
            private Vector3 positionAnchor;
            private Vector3 planeAnchor;
            private Plane movementPlane;
            private bool vertical;
            private bool moved;

            internal GrabGesture(SceneView view, Transform target, int control, Vector2 pointer, bool lift, ConstructionPlane? plane)
            {
                View = view;
                Target = target;
                Control = control;
                Start = target.position;
                pointerStart = pointer;
                gridOrigin = plane?.Origin ?? Vector3.zero;
                gridRotation = plane?.Rotation ?? Quaternion.identity;
                up = gridRotation * Vector3.up;
                vertical = lift;
                Anchor(pointer);
            }

            internal void Update(Vector2 pointer, bool raise, Vector3 spacing)
            {
                if (!moved && (pointer - pointerStart).sqrMagnitude < 9f)
                    return;
                moved = true;
                if (vertical != raise)
                {
                    vertical = raise;
                    Anchor(pointer);
                    return;
                }

                Vector3 position = positionAnchor;
                if (vertical)
                {
                    var camera = View.camera;
                    float distance = Vector3.Dot(positionAnchor - camera.transform.position, camera.transform.forward);
                    float height = camera.orthographic ? camera.orthographicSize * 2f :
                        Mathf.Max(0.1f, distance) * 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2f);
                    position += up * ((pointerAnchor.y - pointer.y) * height * EditorGUIUtility.pixelsPerPoint /
                        Mathf.Max(1f, camera.pixelHeight));
                }
                else
                {
                    var ray = HandleUtility.GUIPointToWorldRay(pointer);
                    if (!movementPlane.Raycast(ray, out float distance))
                        return;
                    position += Vector3.ProjectOnPlane(ray.GetPoint(distance) - planeAnchor, up);
                }
                var increment = vertical ? new Vector3(0f, spacing.y, 0f) : new Vector3(spacing.x, 0f, spacing.z);
                var local = Quaternion.Inverse(gridRotation) * (position - gridOrigin);
                Target.position = gridOrigin + gridRotation * Round(local, increment);
            }

            private void Anchor(Vector2 pointer)
            {
                positionAnchor = Target.position;
                pointerAnchor = pointer;
                var ray = HandleUtility.GUIPointToWorldRay(pointer);
                var parallel = new Plane(up, positionAnchor);
                if (Mathf.Abs(Vector3.Dot(ray.direction, up)) > 0.07f && parallel.Raycast(ray, out float planeDistance))
                {
                    movementPlane = parallel;
                    planeAnchor = ray.GetPoint(planeDistance);
                    return;
                }

                movementPlane = new Plane(View.camera.transform.forward, positionAnchor);
                planeAnchor = movementPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : positionAnchor;
            }
        }
    }
}
