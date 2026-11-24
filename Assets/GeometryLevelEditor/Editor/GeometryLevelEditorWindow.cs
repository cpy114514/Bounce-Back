using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BounceBack.Geometry.Editor
{
    internal sealed class GeometryLevelEditorWindow : EditorWindow
    {
        [SerializeField] private GeometryDrawing drawing;
        [SerializeField] private string activeLayerId;
        [SerializeField] private Vector2 viewCenter;
        [SerializeField] private float viewZoom = DrawingCanvas.DefaultZoom;
        [SerializeField] private bool drawMode = true;
        [SerializeField] private bool scenePreview;

        private readonly DrawingEditSession session = new();
        private DrawingCanvas canvas;
        private ObjectField drawingField;
        private Button saveButton, saveAsButton, duplicateButton, drawButton, selectButton;
        private Button removeLayerButton, raiseLayerButton, lowerLayerButton;
        private Button copyButton, pasteButton, deleteButton, addSwatchButton, replaceSwatchButton, removeSwatchButton;
        private VisualElement workArea, emptyState, layerList, swatches;
        private Label hint, feedback, coordinates, zoomLabel, selectionSummary;
        private TextField layerName;
        private Vector2Field nodePosition;
        private FloatField strokeWidth, gridSpacing;
        private ColorField strokeColor;
        private Toggle snapToggle, dotsToggle;
        private Label strokeTitle;
        private int paletteIndex = -1;
        private IVisualElementScheduledItem clearFeedback;

        [MenuItem("Tools/Geometry Level Editor")]
        private static void ShowWindow() => GetWindow<GeometryLevelEditorWindow>();

        [OnOpenAsset]
        private static bool OpenDrawingAsset(int instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject((EntityId)instanceId) is not GeometryDrawing asset)
                return false;

            GetWindow<GeometryLevelEditorWindow>().OpenDrawing(asset);
            return true;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Geometry Level Editor");
            minSize = new Vector2(700f, 360f);
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneView.duringSceneGui += DrawScenePreview;
            EditorApplication.projectChanged += OnProjectChanged;
            session.Changed += Refresh;
            session.Feedback += ShowFeedback;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneView.duringSceneGui -= DrawScenePreview;
            EditorApplication.projectChanged -= OnProjectChanged;
            session.Changed -= Refresh;
            session.Feedback -= ShowFeedback;
            if (scenePreview)
                SceneView.RepaintAll();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("geometry-editor");
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string stylePath = Path.ChangeExtension(scriptPath, ".uss");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);

            BuildFileBar(root);
            BuildEmptyState(root);
            workArea = Element("geometry-work-area");
            root.Add(workArea);
            BuildToolBar(workArea);

            var body = Element("geometry-body");
            workArea.Add(body);
            canvas = new DrawingCanvas(session, viewCenter, viewZoom) { DrawMode = drawMode };
            canvas.ViewChanged += RefreshView;
            canvas.Shortcut += HandleShortcut;
            canvas.RegisterCallback<ValidateCommandEvent>(evt =>
            {
                if (IsDrawingCommand(evt.commandName))
                    evt.StopPropagation();
            });
            canvas.RegisterCallback<ExecuteCommandEvent>(evt =>
            {
                if (RunDrawingCommand(evt.commandName))
                    evt.StopPropagation();
            });
            body.Add(canvas);
            BuildSidebar(body);
            BuildStatusBar(workArea);
            session.Open(drawing, activeLayerId);
        }

        private void BuildFileBar(VisualElement root)
        {
            var bar = Element("geometry-file-bar");
            bar.Add(ActionButton("New", NewDrawing, "Create a new drawing asset."));
            drawingField = new ObjectField("Drawing")
            {
                objectType = typeof(GeometryDrawing),
                allowSceneObjects = false,
                tooltip = "Pick an existing drawing asset or drop it here from the Project window."
            };
            drawingField.AddToClassList("geometry-drawing-field");
            drawingField.RegisterValueChangedCallback(evt => OpenDrawing(evt.newValue as GeometryDrawing));
            bar.Add(drawingField);
            saveButton = ActionButton("Save", SaveDrawing, "Save this drawing asset.");
            saveAsButton = ActionButton("Save As…", SaveAs, "Save a copy at a new path and open it.");
            duplicateButton = ActionButton("Duplicate", DuplicateDrawing, "Create a copy beside this asset.");
            bar.Add(saveButton);
            bar.Add(saveAsButton);
            bar.Add(duplicateButton);
            root.Add(bar);
        }

        private void BuildEmptyState(VisualElement root)
        {
            emptyState = Element("geometry-empty");
            var title = new Label("Start a drawing");
            title.AddToClassList("geometry-empty-title");
            emptyState.Add(title);
            emptyState.Add(new Label("Create a drawing asset or choose one from the Drawing field."));
            var create = ActionButton("New Drawing…", NewDrawing);
            create.AddToClassList("geometry-empty-action");
            emptyState.Add(create);
            root.Add(emptyState);
        }

        private void BuildToolBar(VisualElement parent)
        {
            var bar = Element("geometry-tool-bar");
            drawButton = ActionButton("Draw", () => SetTool(true), "D. Click points to draw a connected path.");
            selectButton = ActionButton("Select", () => SetTool(false), "V. Select or move nodes and edges.");
            bar.Add(drawButton);
            bar.Add(selectButton);
            bar.Add(Element("geometry-divider"));
            bar.Add(ActionButton("Undo", Undo.PerformUndo, "Undo the last Unity edit."));
            bar.Add(ActionButton("Redo", Undo.PerformRedo, "Redo the last Unity edit."));
            bar.Add(ActionButton("Frame", () => canvas.Frame(), "F. Frame the selection or all visible layers."));
            bar.Add(Element("geometry-flex"));
            var sceneToggle = new Toggle("Scene preview") { tooltip = "Preview visible layers on the Scene view XZ plane." };
            sceneToggle.SetValueWithoutNotify(scenePreview);
            sceneToggle.RegisterValueChangedCallback(evt =>
            {
                scenePreview = evt.newValue;
                SceneView.RepaintAll();
            });
            zoomLabel = new Label();
            zoomLabel.AddToClassList("geometry-zoom");
            bar.Add(sceneToggle);
            bar.Add(zoomLabel);
            parent.Add(bar);
        }

        private void BuildSidebar(VisualElement parent)
        {
            var sidebar = Element("geometry-sidebar");
            var settings = new ScrollView(ScrollViewMode.Vertical);
            settings.AddToClassList("geometry-settings");
            settings.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sidebar.Add(settings);

            var layers = Section(settings, "Layers", out var layersHeader);
            layersHeader.Add(ActionButton("+ Add", session.AddLayer, "Add a layer above the current drawing."));
            layerList = Element("geometry-layer-list");
            layers.Add(layerList);
            layerName = new TextField("Name") { isDelayed = true };
            layerName.RegisterValueChangedCallback(evt =>
            {
                var layer = session.Layer;
                string name = evt.newValue.Trim();
                if (layer == null || name.Length == 0 || name == layer.Name)
                    Refresh();
                else
                    session.Edit("Rename Drawing Layer", () => layer.name = name);
            });
            layers.Add(layerName);
            var layerActions = Element("geometry-row");
            raiseLayerButton = ActionButton("Raise", () => session.ReorderLayer(session.Layer, 1), "Move this layer toward the top.");
            lowerLayerButton = ActionButton("Lower", () => session.ReorderLayer(session.Layer, -1), "Move this layer toward the bottom.");
            removeLayerButton = ActionButton("Remove", () => session.RemoveLayer(session.Layer), "Remove the active layer. Unity Undo restores it.");
            layerActions.Add(raiseLayerButton);
            layerActions.Add(lowerLayerButton);
            layerActions.Add(removeLayerButton);
            layers.Add(layerActions);

            var stroke = Section(settings, "Stroke", out var strokeHeader);
            strokeTitle = (Label)strokeHeader[0];
            strokeColor = new ColorField("Color") { showEyeDropper = false, tooltip = "Color for the brush or selected edges." };
            strokeWidth = new FloatField("Width") { isDelayed = true, tooltip = "Stroke width in drawing units." };
            strokeColor.RegisterValueChangedCallback(evt => SetStrokeColor(evt.newValue));
            strokeWidth.RegisterValueChangedCallback(evt => SetStrokeWidth(evt.newValue));
            stroke.Add(strokeColor);
            stroke.Add(strokeWidth);
            swatches = Element("geometry-swatches");
            stroke.Add(swatches);
            var paletteActions = Element("geometry-row");
            addSwatchButton = ActionButton("Add Color", AddSwatch, "Add the current color to the palette.");
            replaceSwatchButton = ActionButton("Set", ReplaceSwatch, "Replace the chosen swatch with the current color.");
            removeSwatchButton = ActionButton("−", RemoveSwatch, "Remove the chosen swatch.");
            paletteActions.Add(addSwatchButton);
            paletteActions.Add(replaceSwatchButton);
            paletteActions.Add(removeSwatchButton);
            stroke.Add(paletteActions);

            var grid = Section(settings, "Grid", out _);
            gridSpacing = new FloatField("Spacing") { isDelayed = true, tooltip = "Grid spacing in drawing units. The dot display thins as you zoom out." };
            gridSpacing.RegisterValueChangedCallback(evt =>
            {
                float value = ClampNumber(evt.newValue, drawing.gridSpacing, DrawingEditSession.MinSpacing, DrawingEditSession.MaxSpacing);
                gridSpacing.SetValueWithoutNotify(value);
                if (value != drawing.gridSpacing)
                    session.Edit("Change Drawing Grid Spacing", () => drawing.gridSpacing = value);
            });
            grid.Add(gridSpacing);
            var gridToggles = Element("geometry-row");
            snapToggle = new Toggle("Snap");
            dotsToggle = new Toggle("Show dots");
            snapToggle.RegisterValueChangedCallback(evt => session.Edit("Toggle Drawing Snap", () => drawing.snap = evt.newValue));
            dotsToggle.RegisterValueChangedCallback(evt => session.Edit("Toggle Drawing Grid", () => drawing.showGrid = evt.newValue));
            gridToggles.Add(snapToggle);
            gridToggles.Add(dotsToggle);
            grid.Add(gridToggles);

            var selection = Section(sidebar, "Selection", out _);
            selection.AddToClassList("geometry-selection");
            selectionSummary = new Label();
            selectionSummary.AddToClassList("geometry-muted");
            selection.Add(selectionSummary);
            nodePosition = new Vector2Field("Position")
            {
                tooltip = "Enter a node position in drawing coordinates. Numeric edits use the entered values."
            };
            foreach (var component in nodePosition.Query<FloatField>().ToList())
                component.isDelayed = true;
            nodePosition.RegisterValueChangedCallback(evt =>
            {
                session.MoveNodes(session.SelectedEndpoints(), evt.newValue - evt.previousValue);
                Refresh();
            });
            selection.Add(nodePosition);
            var selectionActions = Element("geometry-row");
            copyButton = ActionButton("Copy", session.Copy, "Copy the selected nodes and edges.");
            pasteButton = ActionButton("Paste", () => Paste(false), "Paste into the active layer.");
            deleteButton = ActionButton("Delete", session.DeleteSelection, "Delete selected nodes and edges.");
            selectionActions.Add(copyButton);
            selectionActions.Add(pasteButton);
            selectionActions.Add(deleteButton);
            selection.Add(selectionActions);

            parent.Add(sidebar);
        }

        private void BuildStatusBar(VisualElement parent)
        {
            var bar = Element("geometry-status");
            hint = new Label();
            feedback = new Label();
            coordinates = new Label();
            hint.AddToClassList("geometry-status-message");
            feedback.AddToClassList("geometry-status-message");
            feedback.AddToClassList("geometry-feedback");
            coordinates.AddToClassList("geometry-coordinates");
            bar.Add(hint);
            bar.Add(feedback);
            bar.Add(coordinates);
            SetDisplayed(feedback, false);
            parent.Add(bar);
        }

        private void Refresh()
        {
            if (canvas == null)
                return;

            activeLayerId = session.ActiveLayerId;
            drawingField.SetValueWithoutNotify(drawing);
            SetDisplayed(workArea, drawing);
            SetDisplayed(emptyState, !drawing);
            saveButton.SetEnabled(drawing);
            saveAsButton.SetEnabled(drawing);
            duplicateButton.SetEnabled(drawing);
            if (!drawing)
                return;

            bool editable = session.CanEdit;
            drawButton.EnableInClassList("geometry-active", drawMode);
            selectButton.EnableInClassList("geometry-active", !drawMode);
            RefreshLayers();

            var edges = session.Layer.edges.Where(edge => session.IsEdgeSelected(edge.Id)).ToArray();
            Color color = edges.Length == 0 ? drawing.strokeColor : edges[0].Color;
            float width = edges.Length == 0 ? drawing.strokeWidth : edges[0].Width;
            strokeTitle.text = edges.Length == 0 ? "Stroke" : "Selected edge style";
            strokeColor.SetValueWithoutNotify(color);
            strokeWidth.SetValueWithoutNotify(width);
            strokeColor.showMixedValue = edges.Any(edge => edge.Color != color);
            strokeWidth.showMixedValue = edges.Any(edge => !Mathf.Approximately(edge.Width, width));
            RefreshPalette();
            gridSpacing.SetValueWithoutNotify(drawing.gridSpacing);
            snapToggle.SetValueWithoutNotify(drawing.snap);
            dotsToggle.SetValueWithoutNotify(drawing.showGrid);

            selectionSummary.text = session.HasSelection
                ? $"{CountLabel(session.SelectedNodes.Count, "node")} · {CountLabel(session.SelectedEdges.Count, "edge")}"
                : "No nodes or edges selected";
            bool singleNode = session.SelectedNodes.Count == 1 && session.SelectedEdges.Count == 0;
            SetDisplayed(nodePosition, singleNode);
            if (singleNode)
                nodePosition.SetValueWithoutNotify(session.Layer.nodes.Find(node => session.IsNodeSelected(node.Id)).Position);
            copyButton.SetEnabled(editable && session.HasSelection);
            deleteButton.SetEnabled(editable && session.HasSelection);
            pasteButton.SetEnabled(editable && session.HasClipboard);

            hint.text = editable
                ? drawMode
                    ? "Click to draw · Esc or right-click to end · Drag middle button to pan · Scroll to zoom"
                    : "Drag to move · Shift-click to extend · Drag empty space to box-select"
                : "Choose a visible, unlocked layer to edit.";
            hint.tooltip = hint.text;
            canvas.MarkDirtyRepaint();
            RefreshView();
            if (scenePreview)
                SceneView.RepaintAll();
        }

        private void RefreshLayers()
        {
            while (layerList.childCount < drawing.Layers.Count)
            {
                int index = layerList.childCount;
                var row = Element("geometry-layer-row");
                var name = ActionButton("", () => session.ActivateLayer(LayerAtDisplayIndex(index).Id));
                name.AddToClassList("geometry-layer-name");
                row.Add(name);
                row.Add(ActionButton("", () =>
                {
                    var layer = LayerAtDisplayIndex(index);
                    session.SetLayerVisible(layer, !layer.Visible);
                }, "Toggle layer visibility."));
                row.Add(ActionButton("", () =>
                {
                    var layer = LayerAtDisplayIndex(index);
                    session.SetLayerLocked(layer, !layer.Locked);
                }, "Toggle layer locking."));
                layerList.Add(row);
            }
            while (layerList.childCount > drawing.Layers.Count)
                layerList.RemoveAt(layerList.childCount - 1);

            for (int index = 0; index < drawing.Layers.Count; index++)
            {
                var layer = LayerAtDisplayIndex(index);
                var row = layerList[index];
                var name = (Button)row[0];
                name.text = layer.Name;
                name.tooltip = $"{layer.Name} · {layer.Nodes.Count} nodes · {layer.Edges.Count} edges";
                row.EnableInClassList("geometry-active-layer", layer.Id == activeLayerId);
                ((Button)row[1]).text = layer.Visible ? "Visible" : "Hidden";
                ((Button)row[2]).text = layer.Locked ? "Locked" : "Lock";
            }

            var active = session.Layer;
            layerName.SetValueWithoutNotify(active.Name);
            layerName.SetEnabled(!active.Locked);
            int order = drawing.layers.IndexOf(active);
            raiseLayerButton.SetEnabled(order < drawing.layers.Count - 1);
            lowerLayerButton.SetEnabled(order > 0);
            removeLayerButton.SetEnabled(drawing.layers.Count > 1 && !active.Locked);
        }

        private DrawingLayer LayerAtDisplayIndex(int index) => drawing.layers[drawing.layers.Count - 1 - index];

        private void RefreshPalette()
        {
            while (swatches.childCount < drawing.palette.Count)
            {
                int index = swatches.childCount;
                var swatch = ActionButton("", () =>
                {
                    paletteIndex = index;
                    SetStrokeColor(drawing.palette[index]);
                });
                swatch.AddToClassList("geometry-swatch");
                swatches.Add(swatch);
            }
            while (swatches.childCount > drawing.palette.Count)
                swatches.RemoveAt(swatches.childCount - 1);

            for (int index = 0; index < drawing.palette.Count; index++)
            {
                Color color = drawing.palette[index];
                var swatch = (Button)swatches[index];
                swatch.style.backgroundColor = color;
                swatch.tooltip = $"#{ColorUtility.ToHtmlStringRGBA(color)} · Apply color";
                swatch.EnableInClassList("geometry-swatch-selected", index == paletteIndex);
            }
            bool hasSwatch = paletteIndex >= 0 && paletteIndex < drawing.palette.Count;
            addSwatchButton.SetEnabled(drawing.palette.Count < 32);
            replaceSwatchButton.SetEnabled(hasSwatch);
            removeSwatchButton.SetEnabled(hasSwatch);
        }

        private void SetStrokeColor(Color color)
        {
            var edges = session.Layer.edges.Where(edge => session.IsEdgeSelected(edge.Id)).ToArray();
            if (edges.Length == 0 && drawing.strokeColor != color)
                session.Edit("Change Drawing Brush Color", () => drawing.strokeColor = color);
            else if (edges.Any(edge => edge.Color != color))
                session.Edit("Change Drawing Edge Color", () => Array.ForEach(edges, edge => edge.color = color));
            else
                RefreshPalette();
        }

        private void SetStrokeWidth(float requested)
        {
            var edges = session.Layer.edges.Where(edge => session.IsEdgeSelected(edge.Id)).ToArray();
            float width = ClampNumber(requested, edges.Length == 0 ? drawing.strokeWidth : edges[0].Width,
                DrawingEditSession.MinWidth, DrawingEditSession.MaxWidth);
            if (edges.Length == 0 && drawing.strokeWidth != width)
                session.Edit("Change Drawing Brush Width", () => drawing.strokeWidth = width);
            else if (edges.Any(edge => edge.Width != width))
                session.Edit("Change Drawing Edge Width", () => Array.ForEach(edges, edge => edge.width = width));
            else
                strokeWidth.SetValueWithoutNotify(width);
        }

        private void AddSwatch()
        {
            paletteIndex = drawing.palette.Count;
            session.Edit("Add Drawing Palette Color", () => drawing.palette.Add(strokeColor.value));
        }

        private void ReplaceSwatch()
        {
            if (drawing.palette[paletteIndex] != strokeColor.value)
                session.Edit("Replace Drawing Palette Color", () => drawing.palette[paletteIndex] = strokeColor.value);
        }

        private void RemoveSwatch()
        {
            int index = paletteIndex;
            paletteIndex = -1;
            session.Edit("Remove Drawing Palette Color", () => drawing.palette.RemoveAt(index));
        }

        private void RefreshView()
        {
            viewCenter = canvas.Center;
            viewZoom = canvas.Zoom;
            zoomLabel.text = $"{viewZoom / DrawingCanvas.DefaultZoom:P0}";
            var point = canvas.CursorPosition;
            coordinates.text = point.HasValue ? $"{point.Value.x:0.##} , {point.Value.y:0.##}" : "";
        }

        private void ShowFeedback(string message)
        {
            if (feedback == null)
                return;
            clearFeedback?.Pause();
            feedback.text = message;
            feedback.tooltip = message;
            SetDisplayed(feedback, true);
            SetDisplayed(hint, false);
            clearFeedback = feedback.schedule.Execute(() =>
            {
                SetDisplayed(feedback, false);
                SetDisplayed(hint, true);
            }).StartingIn(3500);
        }

        private void SetTool(bool draw)
        {
            drawMode = draw;
            canvas.DrawMode = draw;
            session.FinishChain();
            if (draw)
                session.ClearSelection();
            Refresh();
            canvas.Focus();
        }

        private void Paste(bool duplicate)
        {
            bool pasted = duplicate ? session.DuplicateSelection(canvas.Center) : session.Paste(canvas.Center);
            if (pasted)
            {
                SetTool(false);
                canvas.RevealPastedSelection();
            }
        }

        private void HandleShortcut(KeyDownEvent evt)
        {
            bool handled = true;
            if (evt.actionKey)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.S: SaveDrawing(); break;
                    case KeyCode.Z when evt.shiftKey: Undo.PerformRedo(); break;
                    case KeyCode.Z: Undo.PerformUndo(); break;
                    case KeyCode.Y: Undo.PerformRedo(); break;
                    case KeyCode.A: SetTool(false); session.SelectAll(); break;
                    case KeyCode.C: session.Copy(); break;
                    case KeyCode.V: Paste(false); break;
                    case KeyCode.D: Paste(true); break;
                    default: handled = false; break;
                }
            }
            else
            {
                switch (evt.keyCode)
                {
                    case KeyCode.D: SetTool(true); break;
                    case KeyCode.V: SetTool(false); break;
                    case KeyCode.F: canvas.Frame(); break;
                    case KeyCode.Delete:
                    case KeyCode.Backspace: session.DeleteSelection(); break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter: session.FinishChain(); break;
                    case KeyCode.Escape:
                        if (!canvas.CancelGesture() && !session.FinishChain())
                            session.ClearSelection();
                        break;
                    default: handled = false; break;
                }
            }

            if (handled)
                evt.StopPropagation();
        }

        private static bool IsDrawingCommand(string command) =>
            command is "Copy" or "Paste" or "Duplicate" or "Delete" or "SoftDelete" or "SelectAll" or "FrameSelected";

        private bool RunDrawingCommand(string command)
        {
            switch (command)
            {
                case "Copy": session.Copy(); break;
                case "Paste": Paste(false); break;
                case "Duplicate": Paste(true); break;
                case "Delete":
                case "SoftDelete": session.DeleteSelection(); break;
                case "SelectAll": SetTool(false); session.SelectAll(); break;
                case "FrameSelected": canvas.Frame(); break;
                default: return false;
            }
            return true;
        }

        private void OpenDrawing(GeometryDrawing asset)
        {
            if (drawing == asset)
                return;

            drawing = asset;
            activeLayerId = null;
            paletteIndex = -1;
            if (canvas == null)
                return;

            canvas.ResetView();
            session.Open(asset);
            if (asset)
                canvas.schedule.Execute(canvas.Frame);
            SceneView.RepaintAll();
        }

        private void NewDrawing()
        {
            string path = ChooseAssetPath("New Geometry Drawing", "Geometry Drawing");
            if (!string.IsNullOrEmpty(path))
                CreateDrawing(path, null, true);
        }

        private void SaveDrawing()
        {
            if (!drawing)
                return;
            AssetDatabase.SaveAssetIfDirty(drawing);
            ShowFeedback(EditorUtility.IsDirty(drawing) ? "Unity could not save this drawing." : $"Saved {drawing.name}.");
        }

        private void SaveAs()
        {
            string path = ChooseAssetPath("Save Geometry Drawing As", drawing.name + " Copy");
            if (!string.IsNullOrEmpty(path))
                CreateDrawing(path, drawing, true);
        }

        private void DuplicateDrawing()
        {
            string directory = DrawingDirectory();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{drawing.name} Copy.asset");
            CreateDrawing(path, drawing, false);
        }

        private string ChooseAssetPath(string title, string name) =>
            EditorUtility.SaveFilePanelInProject(title, name, "asset", "Choose a location in the project.", DrawingDirectory());

        private string DrawingDirectory() => drawing
            ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(drawing))?.Replace('\\', '/') ?? "Assets"
            : "Assets";

        private void CreateDrawing(string path, GeometryDrawing source, bool open)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                EditorUtility.DisplayDialog("Geometry Drawing", "An asset already exists at that path. Choose a new filename.", "OK");
                return;
            }

            var asset = source ? Instantiate(source) : CreateInstance<GeometryDrawing>();
            asset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            if (!AssetDatabase.Contains(asset))
            {
                DestroyImmediate(asset);
                EditorUtility.DisplayDialog("Geometry Drawing", "Unity could not create the drawing asset at that path.", "OK");
                return;
            }

            AssetDatabase.SaveAssetIfDirty(asset);
            if (open)
                OpenDrawing(asset);
            EditorGUIUtility.PingObject(asset);
            ShowFeedback($"Created {asset.name}.");
        }

        private void OnUndoRedo()
        {
            canvas?.CancelGesture();
            paletteIndex = -1;
            session.RefreshFromAsset(finishChain: true);
        }

        private void OnProjectChanged()
        {
            if (!drawing)
            {
                canvas?.CancelGesture();
                drawing = null;
                session.Open(null);
                if (scenePreview)
                    SceneView.RepaintAll();
            }
            else
                session.RefreshFromAsset();
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!scenePreview || !drawing || EditorApplication.isPlaying || Event.current.type != EventType.Repaint)
                return;

            using var scope = new Handles.DrawingScope();
            foreach (DrawingLayer layer in drawing.Layers.Where(layer => layer.Visible))
            {
                var positions = layer.Nodes.ToDictionary(node => node.Id, node => new Vector3(node.Position.x, 0f, node.Position.y));
                foreach (DrawingEdge edge in layer.Edges)
                {
                    if (!positions.TryGetValue(edge.StartId, out Vector3 start) || !positions.TryGetValue(edge.EndId, out Vector3 end))
                        continue;
                    Handles.color = edge.Color;
                    float scale = HandleUtility.GetHandleSize((start + end) / 2f) / 80f;
                    float width = Mathf.Clamp(edge.Width / Mathf.Max(scale, 0.001f), 1.5f, 20f);
                    Handles.DrawAAPolyLine(width, start, end);
                }
                Handles.color = new Color32(232, 237, 252, 220);
                foreach (Vector3 point in positions.Values)
                    Handles.DotHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * 0.035f, EventType.Repaint);
            }
        }

        private static VisualElement Section(VisualElement parent, string name, out VisualElement header)
        {
            var section = Element("geometry-section");
            header = Element("geometry-section-header");
            var title = new Label(name);
            title.AddToClassList("geometry-section-title");
            header.Add(title);
            section.Add(header);
            parent.Add(section);
            return section;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        private static Button ActionButton(string text, Action action, string tooltip = null)
        {
            var button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("geometry-button");
            return button;
        }

        private static void SetDisplayed(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        private static string CountLabel(int count, string singular) => $"{count} {singular}{(count == 1 ? "" : "s")}";

        private static float ClampNumber(float value, float fallback, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }
}
