using UnityEditor;
using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class ElasticMotionPreview
    {
        private static ElasticGeometry target;
        private static GeometryObject geometry;
        private static double start;

        internal static void Start(ElasticGeometry source)
        {
            target = source;
            geometry = source.GetComponent<GeometryObject>();
            if (!geometry || !geometry.model)
                return;
            start = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            SceneView.duringSceneGui -= Draw;
            SceneView.duringSceneGui += Draw;
        }

        private static void Update()
        {
            if (!target || !geometry || EditorApplication.timeSinceStartup - start > 1.8 || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Update;
                SceneView.duringSceneGui -= Draw;
            }
            SceneView.RepaintAll();
        }

        private static void Draw(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || !target || !geometry || !geometry.surface || !geometry.model)
                return;
            float t = (float)(EditorApplication.timeSinceStartup - start);
            float bounce = Mathf.Sin(t * Mathf.Sqrt(Mathf.Max(1f, target.stiffness))) *
                Mathf.Exp(-Mathf.Max(0.2f, target.damping * 0.5f) * t);
            float offset = target.maximumDisplacement * bounce;
            Vector3 axis = target.localAxis.normalized;
            Vector3 delta = target.mode == ElasticDragMode.Translate ? axis * offset : Vector3.zero;
            var rotation = target.mode == ElasticDragMode.Rotate ? Quaternion.AngleAxis(offset, axis) : Quaternion.identity;
            float stretch = Mathf.Abs(bounce) * target.squashStretch;
            var scale = new Vector3(1f - stretch * 0.45f, 1f + stretch, 1f - stretch * 0.45f);
            Transform surface = geometry.surface.transform;
            Vector3 localDelta = surface.InverseTransformVector(target.transform.TransformDirection(delta));
            var matrix = surface.localToWorldMatrix * Matrix4x4.TRS(localDelta, rotation, scale);
            using var scope = new Handles.DrawingScope(new Color(0.41f, 1f, 0.81f), matrix);
            var topology = geometry.model.topology;
            foreach (var face in topology.faces)
                for (int i = 0; i < face.vertices.Count; i++)
                    Handles.DrawLine(topology.vertices[face.vertices[i]], topology.vertices[face.vertices[(i + 1) % face.vertices.Count]]);
        }
    }
}
