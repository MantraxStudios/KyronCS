using System;

namespace KrayonCore
{
    public abstract class Component : IComponent
    {
        [NoSerializeToInspector] public Guid Id { get; private set; }
        [NoSerializeToInspector] public GameObject GameObject { get; set; }
        public bool Enabled { get; set; } = true;

        internal bool _started = false;

        public Component()
        {
            Id = Guid.NewGuid();
        }

        public virtual void Awake() { }
        public virtual void Start() { }
        public virtual void Update(float deltaTime) { }
        public virtual void OnWillRenderObject() { }
        public virtual void OnDestroy() { }
        public virtual void OnReloadComponent() { }

        public T GetComponent<T>() where T : Component
        {
            return GameObject?.GetComponent<T>();
        }

        public T AddComponent<T>() where T : Component, new()
        {
            return GameObject?.AddComponent<T>();
        }
    }
}