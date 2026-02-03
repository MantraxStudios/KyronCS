using System;

namespace KrayonCore
{
    public interface IComponent
    {
        Guid Id { get; }
        GameObject GameObject { get; set; }
        bool Enabled { get; set; }
        void Awake();
        void Start();
        void Update(float deltaTime);
        void OnDestroy();
    }
}