using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceBack.GeometryWorld.Editor
{
    internal sealed class GeometryBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null)
                return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var metadata in root.GetComponentsInChildren<GeometryObject>(true))
                    Object.DestroyImmediate(metadata);
        }
    }
}
