using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("BounceBack.Geometry.Editor")]

namespace BounceBack.Geometry
{
    public sealed class GeometryDrawing : ScriptableObject
    {
        [SerializeField, HideInInspector] internal List<DrawingLayer> layers = new() { new("Layer 1") };
        [SerializeField, HideInInspector] internal List<Color> palette = new()
        {
            new Color32(175, 159, 250, 255), new Color32(247, 170, 184, 255),
            new Color32(255, 207, 156, 255), new Color32(167, 224, 204, 255),
            new Color32(153, 203, 250, 255), new Color32(242, 237, 223, 255)
        };
        [SerializeField, HideInInspector] internal Color strokeColor = new Color32(175, 159, 250, 255);
        [SerializeField, HideInInspector] internal float strokeWidth = 0.14f;
        [SerializeField, HideInInspector] internal float gridSpacing = 1f;
        [SerializeField, HideInInspector] internal bool snap = true;
        [SerializeField, HideInInspector] internal bool showGrid = true;

        public IReadOnlyList<DrawingLayer> Layers => layers;
        public IReadOnlyList<Color> Palette => palette;
    }

    [Serializable]
    public sealed class DrawingLayer
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] internal string name;
        [SerializeField] internal bool visible = true;
        [SerializeField] internal bool locked;
        [SerializeField] internal List<DrawingNode> nodes = new();
        [SerializeField] internal List<DrawingEdge> edges = new();

        internal DrawingLayer(string name) => this.name = name;

        public string Id => id;
        public string Name => name;
        public bool Visible => visible;
        public bool Locked => locked;
        public IReadOnlyList<DrawingNode> Nodes => nodes;
        public IReadOnlyList<DrawingEdge> Edges => edges;
    }

    [Serializable]
    public sealed class DrawingNode
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] internal Vector2 position;

        internal DrawingNode(Vector2 position) => this.position = position;

        public string Id => id;
        public Vector2 Position => position;
    }

    [Serializable]
    public sealed class DrawingEdge
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        [SerializeField] private string startId;
        [SerializeField] private string endId;
        [SerializeField] internal Color color;
        [SerializeField] internal float width;

        internal DrawingEdge(string startId, string endId, Color color, float width)
        {
            this.startId = startId;
            this.endId = endId;
            this.color = color;
            this.width = width;
        }

        public string Id => id;
        public string StartId => startId;
        public string EndId => endId;
        public Color Color => color;
        public float Width => width;
    }
}
