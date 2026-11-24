using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BounceBack.GeometryWorld
{
    internal sealed class RuntimeBlockBuilder
    {
        private static readonly Color Valid = new(0.48f, 0.95f, 0.72f, 0.42f);
        private static readonly Color Blocked = new(1f, 0.34f, 0.27f, 0.42f);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly GeometryLevel level;
        private readonly HashSet<SculptedBlock> edits = new();
        private readonly HashSet<RuntimeBuildPiece> pieceEdits = new();
        private readonly Dictionary<RuntimeBuildPiece, GameObject> markers = new();
        private readonly List<SculptedBlock> placed;
        private readonly BuildFlight flight = new();
        private readonly BuildInventory inventory = new();
        private readonly Collider[] overlaps = new Collider[1];
        private readonly MaterialPropertyBlock tint = new();
        private GameObject preview;
        private MeshRenderer previewRenderer;
        private MeshFilter previewFilter;
        private Mesh cubeMesh;
        private RuntimeBuildCatalog.Item lastItem;
        private BuildItemGeometry itemGeometry;
        private bool active;
        private bool sculpt;
        private bool backpack;
        private int quarterTurns;

        internal RuntimeBlockBuilder(GeometryLevel level)
        {
            this.level = level;
            placed = new List<SculptedBlock>(level.GetComponentsInChildren<SculptedBlock>());
        }
        internal bool Active => active;

        internal void Toggle()
        {
            active = !active;
            if (active)
            {
                flight.Begin(level.gameplayCamera);
                foreach (var piece in level.GetComponentsInChildren<RuntimeBuildPiece>())
                    CreateMarker(piece);
                ShowMarkers(true);
            }
            else
            {
                backpack = false;
                flight.End();
                HidePreview();
                ShowMarkers(false);
            }
        }

        internal bool Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
                Toggle();
            if (!active)
                return false;

            var mouse = Mouse.current;
            Camera camera = level.gameplayCamera;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    backpack = false;
                    flight.Release();
                }
                if (keyboard.eKey.wasPressedThisFrame)
                    ToggleBackpack();
                if (!backpack && keyboard.vKey.wasPressedThisFrame)
                    sculpt = !sculpt;
                if (!backpack && keyboard.rKey.wasPressedThisFrame)
                    quarterTurns = (quarterTurns + 1) % 4;
            }
            if (backpack || mouse == null || !camera || !flight.Captured || !Application.isFocused)
            {
                HidePreview();
                return true;
            }

            inventory.Read(keyboard, mouse);
            flight.Update(keyboard, mouse);
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            PhysicsScene physics = level.gameObject.scene.GetPhysicsScene();
            bool hitSurface = physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, camera.farClipPlane,
                ~0, QueryTriggerInteraction.Ignore);
            var block = hitSurface ? hit.collider.GetComponent<SculptedBlock>() : null;
            if (block && (!block.transform.IsChildOf(level.transform) || string.IsNullOrEmpty(block.BlockId)))
                block = null;
            if (sculpt)
            {
                Sculpt(block, hit, mouse);
                return true;
            }
            var marker = PickMarker(ray, hitSurface ? hit.distance : camera.farClipPlane);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (marker)
                    RemovePiece(marker);
                else if (hitSurface)
                    Remove(hit.collider);
                HidePreview();
                return true;
            }

            var item = inventory.Selected;
            if (item == null || !TryAim(ray, hitSurface, hit, out Pose cubePose))
            {
                HidePreview();
                return true;
            }

            if (lastItem != item)
            {
                lastItem = item;
                itemGeometry = new BuildItemGeometry(item.Sculptable ? null : item.prefab, Vector3.one);
            }
            Vector3 normal = hitSurface ? hit.normal : Vector3.up;
            Pose pose = itemGeometry.RestOnFace(cubePose, normal, quarterTurns);
            bool available = HasRoom(pose, itemGeometry);
            DrawItemPreview(pose, available);
            if (available && mouse.rightButton.wasPressedThisFrame)
            {
                if (item.Sculptable)
                    Add(pose);
                else
                    PlaceItem(item, pose);
            }
            return true;
        }

        private void ToggleBackpack()
        {
            backpack = !backpack;
            if (backpack)
            {
                HidePreview();
                flight.Release();
            }
            else
                flight.Resume();
        }

        private void Sculpt(SculptedBlock block, RaycastHit hit, Mouse mouse)
        {
            if (!block || !block.TryCell(hit.point, hit.normal, false, out Vector3Int cut))
            {
                HidePreview();
                return;
            }
            Transform frame = block.transform;
            Vector3 cell = frame.TransformPoint(SculptedBlock.CellCenter(cut));
            DrawPreview(new Pose(cell, frame.rotation), frame.lossyScale * (SculptedBlock.CellSize * 1.03f), false);
            if (mouse.leftButton.wasPressedThisFrame)
                ChangeCell(block, cut, false);
            else if (mouse.rightButton.wasPressedThisFrame && block.TryCell(hit.point, hit.normal, true, out Vector3Int fill))
            {
                Vector3 center = frame.TransformPoint(SculptedBlock.CellCenter(fill));
                Vector3 extents = frame.lossyScale * (SculptedBlock.CellSize * 0.48f);
                if (level.gameObject.scene.GetPhysicsScene().OverlapBox(center, extents, overlaps, frame.rotation,
                    ~0, QueryTriggerInteraction.Ignore) == 0)
                    ChangeCell(block, fill, true);
            }
        }

        internal void ChangeCell(SculptedBlock block, Vector3Int cell, bool filled)
        {
            block.SetCell(cell, filled);
            edits.Add(block);
            Physics.SyncTransforms();
        }

        private bool TryAim(Ray ray, bool hitSurface, RaycastHit hit, out Pose pose)
        {
            pose = default;
            if (hitSurface)
            {
                Collider surface = hit.collider;
                if (!surface.transform.IsChildOf(level.transform) ||
                    surface.attachedRigidbody && surface.attachedRigidbody.GetComponent<BallBody>())
                    return false;
                Transform frame = surface.transform;
                Vector3 center = frame.position;
                if (surface is BoxCollider box)
                    center = frame.TransformPoint(box.center);
                else if (surface is MeshCollider mesh && mesh.sharedMesh)
                    center = frame.TransformPoint(mesh.sharedMesh.bounds.center);
                return TryFacePose(center, frame.rotation, hit.point, hit.normal, out pose);
            }

            Vector3 origin = level.transform.position;
            if (!new Plane(Vector3.up, origin).Raycast(ray, out float distance))
                return false;
            return TryFacePose(origin, Quaternion.identity, ray.GetPoint(distance), Vector3.up, out pose);
        }

        internal static bool TryFacePose(Vector3 origin, Quaternion rotation, Vector3 hit, Vector3 normal, out Pose pose)
        {
            pose = default;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 localNormal = inverse * normal;
            int axis = Mathf.Abs(localNormal.x) > Mathf.Abs(localNormal.y) ? 0 : 1;
            if (Mathf.Abs(localNormal.z) > Mathf.Abs(localNormal[axis]))
                axis = 2;
            if (Mathf.Abs(localNormal[axis]) < 0.99f)
                return false;

            Vector3 point = inverse * (hit - origin);
            Vector3 snapped = new(Mathf.Round(point.x), Mathf.Round(point.y), Mathf.Round(point.z));
            snapped[axis] = point[axis] + Mathf.Sign(localNormal[axis]) * 0.5f;
            pose = new Pose(origin + rotation * snapped, rotation);
            return true;
        }

        internal bool HasRoom(Pose pose) => HasRoom(pose, new BuildItemGeometry(null, Vector3.one));

        private bool HasRoom(Pose pose, BuildItemGeometry item)
        {
            Vector3 center = pose.position + item.CenterOffset(pose.rotation);
            foreach (var block in placed)
                if (block && !block.Empty && (block.transform.position - center).sqrMagnitude < 0.0001f)
                    return false;
            return level.gameObject.scene.GetPhysicsScene().OverlapBox(center,
                item.Size * 0.493f, overlaps, pose.rotation, ~0, QueryTriggerInteraction.Ignore) == 0;
        }

        internal RuntimeBuildPiece PlaceItem(RuntimeBuildCatalog.Item item, Pose pose)
        {
            var obj = UnityEngine.Object.Instantiate(item.prefab, pose.position, pose.rotation, level.transform);
            obj.name = item.label;
            var piece = obj.AddComponent<RuntimeBuildPiece>();
            piece.SetIdentity(Guid.NewGuid().ToString("N"), item.id);
            pieceEdits.Add(piece);
            level.RefreshBuiltGadgetReferences(obj);
            CreateMarker(piece);
            Physics.SyncTransforms();
            return piece;
        }

        internal void Add(Pose pose)
        {
            var obj = new GameObject("Build Block");
            obj.transform.SetPositionAndRotation(pose.position, pose.rotation);
            obj.transform.SetParent(level.transform, true);
            var block = obj.AddComponent<SculptedBlock>();
            block.SetId(Guid.NewGuid().ToString("N"));
            placed.Add(block);
            edits.Add(block);
            Physics.SyncTransforms();
        }

        internal void Remove(Collider collider)
        {
            var piece = collider.GetComponentInParent<RuntimeBuildPiece>();
            if (piece && piece.transform.IsChildOf(level.transform))
            {
                RemovePiece(piece);
                return;
            }
            if (!collider.TryGetComponent(out SculptedBlock block) || !block.transform.IsChildOf(level.transform) ||
                string.IsNullOrEmpty(block.BlockId))
                return;
            block.ApplyCells(block.BlockId, new int[SculptedBlock.Resolution]);
            edits.Add(block);
            Physics.SyncTransforms();
        }

        private void RemovePiece(RuntimeBuildPiece piece)
        {
            piece.gameObject.SetActive(false);
            pieceEdits.Add(piece);
            level.RefreshBuiltGadgetReferences(null);
            Physics.SyncTransforms();
        }

        internal BuildBlockState[] Snapshot()
        {
            var layout = new List<BuildBlockState>(edits.Count + pieceEdits.Count);
            foreach (var block in edits)
                if (block)
                    layout.Add(block.Capture());
            foreach (var piece in pieceEdits)
                if (piece)
                    layout.Add(piece.Capture());
            return layout.ToArray();
        }

        private void DrawItemPreview(Pose pose, bool available)
        {
            if (!itemGeometry.Mesh)
            {
                DrawPreview(new Pose(pose.position + itemGeometry.CenterOffset(pose.rotation), pose.rotation), itemGeometry.Size, available);
                return;
            }
            var matrix = itemGeometry.MeshToRoot;
            Vector3 center = pose.position + pose.rotation * Vector3.Scale(matrix.GetColumn(3), itemGeometry.Scale);
            DrawPreview(new Pose(center, pose.rotation * matrix.rotation), Vector3.Scale(matrix.lossyScale, itemGeometry.Scale),
                available, itemGeometry.Mesh);
        }

        private void DrawPreview(Pose pose, Vector3 size, bool available, Mesh mesh = null)
        {
            if (!preview)
            {
                preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                preview.name = "Build Preview";
                preview.hideFlags = HideFlags.DontSave;
                preview.GetComponent<Collider>().enabled = false;
                previewRenderer = preview.GetComponent<MeshRenderer>();
                previewFilter = preview.GetComponent<MeshFilter>();
                cubeMesh = previewFilter.sharedMesh;
                previewRenderer.sharedMaterial = Resources.Load<Material>("BounceBack/Build Preview");
                previewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                previewRenderer.receiveShadows = false;
            }
            previewFilter.sharedMesh = mesh ? mesh : cubeMesh;
            preview.transform.SetPositionAndRotation(pose.position, pose.rotation);
            preview.transform.localScale = size;
            tint.SetColor(BaseColor, available ? Valid : Blocked);
            previewRenderer.SetPropertyBlock(tint);
            preview.SetActive(true);
        }

        private void CreateMarker(RuntimeBuildPiece piece)
        {
            if (markers.ContainsKey(piece) || piece.GetComponentInChildren<Renderer>())
                return;
            var shape = new BuildItemGeometry(piece.gameObject, Vector3.one);
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Build Marker";
            marker.hideFlags = HideFlags.DontSave;
            marker.GetComponent<Collider>().enabled = false;
            marker.transform.SetParent(piece.transform, false);
            marker.transform.localPosition = shape.Bounds.center;
            marker.transform.localScale = shape.Bounds.size * 0.92f;
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("BounceBack/Build Preview");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tint.SetColor(BaseColor, piece.GetComponent<PuzzleTrigger>()?.role == TriggerRole.Goal
                ? new Color(1f, 0.77f, 0.33f, 0.36f) : new Color(0.48f, 0.73f, 1f, 0.34f));
            renderer.SetPropertyBlock(tint);
            marker.SetActive(active);
            markers[piece] = marker;
        }

        private void ShowMarkers(bool show)
        {
            foreach (var marker in markers.Values)
                if (marker)
                    marker.SetActive(show);
        }

        private RuntimeBuildPiece PickMarker(Ray ray, float limit)
        {
            RuntimeBuildPiece selected = null;
            foreach (var (piece, marker) in markers)
            {
                if (!piece || !marker || !marker.activeInHierarchy)
                    continue;
                Transform frame = marker.transform;
                var local = new Ray(frame.InverseTransformPoint(ray.origin), frame.InverseTransformVector(ray.direction));
                if (!new Bounds(Vector3.zero, Vector3.one).IntersectRay(local, out float distance))
                    continue;
                float worldDistance = Vector3.Distance(ray.origin, frame.TransformPoint(local.GetPoint(distance)));
                if (worldDistance > limit)
                    continue;
                limit = worldDistance;
                selected = piece;
            }
            return selected;
        }

        private void HidePreview()
        {
            if (preview)
                preview.SetActive(false);
        }

        internal void Dispose()
        {
            flight.End();
            if (preview)
                UnityEngine.Object.Destroy(preview);
        }

        private Rect Panel(float scale) => new(12f, Screen.height / scale - (active ? 190f : 48f),
            active ? 475f : 118f, active ? 116f : 36f);

        internal bool PointerOverHud(Vector2 screenPoint)
        {
            float scale = GeometryLevel.HudScale;
            return Panel(scale).Contains(new Vector2(screenPoint.x, Screen.height - screenPoint.y) / scale);
        }

        internal void DrawHud(float scale)
        {
            Rect panel = Panel(scale);
            if (!active)
            {
                if (GUI.Button(panel, "Build · B"))
                    Toggle();
                return;
            }

            inventory.DrawHotbar(scale, !flight.Captured);
            if (backpack)
            {
                inventory.DrawBackpack(scale);
                if (GUI.Button(new Rect(12f, Screen.height / scale - 104f, 142f, 29f), "Close Backpack · E"))
                    ToggleBackpack();
                return;
            }

            float y = panel.y;
            GUI.Box(panel, sculpt ? "Sculpt · 4 × 4 × 4" : $"Build · {inventory.Selected?.label ?? "Empty slot"}");
            GUI.Label(new Rect(22f, y + 23f, 397f, 20f), sculpt ? "Left: carve voxel    Right: fill a hole" : "Left: break    Right: place");
            GUI.Label(new Rect(22f, y + 45f, 397f, 20f), "WASD: fly    Space / Ctrl: up / down    Shift: boost");
            if (flight.Captured)
            {
                var rect = level.gameplayCamera.pixelRect;
                Vector2 center = new Vector2(rect.center.x, Screen.height - rect.center.y) / scale;
                GUI.DrawTexture(new Rect(center.x - 7f, center.y - 1f, 14f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - 1f, center.y - 7f, 2f, 14f), Texture2D.whiteTexture);
                GUI.Label(new Rect(22f, y + 76f, 455f, 22f), "E: backpack   1–9 / scroll: slot   R: turn   V: sculpt   Esc: menu");
                return;
            }
            if (GUI.Button(new Rect(22f, y + 77f, 104f, 30f), "Resume Flight"))
                flight.Resume();
            if (GUI.Button(new Rect(131f, y + 77f, 75f, 30f), "Play · B"))
                Toggle();
            if (GeometryLevel.CanSaveBuild && (edits.Count > 0 || pieceEdits.Count > 0) &&
                GUI.Button(new Rect(211f, y + 77f, 200f, 30f), "Save Blocks & Exit"))
                level.SaveBuiltBlocks(Snapshot());
        }
    }
}
