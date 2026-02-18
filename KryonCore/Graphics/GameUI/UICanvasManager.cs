using KrayonCore.Graphics.GameUI;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Singleton manager for all UICanvas instances.
    /// Handles creation, lookup, resize broadcasting, and shutdown.
    ///
    /// Usage:
    ///   UICanvasManager.Instance.Create("hud",  sceneRenderer, sortOrder: 0);
    ///   UICanvasManager.Instance.Create("menu", sceneRenderer, sortOrder: 10);
    ///   UICanvasManager.Instance.Get("hud")?.Add(new UILabel { Text = "Hello World!" ... });
    /// </summary>
    public sealed class UICanvasManager
    {
        // ── Singleton ─────────────────────────────────────────────────────
        private static UICanvasManager? _instance;
        public static UICanvasManager Instance
            => _instance ??= new UICanvasManager();

        // ── Storage ───────────────────────────────────────────────────────
        private readonly Dictionary<string, UICanvas> _canvases = new();

        // ── Factory ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new canvas, attaches it to <paramref name="renderer"/>, and registers it.
        /// If a canvas with the same name already exists it is returned unchanged.
        /// </summary>
        public UICanvas Create(string name, SceneRenderer renderer, int sortOrder = 0)
        {
            if (_canvases.TryGetValue(name, out var existing))
                return existing;

            var canvas = new UICanvas(name, sortOrder);
            canvas.Attach(renderer);
            _canvases[name] = canvas;
            return canvas;
        }

        /// <summary>Creates a canvas without attaching it to any renderer.</summary>
        public UICanvas CreateDetached(string name, int sortOrder = 0)
        {
            if (_canvases.TryGetValue(name, out var existing))
                return existing;

            var canvas = new UICanvas(name, sortOrder);
            _canvases[name] = canvas;
            return canvas;
        }

        // ── Lookup ────────────────────────────────────────────────────────

        public UICanvas? Get(string name)
            => _canvases.TryGetValue(name, out var c) ? c : null;

        public bool Has(string name) => _canvases.ContainsKey(name);

        public IEnumerable<UICanvas> All() => _canvases.Values;

        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>Broadcasts Update to all visible canvases.</summary>
        public void Update(float deltaTime)
        {
            foreach (var c in _canvases.Values)
                c.Update(deltaTime);
        }

        /// <summary>
        /// No es necesario llamar a este método — los canvas leen el viewport
        /// de GL automáticamente cada frame. Se mantiene por compatibilidad.
        /// </summary>
        public void Resize(int width, int height) { }

        /// <summary>Removes, detaches, and disposes a canvas.</summary>
        public bool Destroy(string name)
        {
            if (!_canvases.Remove(name, out var c)) return false;
            c.Dispose();
            return true;
        }

        /// <summary>Disposes all canvases and resets the manager.</summary>
        public void Shutdown()
        {
            foreach (var c in _canvases.Values) c.Dispose();
            _canvases.Clear();
        }
    }
}