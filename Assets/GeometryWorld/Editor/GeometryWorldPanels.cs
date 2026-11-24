using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BounceBack.GeometryWorld.Editor
{
    internal sealed partial class GeometryWorldWindow
    {
        private static readonly string[] PastelNames = GeometryAssets.Pastels.Select(preset => preset.Name).ToArray();
        [SerializeField] private float operationDistance = 0.25f;
        [SerializeField] private float inset = 0.22f;
        [SerializeField] private float faceScale = 1.2f;
        [SerializeField] private Atmosphere atmosphere;
        [SerializeField] private string materialName = "New Pastel";
        [SerializeField] private Color materialColor = new Color32(190, 181, 232, 255);
        [SerializeField] private float metallic;
        [SerializeField] private float smoothness = 0.32f;
        [SerializeField] private float opacity = 1f;
        private RenderTexture cameraPreview;

        private void DrawElementActions(GeometryObject geometry, ModelingMode mode)
        {
            switch (mode)
            {
                case ModelingMode.Vertex:
                    EditorGUILayout.LabelField($"{meshTool.Vertices.Count} selected · Shift-click to add or remove", EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(meshTool.Vertices.Count < 2))
                        if (GUILayout.Button("Merge at Center"))
                            meshTool.Edit(geometry, "Merge Geometry Vertices", topology => TopologyEdits.Merge(topology, meshTool.Vertices), true);
                    using (new EditorGUI.DisabledScope(meshTool.Vertices.Count == 0))
                        if (GUILayout.Button("Delete Vertices"))
                            meshTool.Edit(geometry, "Delete Geometry Vertices", topology => TopologyEdits.DeleteVertices(topology, meshTool.Vertices), true);
                    break;
                case ModelingMode.Edge:
                    var edge = meshTool.Edge;
                    EditorGUILayout.LabelField(edge.HasValue ? $"Edge {edge.Value.A + 1}–{edge.Value.B + 1}" : "Click an edge midpoint in Scene view.",
                        EditorStyles.miniLabel);
                    operationDistance = Mathf.Max(0.001f, EditorGUILayout.FloatField("Distance / bevel width", operationDistance));
                    using (new EditorGUI.DisabledScope(!edge.HasValue))
                    {
                        if (GUILayout.Button("Split Edge"))
                            meshTool.Edit(geometry, "Split Geometry Edge", topology => TopologyEdits.SplitEdge(topology, edge.Value), true);
                        if (GUILayout.Button(new GUIContent("Extrude Boundary Edge", "Extend an edge with a single attached face.")))
                            meshTool.Edit(geometry, "Extrude Boundary", topology => TopologyEdits.ExtrudeBoundary(topology, edge.Value, operationDistance), true);
                        if (GUILayout.Button(new GUIContent("Bevel Convex Edge", "Cut a narrow bevel on a closed convex solid.")))
                            meshTool.Edit(geometry, "Bevel Geometry Edge", topology => TopologyEdits.Bevel(topology, edge.Value, operationDistance, 0), true);
                    }
                    break;
                case ModelingMode.Face:
                    int face = meshTool.Face;
                    EditorGUILayout.LabelField(face < 0 ? "Click a face center in Scene view." : $"Face {face + 1}", EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(face < 0))
                    {
                        operationDistance = Mathf.Max(0.001f, EditorGUILayout.FloatField("Extrusion distance", operationDistance));
                        if (GUILayout.Button("Extrude Face"))
                            meshTool.Edit(geometry, "Extrude Geometry Face", topology => TopologyEdits.ExtrudeFace(topology, face, operationDistance));
                        inset = EditorGUILayout.Slider("Inset fraction", inset, 0.01f, 0.95f);
                        if (GUILayout.Button("Inset Convex Face"))
                            meshTool.Edit(geometry, "Inset Geometry Face", topology => TopologyEdits.InsetFace(topology, face, inset));
                        faceScale = Mathf.Max(0.01f, EditorGUILayout.FloatField("Scale factor", faceScale));
                        if (GUILayout.Button("Scale Face"))
                            meshTool.Edit(geometry, "Scale Geometry Face", topology => TopologyEdits.ScaleFace(topology, face, faceScale));
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            Material existing = face >= 0 ? geometry.model.materials[geometry.model.topology.faces[face].material] : null;
                            Material next = (Material)EditorGUILayout.ObjectField("Face material", existing, typeof(Material), false);
                            if (next && next != existing)
                                Run(() => meshTool.AssignFaceMaterial(geometry, next));
                            if (GUILayout.Button("Apply Pastel", GUILayout.Width(86)))
                                Run(() => meshTool.AssignFaceMaterial(geometry, GeometryAssets.Preset(preset)));
                        }
                        if (GUILayout.Button("Delete Face"))
                            meshTool.Edit(geometry, "Delete Geometry Face", topology => TopologyEdits.DeleteFace(topology, face), true);
                    }
                    break;
            }
        }

        private void DrawPuzzle(GeometryLevel level)
        {
            using var disabled = new EditorGUI.DisabledScope(!level);
            Title("Puzzle objects");
            puzzleItem = (PuzzleItem)EditorGUILayout.EnumPopup("Type", puzzleItem);
            placement = EditorGUILayout.Vector3Field("World placement", placement);
            using (new EditorGUI.DisabledScope(puzzleItem == PuzzleItem.CollisionEvent))
                if (GUILayout.Button("Create Puzzle Object"))
                    Run(() =>
                    {
                        var profile = new ConstructionProfile(puzzleItem.ToString(), puzzleItem, "");
                        var prefab = profile.Prefab;
                        GameObject obj = prefab ? profile.Place(level, ParentFor(level), prefab, placement, Quaternion.identity, true)
                            : LevelAuthoring.CreateItem(level, ParentFor(level), puzzleItem, placement);
                        Selection.activeGameObject = obj;
                        showInspector = true;
                    });

            bool attach = puzzleItem is PuzzleItem.BouncePad or PuzzleItem.Switch or PuzzleItem.ElasticObject or PuzzleItem.MovingPlatform
                or PuzzleItem.RotatingPlatform or PuzzleItem.CollisionEvent;
            using (new EditorGUI.DisabledScope(!attach || !Selected || !level || Selected == level.gameObject ||
                !Selected.transform.IsChildOf(level.transform)))
                if (GUILayout.Button("Attach Behavior to Selection"))
                    Run(() =>
                    {
                        var obj = SelectedModel ? SelectedModel.gameObject : Selected;
                        LevelAuthoring.AddBehavior(obj, puzzleItem, level, true);
                        chosenComponent = null;
                        showInspector = true;
                    });

            if (Selected && Selected.TryGetComponent(out SpaceConnection connection))
                EditorGUILayout.HelpBox(connection.activation == ConnectionActivation.CameraAligned
                    ? (connection.IsAligned() ? "The paired anchors align from the assigned camera." : "The paired anchors are outside the alignment tolerance.")
                    : "Manual mode uses the Enabled By Logic field or a Set Connection action.", MessageType.Info);
            if (Selected && Selected.TryGetComponent(out ElasticGeometry elastic) && GUILayout.Button("Preview Spring in Scene View"))
                ElasticMotionPreview.Start(elastic);
            DrawComponentInspector();
        }

        private void DrawLook(GeometryLevel level)
        {
            using var disabled = new EditorGUI.DisabledScope(!level);
            Title("Pastel materials");
            preset = EditorGUILayout.Popup("Preset", preset, PastelNames);
            Rect swatch = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(swatch, GeometryAssets.Pastels[preset].Color);
            if (GUILayout.Button("Use Preset for Custom Material"))
                materialColor = GeometryAssets.Pastels[preset].Color;
            materialName = EditorGUILayout.DelayedTextField("Name", materialName);
            materialColor = EditorGUILayout.ColorField("Color", materialColor);
            metallic = EditorGUILayout.Slider("Metallic", metallic, 0f, 1f);
            smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0f, 1f);
            opacity = EditorGUILayout.Slider("Opacity", opacity, 0.05f, 1f);
            if (GUILayout.Button("Save Material Asset"))
                Run(() =>
                {
                    string name = materialName.Trim();
                    if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        throw new InvalidOperationException("Use a valid material filename.");
                    selectedMaterial = GeometryAssets.CreateMaterial(name, materialColor, metallic, smoothness, opacity);
                    EditorGUIUtility.PingObject(selectedMaterial);
                    Report($"Saved {selectedMaterial.name} to the material library.");
                });
            selectedMaterial = (Material)EditorGUILayout.ObjectField("Preview / apply", selectedMaterial, typeof(Material), false);
            if (selectedMaterial)
            {
                var preview = AssetPreview.GetAssetPreview(selectedMaterial);
                Rect box = GUILayoutUtility.GetRect(60f, 80f, GUILayout.ExpandWidth(true));
                if (preview)
                    GUI.DrawTexture(box, preview, ScaleMode.ScaleToFit);
                else
                {
                    EditorGUI.DrawRect(box, selectedMaterial.HasProperty("_BaseColor") || selectedMaterial.HasProperty("_Color")
                        ? selectedMaterial.color : Color.gray);
                    Repaint();
                }
            }
            using (new EditorGUI.DisabledScope(!SelectedModel || SelectedModel.editLocked || !SelectedModel.surfaceRenderer))
                if (GUILayout.Button("Apply Material to Selected Object"))
                    Run(() =>
                    {
                        var renderer = SelectedModel.surfaceRenderer;
                        Undo.RecordObject(renderer, "Assign Object Material");
                        renderer.sharedMaterial = selectedMaterial ? selectedMaterial : GeometryAssets.Preset(preset);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    });

            Title("Lighting and camera");
            atmosphere = (Atmosphere)EditorGUILayout.EnumPopup("Atmosphere", atmosphere);
            if (GUILayout.Button("Apply Scene Atmosphere"))
                Run(() => LevelAuthoring.ApplyAtmosphere(level, atmosphere));
            using (new EditorGUI.DisabledScope(!level || !level.gameplayCamera))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Camera"))
                        Selection.activeGameObject = level.gameplayCamera.gameObject;
                    if (GUILayout.Button("Refresh Camera Preview"))
                        EditorApplication.delayCall += () => Run(() => RenderCameraPreview(level));
                }
                if (cameraPreview)
                {
                    Rect rect = GUILayoutUtility.GetAspectRect(16f / 9f, GUILayout.MaxHeight(260f));
                    GUI.DrawTexture(rect, cameraPreview, ScaleMode.ScaleToFit);
                }
            }

            Title("Adaptive audio");
            if (level && !level.audioDirector && GUILayout.Button("Create Audio Director"))
            {
                Selection.activeGameObject = LevelAuthoring.CreateItem(level, level.transform, PuzzleItem.Audio, placement);
                showInspector = true;
            }
            else if (level && level.audioDirector && GUILayout.Button("Select Audio Director"))
            {
                Selection.activeGameObject = level.audioDirector.gameObject;
                showInspector = true;
            }
            DrawComponentInspector();
        }

        private void RenderCameraPreview(GeometryLevel level)
        {
            if (!this || !level || !level.gameplayCamera || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!cameraPreview)
                cameraPreview = new RenderTexture(640, 360, 24) { hideFlags = HideFlags.HideAndDontSave };
            if (!cameraPreview.IsCreated() && !cameraPreview.Create())
                throw new InvalidOperationException("Unity could not allocate the camera preview texture.");
            var camera = level.gameplayCamera;
            var previousTarget = camera.targetTexture;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var request = new RenderPipeline.StandardRequest { destination = cameraPreview };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = cameraPreview;
                    camera.Render();
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                camera.targetTexture = previousTarget;
            }
        }

        private void ReleaseCameraPreview()
        {
            if (!cameraPreview)
                return;
            cameraPreview.Release();
            DestroyImmediate(cameraPreview);
        }

        private void DrawComponentInspector()
        {
            showInspector = EditorGUILayout.Foldout(showInspector, "Selected object inspector", true);
            if (!showInspector || !Selected)
                return;
            var settings = Selected.GetComponents<Component>().Where(component => component && component is not GeometryObject).ToArray();
            if (settings.Length == 0)
                return;
            int previous = Array.IndexOf(settings, chosenComponent);
            if (previous < 0)
                previous = settings.Length - 1;
            int index = EditorGUILayout.Popup("Component", previous, settings.Select(component => ObjectNames.NicifyVariableName(component.GetType().Name)).ToArray());
            chosenComponent = settings[index];
            UnityEditor.Editor.CreateCachedEditor(chosenComponent, null, ref inlineEditor);
            if (inlineEditor)
                inlineEditor.OnInspectorGUI();
        }

        private static GameObject[] SceneSelection(GeometryLevel level) =>
            Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.TopLevel)
                .Where(obj => obj && obj != level.gameObject && obj.transform.IsChildOf(level.transform) &&
                    !SceneVisibilityManager.instance.IsPickingDisabled(obj) &&
                    (!obj.TryGetComponent(out GeometryObject geometry) || !geometry.editLocked)).ToArray();

        private static void Group(GeometryLevel level)
        {
            var objects = SceneSelection(level);
            if (objects.Length == 0)
                return;
            Vector3 center = objects.Aggregate(Vector3.zero, (sum, obj) => sum + obj.transform.position) / objects.Length;
            Transform parent = objects.All(obj => obj.transform.parent == objects[0].transform.parent)
                ? objects[0].transform.parent : level.transform;
            var group = LevelAuthoring.NewObject("Geometry Group", parent, level.gameObject.scene, center, true);
            foreach (var obj in objects)
                Undo.SetTransformParent(obj.transform, group.transform, "Group Geometry");
            Selection.activeGameObject = group;
        }

        private void Reparent(GeometryLevel level)
        {
            Transform parent = newParent ? newParent.transform : level.transform;
            if (parent != level.transform && !parent.IsChildOf(level.transform))
                throw new InvalidOperationException("Choose a parent inside the current level.");
            foreach (var obj in SceneSelection(level))
                if (obj.transform == parent || parent.IsChildOf(obj.transform))
                    throw new InvalidOperationException("Parenting this selection would create a hierarchy cycle.");
            foreach (var obj in SceneSelection(level))
                Undo.SetTransformParent(obj.transform, parent, "Reparent Geometry");
        }

        private static void AlignPositions(GeometryLevel level)
        {
            var objects = SceneSelection(level);
            if (objects.Length < 2 || !objects.Contains(Selection.activeGameObject))
                return;
            Vector3 anchor = Selection.activeTransform.position;
            int axis = EditorUtility.DisplayDialogComplex("Align Geometry", "Align the selected objects to the active object on one axis.", "X", "Y", "Z");
            foreach (var obj in objects)
            {
                var transform = obj.transform;
                Undo.RecordObject(transform, "Align Geometry");
                Vector3 position = transform.position;
                position[axis] = anchor[axis];
                transform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            }
        }

        private static void DeleteSelection(GeometryLevel level)
        {
            foreach (var obj in SceneSelection(level))
                Undo.DestroyObjectImmediate(obj);
        }

        private static void NativeSceneCommand(GeometryLevel level, string command)
        {
            var scene = SceneView.lastActiveSceneView;
            if (!scene)
                throw new InvalidOperationException("Open a Scene view to use scene object clipboard commands.");
            if (command != "Paste")
            {
                var objects = SceneSelection(level);
                if (objects.Length == 0)
                    return;
                Selection.objects = objects;
            }
            var priorRoots = command == "Paste" ? level.gameObject.scene.GetRootGameObjects() : Array.Empty<GameObject>();
            scene.Focus();
            if (!EditorApplication.ExecuteMenuItem("Edit/" + command))
                scene.SendEvent(EditorGUIUtility.CommandEvent(command));
            if (command == "Paste")
            {
                EditorApplication.delayCall += () =>
                {
                    if (!level)
                        return;
                    foreach (var obj in level.gameObject.scene.GetRootGameObjects().Where(obj => obj != level.gameObject && !priorRoots.Contains(obj)))
                        Undo.SetTransformParent(obj.transform, level.transform, "Add Pasted Geometry to Level");
                };
            }
        }
    }
}
