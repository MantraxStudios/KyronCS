using KrayonCore.Graphics.GameUI;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Static manager for all UICanvas instances.
    /// Single source of truth — no singleton, no per-renderer instance.
    ///
    /// Usage:
    ///   UICanvasManager.Create("hud",  sceneRenderer);
    ///   UICanvasManager.Create("menu", sceneRenderer, sortOrder: 10);
    ///   UICanvasManager.Get("hud")?.Add(new UILabel { Text = "Hello World!" });
    /// </summary>
    public static class UICanvasManager
    {
        // ── Storage ───────────────────────────────────────────────────────
        private static readonly Dictionary<string, UICanvas> _canvases = new();

        // ── Factory ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new canvas attached to <paramref name="renderer"/> and registers it.
        /// If a canvas with the same name already exists it is returned unchanged.
        /// </summary>
        public static UICanvas Create(string name, SceneRenderer renderer, int sortOrder = 0)
        {
            if (_canvases.TryGetValue(name, out var existing))
                return existing;

            var canvas = new UICanvas(name, sortOrder);
            canvas.Attach(renderer);
            _canvases[name] = canvas;
            return canvas;
        }

        /// <summary>Creates a canvas without attaching it to any renderer.</summary>
        public static UICanvas CreateDetached(string name, int sortOrder = 0)
        {
            if (_canvases.TryGetValue(name, out var existing))
                return existing;

            var canvas = new UICanvas(name, sortOrder);
            _canvases[name] = canvas;
            return canvas;
        }

        // ── Lookup ────────────────────────────────────────────────────────

        public static UICanvas? Get(string name)
            => _canvases.TryGetValue(name, out var c) ? c : null;

        public static bool Has(string name) => _canvases.ContainsKey(name);

        public static IEnumerable<UICanvas> All() => _canvases.Values;

        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>Broadcasts Update to all visible canvases.</summary>
        public static void Update(float deltaTime)
        {
            foreach (var c in _canvases.Values)
                c.Update(deltaTime);
        }

        /// <summary>Removes, detaches, and disposes a canvas.</summary>
        public static bool Destroy(string name)
        {
            if (!_canvases.Remove(name, out var c)) return false;
            c.Dispose();
            return true;
        }

        /// <summary>Disposes all canvases and clears the manager.</summary>
        public static void Shutdown()
        {
            foreach (var c in _canvases.Values) c.Dispose();
            _canvases.Clear();
        }
    }
}