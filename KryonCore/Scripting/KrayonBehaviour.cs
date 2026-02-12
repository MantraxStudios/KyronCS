namespace KrayonCore
{
    public abstract class KrayonBehaviour
    {
        public GameObject GameObject { get; set; }

        public virtual void Start() { }
        public virtual void Update(float deltaTime) { }
        public virtual void OnDestroy() { }
    }
}