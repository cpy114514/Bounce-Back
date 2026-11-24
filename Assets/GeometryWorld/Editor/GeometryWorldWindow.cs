using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BounceBack.Geometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BounceBack.GeometryWorld.Editor
{
    internal sealed partial class GeometryWorldWindow : EditorWindow
    {
        private enum Panel { Level, Create, Mesh, Puzzle, Look }

        [SerializeField] private Panel panel;
        [SerializeField] private GeometryShape shape = GeometryShape.Platform;
        [SerializeField] private Vector3 dimensions = new(3f, 0.4f, 2f);
        [SerializeField] private int segments = 16;
        [SerializeField] private float arc = 110f;
        [SerializeField] private Vector3 placement;
        [SerializeField] private bool decorative;
        [SerializeField] private bool generateCollider = true;
        [SerializeField] private GeometryDrawing drawing;
        [SerializeField] private DrawingConversion conversion;
        [SerializeField] private float depth = 0.35f;
        [SerializeField] private float beamWidth = 1f;
        [SerializeField] private bool useStrokeWidths = true;
        [SerializeField] private int preset = 2;
        [SerializeField] private List<Vector2> customPolygon = new() { new(0, 0), new(2, 0), new(1.2f, 2) };
        [SerializeField] private PuzzleItem puzzleItem;
        [SerializeField] private float meshSnap;
        [SerializeField] private bool symmetry;

        private readonly SceneModelingTool meshTool = new();
        private ToolbarToggle[] panelTabs;
        private Label heading, statusLine;
        private Vector2 hierarchyScroll, detailsScroll;
        private string status = "";
        private MessageType statusType;
        private List<string> validationIssues;
        private bool showInspector;
        private bool showShapeLibrary;
        private Material selectedMaterial;
        private GameObject selectedPrefab;
        private GeometryMeshAsset selectedModel;
        private GameObject newParent;
        private UnityEditor.Editor inlineEditor;
        private Component chosenComponent;
        private bool ownsToolVisibility;
        private bool previousToolVisibility;

        [MenuItem("Tools/Geometry World Editor")]
        private static void OpenWindow() => GetWindow<GeometryWorldWindow>();

        private GeometryLevel CurrentLevel => LevelAuthoring.FindLevel(SceneManager.GetActiveScene());
        private GameObject Selected => Selection.activeGameObject;
        private GeometryObject SelectedModel => Selected ? Selected.GetComponentInParent<GeometryObject>() : null;

        private void OnEnable()
        {
            titleContent = new GUIContent("Geometry World Editor");
            minSize = new Vector2(705f, 440f);
            Selection.selectionChanged += SelectionChanged;
            Undo.undoRedoPerformed += UndoChanged;
            SceneView.duringSceneGui += DuringSceneView;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.hierarchyChanged += Repaint;
            meshTool.Changed += RepaintSceneAndWindow;
            meshTool.Feedback += ReportError;
            SceneConstruction.TakingControl += SwitchToObjectMode;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
            Undo.undoRedoPerformed -= UndoChanged;
            SceneView.duringSceneGui -= DuringSceneView;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.hierarchyChanged -= Repaint;
            meshTool.Changed -= RepaintSceneAndWindow;
            meshTool.Feedback -= ReportError;
            SceneConstruction.TakingControl -= SwitchToObjectMode;
            SceneConstruction.MeshEditing = false;
            SetToolVisibility(false);
            if (inlineEditor)
                DestroyImmediate(inlineEditor);
            ReleaseCameraPreview();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            var accent = new Color(0.66f, 0.59f, 0.97f);
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 43;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new Color(0.28f, 0.26f, 0.37f);
            header.style.backgroundColor = new Color(0.16f, 0.17f, 0.23f);
            heading = new Label("Geometry World Editor");
            heading.style.flexGrow = 1;
            heading.style.color = new Color(0.96f, 0.94f, 1f);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 14;
            header.Add(heading);
            header.Add(HeaderButton("Build in Scene", "Open the Scene view with the Geometry Build palette.",
                SceneConstruction.Focus));
            header.Add(HeaderButton("Drawing Editor", "Open the existing 2D drawing editor.", OpenDrawing));
            header.Add(HeaderButton("Save", "Save the level scene and its model assets.",
                () => WithLevel(SaveLevel)));
            var play = HeaderButton("Playtest / Stop", "Start or stop a temporary playtest.", TogglePlaytest);
            play.style.borderBottomColor = accent;
            play.style.borderBottomWidth = 2;
            header.Add(play);
            root.Add(header);

            var tabs = new Toolbar();
            panelTabs = new ToolbarToggle[Enum.GetValues(typeof(Panel)).Length];
            for (int index = 0; index < panelTabs.Length; index++)
            {
                Panel selected = (Panel)index;
                var tab = new ToolbarToggle { text = selected.ToString(), tooltip = PanelTooltip(selected) };
                panelTabs[index] = tab;
                tab.RegisterValueChangedCallback(_ => SelectPanel(selected));
                tabs.Add(tab);
            }
            root.Add(tabs);
            SelectPanel(panel);

            var body = new IMGUIContainer(DrawBody);
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            root.Add(body);
            statusLine = new Label();
            statusLine.style.paddingLeft = 8;
            statusLine.style.paddingRight = 8;
            statusLine.style.minHeight = 20;
            statusLine.style.maxHeight = 20;
            statusLine.style.overflow = Overflow.Hidden;
            statusLine.style.textOverflow = TextOverflow.Ellipsis;
            root.Add(statusLine);
            UpdateStatus();
        }

        private static Button HeaderButton(string title, string tooltip, Action action)
        {
            var button = new Button(action) { text = title, tooltip = tooltip };
            button.style.marginLeft = 3;
            button.style.minHeight = 25;
            return button;
        }

        private static string PanelTooltip(Panel tab) => tab switch
        {
            Panel.Level => "Create, save, validate and test the level scene.",
            Panel.Create => "Create models, convert 2D artwork, or place library assets.",
            Panel.Mesh => "Edit vertices, edges, faces and surface materials.",
            Panel.Puzzle => "Place and configure ball physics, triggers, connections and sequences.",
            Panel.Look => "Configure pastel materials, camera, lighting and audio.",
            _ => ""
        };

        private void SelectPanel(Panel choice)
        {
            panel = choice;
            detailsScroll = Vector2.zero;
            if (panelTabs != null)
                for (int index = 0; index < panelTabs.Length; index++)
                    panelTabs[index].SetValueWithoutNotify(index == (int)choice);
            if (choice != Panel.Mesh)
                meshTool.SelectMode(ModelingMode.Object);
            Repaint();
        }

        private void DrawBody()
        {
            var level = CurrentLevel;
            if (heading != null)
                heading.text = level ? level.levelTitle : "Geometry World Editor";
            using var row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Clamp(position.width * 0.31f, 198f, 330f))))
                DrawHierarchy(level);
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                EditorGUILayout.Space(6);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode && panel != Panel.Level))
                    switch (panel)
                    {
                        case Panel.Level: DrawLevel(level); break;
                        case Panel.Create: DrawCreate(level); break;
                        case Panel.Mesh: DrawModel(); break;
                        case Panel.Puzzle: DrawPuzzle(level); break;
                        case Panel.Look: DrawLook(level); break;
                    }
                EditorGUILayout.Space(8);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHierarchy(GeometryLevel level)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Level hierarchy", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!level || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Move", EditorStyles.miniButtonLeft))
                        SetNativeTool(Tool.Move);
                    if (GUILayout.Button("Rotate", EditorStyles.miniButtonMid))
                        SetNativeTool(Tool.Rotate);
                    if (GUILayout.Button("Scale", EditorStyles.miniButtonRight))
                        SetNativeTool(Tool.Scale);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tools.pivotRotation.ToString(), EditorStyles.miniButtonLeft))
                        Tools.pivotRotation = Tools.pivotRotation == PivotRotation.Local ? PivotRotation.Global : PivotRotation.Local;
                    EditorSnapSettings.gridSnapEnabled = GUILayout.Toggle(EditorSnapSettings.gridSnapEnabled,
                        new GUIContent("Grid Snap", "Toggle Unity Scene view grid snapping."), EditorStyles.miniButtonRight);
                }
            }
            hierarchyScroll = EditorGUILayout.BeginScrollView(hierarchyScroll, GUILayout.ExpandHeight(true));
            if (level)
                ObjectRow(level.transform, 0);
            else
                EditorGUILayout.HelpBox("Create or open a Geometry World level.", MessageType.Info);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(!level || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Group", EditorStyles.miniButtonLeft))
                        Run(() => Group(level));
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid))
                        RunLater(() => NativeSceneCommand(level, "Duplicate"));
                    if (GUILayout.Button("Delete", EditorStyles.miniButtonRight))
                        DeleteSelection(level);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy", EditorStyles.miniButtonLeft))
                        RunLater(() => NativeSceneCommand(level, "Copy"));
                    if (GUILayout.Button("Paste", EditorStyles.miniButtonMid))
                        RunLater(() => NativeSceneCommand(level, "Paste"));
                    if (GUILayout.Button("Align", EditorStyles.miniButtonRight))
                        RunLater(() => AlignPositions(level));
                }
                newParent = (GameObject)EditorGUILayout.ObjectField("Parent", newParent, typeof(GameObject), true);
                if (GUILayout.Button("Reparent selection"))
                    Run(() => Reparent(level));
            }
        }

        private void ObjectRow(Transform transform, int depth)
        {
            if (depth > 40)
                return;
            GameObject obj = transform.gameObject;
            if (transform.parent && transform.parent.TryGetComponent(out GeometryObject parentGeometry) &&
                parentGeometry.surface && parentGeometry.surface.transform == transform)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(Mathf.Min(depth, 9) * 9f);
                bool selected = Selection.Contains(obj);
                bool hidden = SceneVisibilityManager.instance.IsHidden(obj);
                bool picking = SceneVisibilityManager.instance.IsPickingDisabled(obj);
                var content = new GUIContent(obj.name, ObjectNames.GetClassName(obj));
                if (GUILayout.Toggle(selected, content, EditorStyles.miniButton, GUILayout.MinWidth(28)) != selected)
                    SelectHierarchyObject(obj, EditorGUI.actionKey || Event.current.shift);
                if (GUILayout.Button(new GUIContent(hidden ? "–" : "●", hidden ? "Show in Scene view" : "Hide in Scene view"),
                        EditorStyles.miniButton, GUILayout.Width(21)))
                    SceneVisibilityManager.instance.ToggleVisibility(obj, true);
                if (GUILayout.Button(new GUIContent(picking ? "■" : "□", picking ? "Unlock selection" : "Lock selection"),
                        EditorStyles.miniButton, GUILayout.Width(21)))
                {
                    SceneVisibilityManager.instance.TogglePicking(obj, true);
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && obj.TryGetComponent(out GeometryObject model))
                    {
                        Undo.RecordObject(model, "Set Geometry Edit Lock");
                        model.editLocked = !picking;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                    }
                }
            }
            foreach (Transform child in transform)
                ObjectRow(child, depth + 1);
        }

        private static void SelectHierarchyObject(GameObject obj, bool additive)
        {
            if (!additive)
            {
                Selection.activeGameObject = obj;
                return;
            }
            var selection = Selection.objects.ToList();
            if (!selection.Remove(obj))
                selection.Insert(0, obj);
            Selection.objects = selection.ToArray();
        }

        private void DrawLevel(GeometryLevel level)
        {
            Title("Level scene");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create Level"))
                        RunLater(CreateLevel);
                    if (GUILayout.Button("Open Level"))
                        RunLater(OpenLevel);
                    if (GUILayout.Button("Open Demo"))
                        RunLater(GeometryWorldDemo.OpenOrCreate);
                }
                if (level)
                {
                    EditorGUILayout.LabelField(level.gameObject.scene.path, EditorStyles.miniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Save"))
                            RunLater(() => SaveLevel(level));
                        if (GUILayout.Button("Save As"))
                            RunLater(() => SaveLevelCopy(level, false));
                        if (GUILayout.Button("Duplicate Level"))
                            RunLater(() => SaveLevelCopy(level, true));
                    }
                    DrawLevelMetadata(level);
                    if (GUILayout.Button("Validate Level"))
                        ValidateLevel(level);
                    if (validationIssues != null)
                    {
                        foreach (string issue in validationIssues)
                            EditorGUILayout.HelpBox(issue, MessageType.Warning);
                        if (validationIssues.Count == 0)
                            EditorGUILayout.HelpBox("Scene references and gameplay setup passed validation.", MessageType.Info);
                    }
                }
            }

            if (!level)
                return;
            if (!EditorApplication.isPlaying && GUILayout.Button(level.StarterPiston && level.StarterPiston.HasVisuals
                    ? "Select Ball and Piston" : "Set Up Ball and Piston"))
                RunLater(() => LevelStarterAuthoring.Select(level));
            Title("Playtest");
            if (GUILayout.Button(EditorApplication.isPlaying ? "Stop and Return to Editing" : "Save and Playtest"))
                RunLater(TogglePlaytest);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                using var controls = new EditorGUILayout.HorizontalScope();
                if (GUILayout.Button(level.StarterPiston ? "Push Ball" : "Release Ball"))
                    level.ReleaseBall();
                if (GUILayout.Button("Reset Ball"))
                    level.ResetBall();
                if (GUILayout.Button("Restart Level"))
                    RunLater(level.Restart);
            }
            if (EditorApplication.isPlaying && Selected && Selected.TryGetComponent(out ElasticGeometry elastic) &&
                GUILayout.Button("Pulse Selected Elastic Object"))
                elastic.Pulse();
        }

        private static void DrawLevelMetadata(GeometryLevel level)
        {
            var serialized = new SerializedObject(level);
            serialized.Update();
            foreach (string property in new[] { "levelTitle", "designerNotes", "playHint", "gameplayCamera", "spawn", "goal", "audioDirector", "fallHeight", "showPlayHud" })
                EditorGUILayout.PropertyField(serialized.FindProperty(property), true);
            serialized.ApplyModifiedProperties();
        }

        private void DrawCreate(GeometryLevel level)
        {
            if (GUILayout.Button("Build in Scene View", GUILayout.Height(26)))
                RunLater(SceneConstruction.Focus);
            using var disabled = new EditorGUI.DisabledScope(!level);
            Title("Geometry");
            placement = EditorGUILayout.Vector3Field("World placement", placement);
            if (GUILayout.Button("Use Scene View Pivot") && SceneView.lastActiveSceneView)
                placement = SceneView.lastActiveSceneView.pivot;
            decorative = EditorGUILayout.Toggle("Decorative", decorative);
            using (new EditorGUI.DisabledScope(decorative))
                generateCollider = EditorGUILayout.Toggle("Generate collider", generateCollider);
            selectedMaterial = (Material)EditorGUILayout.ObjectField("Material override", selectedMaterial, typeof(Material), false);
            preset = EditorGUILayout.Popup("Pastel preset", preset, PastelNames);
            shape = (GeometryShape)EditorGUILayout.EnumPopup("Shape", shape);
            if (shape == GeometryShape.CustomPolygon)
            {
                dimensions.y = EditorGUILayout.FloatField("Extrusion depth", dimensions.y);
            }
            else if (shape == GeometryShape.Plane)
            {
                Vector2 area = EditorGUILayout.Vector2Field("Width / length", new Vector2(dimensions.x, dimensions.z));
                dimensions.x = area.x;
                dimensions.z = area.y;
            }
            else
            {
                dimensions = EditorGUILayout.Vector3Field("Dimensions", dimensions);
            }
            if (shape is GeometryShape.Sphere or GeometryShape.Cylinder or GeometryShape.Cone or GeometryShape.Prism or
                GeometryShape.Archway or GeometryShape.Column or GeometryShape.Stairs or GeometryShape.Tower or
                GeometryShape.CircularPlatform or GeometryShape.CurvedPath)
                segments = EditorGUILayout.IntSlider("Segments", segments, 3, 64);
            if (shape == GeometryShape.CurvedPath)
                arc = EditorGUILayout.Slider("Sweep", arc, 15f, 330f);
            if (shape == GeometryShape.CustomPolygon)
            {
                for (int index = 0; index < customPolygon.Count; index++)
                {
                    using var point = new EditorGUILayout.HorizontalScope();
                    customPolygon[index] = EditorGUILayout.Vector2Field($"Vertex {index + 1}", customPolygon[index]);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        customPolygon.RemoveAt(index);
                        break;
                    }
                }
                if (GUILayout.Button("Add Polygon Vertex"))
                    customPolygon.Add(Vector2.zero);
            }
            if (GUILayout.Button("Create Editable Model"))
                RunLater(() =>
                {
                    var topology = GeometryGenerators.Generate(shape, dimensions, segments, arc, customPolygon);
                    var model = GeometryAssets.CreateModel(ObjectNames.NicifyVariableName(shape.ToString()), topology,
                        new[] { selectedMaterial ? selectedMaterial : GeometryAssets.Preset(preset) });
                    PlaceModel(level, model, placement);
                });

            Title("From 2D drawing");
            drawing = (GeometryDrawing)EditorGUILayout.ObjectField("Drawing asset", drawing, typeof(GeometryDrawing), false);
            if (GUILayout.Button("Open in Drawing Editor"))
                OpenDrawing();
            conversion = (DrawingConversion)EditorGUILayout.EnumPopup("Conversion", conversion);
            if (conversion != DrawingConversion.Surface)
                depth = Mathf.Max(0.01f, EditorGUILayout.FloatField("Depth", depth));
            if (conversion == DrawingConversion.Beams)
            {
                useStrokeWidths = EditorGUILayout.Toggle("Use drawing widths", useStrokeWidths);
                beamWidth = Mathf.Max(0.01f, EditorGUILayout.FloatField(useStrokeWidths ? "Width multiplier" : "Beam width", beamWidth));
            }
            using (new EditorGUI.DisabledScope(!drawing))
                if (GUILayout.Button("Convert to Editable Model"))
                    RunLater(() => ConvertDrawing(level));

            Title("Reusable library");
            selectedModel = (GeometryMeshAsset)EditorGUILayout.ObjectField("Model", selectedModel, typeof(GeometryMeshAsset), false);
            using (new EditorGUI.DisabledScope(!selectedModel))
                if (GUILayout.Button("Place Linked Model"))
                    Run(() => PlaceModel(level, selectedModel, placement));
            selectedPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", selectedPrefab, typeof(GameObject), false);
            using (new EditorGUI.DisabledScope(!selectedPrefab))
                if (GUILayout.Button("Place Prefab"))
                    Run(() => Selection.activeGameObject = GeometryAssets.PlacePrefab(selectedPrefab, ParentFor(level), level.gameObject.scene, placement));
            if (GUILayout.Button("Save Selection as Prefab") && Selected)
                RunLater(() => SavePrefab(level));
            showShapeLibrary = EditorGUILayout.Foldout(showShapeLibrary, "Browse model assets", true);
            if (showShapeLibrary)
                DrawLibrary();
        }

        private void DrawLibrary()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GeometryMeshAsset"))
            {
                var model = AssetDatabase.LoadAssetAtPath<GeometryMeshAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!model)
                    continue;
                Rect rect = EditorGUILayout.GetControlRect();
                if (GUI.Button(rect, model.name, EditorStyles.miniButton))
                    selectedModel = model;
                var current = Event.current;
                if (current.type == EventType.MouseDrag && rect.Contains(current.mousePosition))
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[] { model };
                    DragAndDrop.StartDrag(model.name);
                    current.Use();
                }
            }
        }

        private void DrawModel()
        {
            Title("Mesh edit mode");
            var previous = meshTool.Mode;
            var mode = (ModelingMode)GUILayout.Toolbar((int)previous, new[] { "Object", "Vertex", "Edge", "Face" });
            if (mode != previous)
            {
                meshTool.SelectMode(mode);
                SceneView.RepaintAll();
            }
            var geometry = SelectedModel;
            if (!geometry || !geometry.model || !geometry.surface)
            {
                EditorGUILayout.HelpBox("Select an editable geometry object in the hierarchy or Scene view.", MessageType.Info);
                DrawTransformSnapping();
                return;
            }

            var model = geometry.model;
            EditorGUILayout.LabelField(model.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{model.topology.vertices.Count} vertices  ·  {model.topology.faces.Count} faces", EditorStyles.miniLabel);
            if (model.sourceDrawing && GUILayout.Button("Open Source Drawing"))
                AssetDatabase.OpenAsset(model.sourceDrawing);

            using var locked = new EditorGUI.DisabledScope(geometry.editLocked || SceneVisibilityManager.instance.IsPickingDisabled(geometry.gameObject));
            if (geometry.editLocked)
                EditorGUILayout.HelpBox("Unlock this object in the level hierarchy to change it.", MessageType.Info);
            DrawModelProperties(geometry);
            if (mode != ModelingMode.Object)
            {
                meshSnap = Mathf.Max(0f, EditorGUILayout.FloatField("Vertex snap step", meshSnap));
                symmetry = EditorGUILayout.Toggle("Mirror X movement", symmetry);
                meshTool.SnapStep = meshSnap;
                meshTool.SymmetryX = symmetry;
                DrawElementActions(geometry, mode);
            }

            Title("Model operations");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Flip Normals"))
                    meshTool.Edit(geometry, "Flip Geometry Normals", data => data.faces.ForEach(face => face.vertices.Reverse()));
                if (GUILayout.Button("Recalculate Normals / UVs"))
                    meshTool.Edit(geometry, "Recalculate Geometry Surface", _ => { });
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mirror Model X"))
                    meshTool.Edit(geometry, "Mirror Geometry", TopologyEdits.Mirror);
                if (GUILayout.Button("Center Pivot"))
                    Run(() => { GeometryAssets.AssignUnique(geometry, true); meshTool.Reset(); });
                if (GUILayout.Button("Make Model Unique"))
                    Run(() => { GeometryAssets.AssignUnique(geometry, false); meshTool.Reset(); });
            }
            bool smooth = EditorGUILayout.Toggle("Smooth normals", model.topology.smooth);
            if (smooth != model.topology.smooth)
                meshTool.Edit(geometry, "Change Geometry Shading", data => data.smooth = smooth);
            DrawTransformSnapping();
        }

        private static void DrawTransformSnapping()
        {
            Title("Unity transform increments");
            Vector3 move = EditorGUILayout.Vector3Field("Move step", EditorSnapSettings.move);
            if (move != EditorSnapSettings.move)
                EditorSnapSettings.move = Vector3.Max(move, Vector3.one * 0.001f);
            float angle = EditorGUILayout.FloatField("Rotate step", EditorSnapSettings.rotate);
            if (!Mathf.Approximately(angle, EditorSnapSettings.rotate))
                EditorSnapSettings.rotate = Mathf.Clamp(angle, 0.001f, 360f);
        }

        private void DrawModelProperties(GeometryObject geometry)
        {
            bool isDecorative = EditorGUILayout.Toggle("Decorative", geometry.decorative);
            bool hasCollider = geometry.gameplayCollider && geometry.gameplayCollider.enabled;
            bool requestedCollider;
            using (new EditorGUI.DisabledScope(isDecorative))
                requestedCollider = EditorGUILayout.Toggle("Gameplay collider", hasCollider);
            if (isDecorative)
                requestedCollider = false;
            if (isDecorative != geometry.decorative || requestedCollider != hasCollider)
            {
                Undo.RecordObject(geometry, "Configure Geometry Classification");
                geometry.decorative = isDecorative;
                GeometryAssets.SetCollider(geometry, requestedCollider);
            }
            if (geometry.surfaceRenderer)
            {
                var renderer = geometry.surfaceRenderer;
                Material next = (Material)EditorGUILayout.ObjectField("Object material", renderer.sharedMaterial, typeof(Material), false);
                if (next && next != renderer.sharedMaterial)
                {
                    Undo.RecordObject(renderer, "Assign Object Material");
                    renderer.sharedMaterial = next;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
        }

        private void OpenDrawing()
        {
            if (drawing)
                AssetDatabase.OpenAsset(drawing);
            else
                EditorApplication.ExecuteMenuItem("Tools/Geometry Level Editor");
        }

        private void ConvertDrawing(GeometryLevel level)
        {
            var colors = new List<Color>();
            MeshTopology topology = GeometryGenerators.Convert(drawing, conversion, depth, beamWidth, useStrokeWidths, colors, out var issues);
            if (topology.faces.Count == 0)
                throw new InvalidOperationException(string.Join(" ", issues.Distinct()));
            var materials = colors.Select(color => selectedMaterial ? selectedMaterial : GeometryAssets.EdgeMaterial(color)).ToArray();
            var model = GeometryAssets.CreateModel(drawing.name + " " + conversion, topology, materials, drawing);
            PlaceModel(level, model, placement);
            if (issues.Count > 0)
                Report(string.Join(" ", issues.Distinct()), MessageType.Warning);
        }

        private void PlaceModel(GeometryLevel level, GeometryMeshAsset model, Vector3 point)
        {
            Selection.activeGameObject = GeometryAssets.Instantiate(model, ParentFor(level), point, decorative, generateCollider).gameObject;
            Report($"{model.name} placed. Select Mesh for topology editing.");
        }

        internal static Transform ParentFor(GeometryLevel level)
        {
            var selected = Selection.activeTransform;
            var selectedModel = selected ? selected.GetComponentInParent<GeometryObject>() : null;
            if (selectedModel)
                selected = selectedModel.transform.parent;
            return selected && selected.IsChildOf(level.transform) && selected.GetComponents<Component>().Length == 1
                ? selected : level.transform;
        }

        private void SavePrefab(GeometryLevel level)
        {
            if (Selected == level.gameObject || !Selected.transform.IsChildOf(level.transform))
                throw new InvalidOperationException("Select one geometry object or group within this level.");
            string folder = GeometryAssets.EnsureFolder(GeometryAssets.Prefabs);
            string path = EditorUtility.SaveFilePanelInProject("Save Geometry Prefab", Selected.name, "prefab", "Choose a new prefab path.", folder);
            if (string.IsNullOrEmpty(path))
                return;
            RefuseExisting(path);
            GeometryAssets.SavePrefab(Selected, path);
            Report("Prefab saved to " + path);
        }

        private void CreateLevel()
        {
            string folder = GeometryAssets.EnsureFolder(LevelAuthoring.LevelFolder);
            string path = EditorUtility.SaveFilePanelInProject("Create Geometry Level", "New Level", "unity", "Choose a new scene path.", folder);
            if (string.IsNullOrEmpty(path))
                return;
            RefuseExisting(path);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var level = LevelAuthoring.CreateInScene(scene, Path.GetFileNameWithoutExtension(path), true);
            LevelAuthoring.Save(level, path);
            Selection.activeGameObject = level.gameObject;
            validationIssues = null;
            Report("Level created. Use Create to place geometry and Puzzle to add a spawn and goal.");
        }

        private void OpenLevel()
        {
            string path = EditorUtility.OpenFilePanel("Open Geometry Level", "Assets", "unity");
            if (string.IsNullOrEmpty(path))
                return;
            string relative = FileUtil.GetProjectRelativePath(path);
            if (string.IsNullOrEmpty(relative) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(relative))
                throw new InvalidOperationException("Choose a Unity scene asset in this project.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = EditorSceneManager.OpenScene(relative, OpenSceneMode.Single);
            var level = LevelAuthoring.FindLevel(scene);
            if (level)
                Selection.activeGameObject = level.gameObject;
            else
                Report("This scene has no Geometry Level root.", MessageType.Warning);
            validationIssues = null;
        }

        private void SaveLevel(GeometryLevel level)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop the playtest before saving the level.");
            if (!LevelAuthoring.Save(level))
                SaveLevelCopy(level, false);
            else
                Report($"Saved {level.levelTitle}.");
        }

        private void SaveLevelCopy(GeometryLevel level, bool keepOriginalOpen)
        {
            string folder = GeometryAssets.EnsureFolder(LevelAuthoring.LevelFolder);
            string name = Path.GetFileNameWithoutExtension(level.gameObject.scene.path);
            string path = EditorUtility.SaveFilePanelInProject(keepOriginalOpen ? "Duplicate Level" : "Save Level As",
                (string.IsNullOrEmpty(name) ? "New Level" : name) + " Copy", "unity",
                "Choose a new scene path.", folder);
            if (string.IsNullOrEmpty(path))
                return;
            RefuseExisting(path);
            if (!LevelAuthoring.Save(level, path, keepOriginalOpen))
                throw new InvalidOperationException("Unity could not save the level copy.");
            Report(keepOriginalOpen ? $"Scene copy saved to {path}. Model assets are shared." : $"Saved scene as {path}.");
        }

        private static void RefuseExisting(string path)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                throw new InvalidOperationException("An asset already exists here. Choose a new filename.");
        }

        private void ValidateLevel(GeometryLevel level)
        {
            validationIssues = LevelAuthoring.Validate(level);
            Report(validationIssues.Count == 0 ? "Level validation passed." : $"{validationIssues.Count} items to review.",
                validationIssues.Count == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void TogglePlaytest()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
                return;
            }
            WithLevel(level =>
            {
                ValidateLevel(level);
                if (!level.gameplayCamera || !level.spawn || !level.goal)
                {
                    SelectPanel(Panel.Level);
                    throw new InvalidOperationException("Assign the gameplay camera, ball spawn and goal before playtesting.");
                }
                LevelPlaytest.Start(level);
            });
        }

        private void DuringSceneView(SceneView view)
        {
            var level = LevelAuthoring.FindLevel(SceneManager.GetActiveScene());
            if (!level || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var geometry = SelectedModel;
            var current = Event.current;
            if (meshTool.Mode != ModelingMode.Object && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                meshTool.SelectMode(ModelingMode.Object);
                current.Use();
                return;
            }
            if (geometry && geometry.transform.IsChildOf(level.transform) &&
                !SceneVisibilityManager.instance.IsPickingDisabled(geometry.gameObject) &&
                !SceneVisibilityManager.instance.IsHidden(geometry.gameObject))
                meshTool.Draw(geometry);

            if (current.type is not (EventType.DragUpdated or EventType.DragPerform))
                return;
            var model = DragAndDrop.objectReferences.OfType<GeometryMeshAsset>().FirstOrDefault();
            if (!model)
                return;
            var parent = ParentFor(level);
            Vector3 drop = default;
            bool valid = model.mesh && SceneConstruction.TryPosition(current.mousePosition, model.mesh.bounds,
                parent, placement.y, out drop);
            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (valid)
                SceneConstruction.ShowPreview(view, model.mesh.bounds, SceneConstruction.LeafMatrix(parent, drop));
            if (current.type == EventType.DragPerform)
            {
                SceneConstruction.ExitPlacement();
                if (valid)
                {
                    DragAndDrop.AcceptDrag();
                    RunLater(() => PlaceModel(level, model, drop));
                }
            }
            current.Use();
        }

        private void SelectionChanged()
        {
            meshTool.Reset();
            chosenComponent = null;
            Repaint();
        }

        private void UndoChanged()
        {
            meshTool.Reset();
            foreach (var obj in FindObjectsByType<GeometryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                GeometryAssets.Sync(obj, false);
            validationIssues = null;
            RepaintSceneAndWindow();
        }

        private void PlayModeChanged(PlayModeStateChange _)
        {
            meshTool.SelectMode(ModelingMode.Object);
            SetToolVisibility(false);
            Repaint();
        }

        private void RepaintSceneAndWindow()
        {
            SceneConstruction.MeshEditing = meshTool.Mode != ModelingMode.Object;
            if (SceneConstruction.MeshEditing)
                SceneConstruction.ExitPlacement();
            SetToolVisibility(meshTool.Mode != ModelingMode.Object && SelectedModel && !SelectedModel.editLocked &&
                !EditorApplication.isPlayingOrWillChangePlaymode);
            Repaint();
            SceneView.RepaintAll();
        }

        private void SetToolVisibility(bool hide)
        {
            if (hide == ownsToolVisibility)
                return;
            if (hide)
                previousToolVisibility = Tools.hidden;
            Tools.hidden = hide || previousToolVisibility;
            ownsToolVisibility = hide;
        }

        private void SetNativeTool(Tool value)
        {
            SwitchToObjectMode();
            SceneConstruction.SetGrab(false);
            Tools.current = value;
            SceneView.RepaintAll();
        }

        private void SwitchToObjectMode() => meshTool.SelectMode(ModelingMode.Object);

        private void WithLevel(Action<GeometryLevel> apply)
        {
            var level = CurrentLevel;
            if (level)
                Run(() => apply(level));
            else
                ReportError("Create or open a level first.");
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException)
            {
                ReportError(error.Message);
            }
            Repaint();
        }

        private void RunLater(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this)
                    Run(action);
            };
        }

        private void ReportError(string message) => Report(message, MessageType.Error);

        private void Report(string message, MessageType type = MessageType.Info)
        {
            status = message;
            statusType = type;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusLine == null)
                return;
            statusLine.text = status;
            statusLine.tooltip = status;
            statusLine.style.color = statusType == MessageType.Error ? new Color(1f, 0.44f, 0.44f) :
                statusType == MessageType.Warning ? new Color(0.95f, 0.71f, 0.37f) : new Color(0.69f, 0.77f, 0.83f);
        }

        private static void Title(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
