namespace KrayonCore
{
    public abstract class KrayonBehaviour
    {
        public GameObject GameObject { get; set; }

        public virtual void Start() { }
        public virtual void Update(float deltaTime) { }
        public virtual void OnDestroy() { }

        // Collision events
        public virtual void OnCollisionEnter(GameObject other) { }
        public virtual void OnCollisionStay(GameObject other) { }
        public virtual void OnCollisionExit(GameObject other) { }

        // Trigger events
        public virtual void OnTriggerEnter(GameObject other) { }
        public virtual void OnTriggerStay(GameObject other) { }
        public virtual void OnTriggerExit(GameObject other) { }
    }
}