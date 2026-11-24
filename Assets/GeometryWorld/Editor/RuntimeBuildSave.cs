using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    [InitializeOnLoad]
    internal static class RuntimeBuildSave
    {
        private const string PendingKey = "GeometryWorld.PendingVoxelBuild";

        [Serializable]
        private sealed class Layout
        {
            public string scene;
            public BuildBlockState[] blocks;
        }

        static RuntimeBuildSave()
        {
            GeometryLevel.SaveBuildRequested += RequestSave;
            Undo.undoRedoPerformed += RebuildAfterUndo;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += ApplyPending;
            };
        }

        private static void RebuildAfterUndo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            foreach (var block in UnityEngine.Object.FindObjectsByType<SculptedBlock>(FindObjectsSortMode.None))
                block.Rebuild();
        }

        private static void RequestSave(GeometryLevel level, BuildBlockState[] blocks)
        {
            if (!EditorApplication.isPlaying || blocks.Length == 0)
                return;
            string path = level.gameObject.scene.path;
            if (string.IsNullOrEmpty(path) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
            {
                Debug.LogWarning("Save the level as a scene asset before using Save Blocks & Exit.");
                return;
            }
            var layout = new Layout { scene = path, blocks = blocks };
            SessionState.SetString(PendingKey, JsonUtility.ToJson(layout));
            EditorApplication.ExitPlaymode();
        }

        private static void ApplyPending()
        {
            string pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending))
                return;
            SessionState.EraseString(PendingKey);
            var layout = JsonUtility.FromJson<Layout>(pending);
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(layout.scene);
            if (!asset)
            {
                Debug.LogWarning("The saved build could not be applied because the level is missing.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(layout.scene);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(layout.scene, OpenSceneMode.Additive);
            var level = LevelAuthoring.FindLevel(scene);
            if (!level)
            {
                Debug.LogWarning($"The saved build could not find a Geometry Level in {layout.scene}.");
                return;
            }

            bool alreadyDirty = scene.isDirty;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            const string change = "Apply Play Mode Blocks";
            Undo.SetCurrentGroupName(change);
            int changed = ApplyBlocks(level, layout.blocks, change);
            Undo.CollapseUndoOperations(group);
            if (changed == 0)
                return;
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = !alreadyDirty && EditorSceneManager.SaveScene(scene);
            string message = saved ? $"Saved {changed} block changes" :
                $"Applied {changed} block changes. Save the scene when ready.";
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message));
            Debug.Log($"{message} · {layout.scene}");
        }

        internal static int ApplyBlocks(GeometryLevel level, BuildBlockState[] blocks, string change)
        {
            var existing = new System.Collections.Generic.Dictionary<string, SculptedBlock>();
            foreach (var block in level.GetComponentsInChildren<SculptedBlock>(true))
                if (!string.IsNullOrEmpty(block.BlockId))
                    existing.TryAdd(block.BlockId, block);
            var pieces = new System.Collections.Generic.Dictionary<string, RuntimeBuildPiece>();
            foreach (var piece in level.GetComponentsInChildren<RuntimeBuildPiece>(true))
                if (!string.IsNullOrEmpty(piece.PieceId))
                    pieces.TryAdd(piece.PieceId, piece);
            var catalog = RuntimeBuildCatalog.Load();
            bool wroteGadgets = false;

            int changed = 0;
            foreach (var block in blocks)
            {
                if (!string.IsNullOrEmpty(block.itemId))
                {
                    if (!wroteGadgets)
                        Undo.RegisterCompleteObjectUndo(level, change);
                    if (ApplyPiece(level, pieces, catalog, block, change))
                        changed++;
                    wroteGadgets = true;
                    continue;
                }
                if (existing.TryGetValue(block.id, out var current))
                {
                    if (block.IsRemoved)
                        Undo.DestroyObjectImmediate(current.gameObject);
                    else
                    {
                        Undo.RecordObject(current, change);
                        current.ApplyCells(block.id, block.layers);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(current);
                    }
                }
                else if (!block.IsRemoved)
                {
                    var instance = new GameObject("Sculpted Block");
                    SceneManager.MoveGameObjectToScene(instance, level.gameObject.scene);
                    instance.transform.SetPositionAndRotation(block.position, block.rotation);
                    instance.transform.SetParent(level.transform, true);
                    instance.AddComponent<SculptedBlock>().ApplyCells(block.id, block.layers);
                    Undo.RegisterCreatedObjectUndo(instance, change);
                }
                else
                    continue;
                changed++;
            }
            if (wroteGadgets)
            {
                level.RefreshBuiltGadgetReferences(null);
                PrefabUtility.RecordPrefabInstancePropertyModifications(level);
            }
            return changed;
        }

        private static bool ApplyPiece(GeometryLevel level,
            System.Collections.Generic.Dictionary<string, RuntimeBuildPiece> existing, RuntimeBuildCatalog catalog,
            BuildBlockState state, string change)
        {
            if (state.IsRemoved)
            {
                if (!existing.TryGetValue(state.id, out var piece))
                    return false;
                Undo.DestroyObjectImmediate(piece.gameObject);
                return true;
            }
            if (existing.ContainsKey(state.id))
                return false;
            var item = catalog ? catalog.Find(state.itemId) : null;
            if (item == null || !item.prefab)
            {
                Debug.LogWarning($"Could not save the build item '{state.itemId}'.");
                return false;
            }
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, level.gameObject.scene);
            obj.name = item.label;
            obj.transform.SetParent(level.transform, false);
            obj.transform.SetPositionAndRotation(state.position, state.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);
            obj.AddComponent<RuntimeBuildPiece>().SetIdentity(state.id, state.itemId);
            level.RefreshBuiltGadgetReferences(obj);
            Undo.RegisterCreatedObjectUndo(obj, change);
            return true;
        }
    }
}
