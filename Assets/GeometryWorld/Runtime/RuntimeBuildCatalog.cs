using System;
using UnityEngine;

namespace BounceBack.GeometryWorld
{
    public sealed class RuntimeBuildCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            public string id;
            public string label;
            public bool gadget;
            public GameObject prefab;

            internal bool Sculptable => id == "Block";
        }

        public Item[] items = Array.Empty<Item>();
        public static RuntimeBuildCatalog Load() => Resources.Load<RuntimeBuildCatalog>("BounceBack/Build Catalog");

        public Item Find(string id) => Array.Find(items, item => item.id == id);
    }
}
