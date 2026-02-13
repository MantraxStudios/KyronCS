using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KrayonCore.GraphicsData;

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

        public Camera GetMainCamera() { return GraphicsEngine.Instance.GetSceneRenderer().GetCamera(); }
        public void DestroyObject(GameObject objectToDestroy) { SceneManager.ActiveScene.DestroyGameObject(objectToDestroy); }

        // -------------------- INVOKE SYSTEM --------------------
        private Dictionary<string, CancellationTokenSource> _invokeTokens = new();

        /// <summary>
        /// Invoca una acción una sola vez después de un delay.
        /// </summary>
        public void Invoke(string id, Action action, int delayMilliseconds)
        {
            CancelInvoke(id); // Si ya existe, cancelamos
            var cts = new CancellationTokenSource();
            _invokeTokens[id] = cts;
            RunOnce(action, delayMilliseconds, cts.Token);
        }

        private async void RunOnce(Action action, int delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
                if (!token.IsCancellationRequested) action();
            }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// Invoca repetidamente una acción con delay inicial y periodo.
        /// </summary>
        public void InvokeRepeating(string id, Action action, int delayMilliseconds, int repeatMilliseconds)
        {
            CancelInvoke(id); // Si ya existe, cancelamos
            var cts = new CancellationTokenSource();
            _invokeTokens[id] = cts;
            RunRepeating(action, delayMilliseconds, repeatMilliseconds, cts.Token);
        }

        private async void RunRepeating(Action action, int initialDelay, int repeat, CancellationToken token)
        {
            try
            {
                await Task.Delay(initialDelay, token);
                while (!token.IsCancellationRequested)
                {
                    action();
                    await Task.Delay(repeat, token);
                }
            }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// Cancela un Invoke específico por ID.
        /// </summary>
        public void CancelInvoke(string id)
        {
            if (_invokeTokens.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                _invokeTokens.Remove(id);
            }
        }

        /// <summary>
        /// Cancela todos los Invokes activos.
        /// </summary>
        public void CancelAllInvokes()
        {
            foreach (var cts in _invokeTokens.Values)
                cts.Cancel();
            _invokeTokens.Clear();
        }
    }
}
