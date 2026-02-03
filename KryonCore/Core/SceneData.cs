using System;
using System.Collections.Generic;

namespace KrayonCore
{
    [Serializable]
    public class SceneData
    {
        public string SceneName { get; set; }
        public List<GameObjectData> GameObjects { get; set; } = new List<GameObjectData>();
    }

    [Serializable]
    public class GameObjectData
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public bool Active { get; set; }
        public List<ComponentData> Components { get; set; } = new List<ComponentData>();
    }

    [Serializable]
    public class ComponentData
    {
        public string TypeName { get; set; }
        public Guid ComponentId { get; set; }
        public bool Enabled { get; set; }
        public Dictionary<string, object> SerializedFields { get; set; } = new Dictionary<string, object>();
    }
}