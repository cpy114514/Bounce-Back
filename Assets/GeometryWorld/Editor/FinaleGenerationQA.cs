using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    public static class FinaleGenerationQA
    {
        public static void Create()
        {
            Debug.Log("J_FINALE_GENERATION_BEGIN");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.DeleteAsset(SantoriniSystemsAuthoring.LevelPath))
                throw new System.InvalidOperationException("QA could not replace the stale generated scene.");
            SantoriniSystemsAuthoring.Create();
            var flow = Object.FindFirstObjectByType<CampaignPlayground>();
            if (!flow || !flow.finale || flow.finale.AnchorCount != 4 || !flow.finale.waterfall)
                throw new System.InvalidOperationException("Generated finale references are incomplete.");
            if (!File.Exists(SantoriniSystemsAuthoring.LevelPath))
                throw new System.InvalidOperationException("Generated scene is not present on disk.");
            Debug.Log($"J_FINALE_GENERATION_VERIFIED anchors={flow.finale.AnchorCount} seam={flow.city.waypoints[4].position} camera={flow.rig.overviewSize}");
            Debug.Log("J_FINALE_GENERATION_END");
        }
    }
}
