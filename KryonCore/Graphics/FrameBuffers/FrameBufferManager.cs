using KrayonCore.Graphics.FrameBuffers;

namespace KrayonCore
{
    /// <summary>
    /// Registro centralizado de FrameBuffers identificados por nombre.
    /// Permite crear, acceder, redimensionar y destruir buffers en cualquier parte del engine.
    /// </summary>
    public sealed class FrameBufferManager : IDisposable
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static FrameBufferManager Instance { get; } = new();
        private FrameBufferManager() { }

        // ── Registro ─────────────────────────────────────────────────────────
        private readonly Dictionary<string, FrameBuffer> _buffers = new();
        private bool _disposed;

        // ── API pública ──────────────────────────────────────────────────────

        /// <summary>
        /// Crea un FrameBuffer con el nombre dado. Si ya existe, lo reemplaza y destruye el anterior.
        /// </summary>
        public FrameBuffer Create(string name, int width, int height,
            bool useEmission = false, bool useGBuffer = false)
        {
            if (_buffers.TryGetValue(name, out var existing))
            {
                Console.WriteLine($"[FrameBufferManager] Reemplazando '{name}'");
                existing.Dispose();
            }

            var fb = new FrameBuffer(width, height, useEmission, useGBuffer);
            _buffers[name] = fb;
            Console.WriteLine($"[FrameBufferManager] Creado '{name}' ({width}x{height})");
            return fb;
        }

        /// <summary>Obtiene un FrameBuffer por nombre. Lanza excepción si no existe.</summary>
        public FrameBuffer Get(string name)
        {
            if (_buffers.TryGetValue(name, out var fb)) return fb;
            throw new KeyNotFoundException($"[FrameBufferManager] FrameBuffer '{name}' no encontrado.");
        }

        /// <summary>Obtiene un FrameBuffer por nombre o null si no existe.</summary>
        public FrameBuffer? TryGet(string name)
            => _buffers.TryGetValue(name, out var fb) ? fb : null;

        /// <summary>Comprueba si existe un FrameBuffer con ese nombre.</summary>
        public bool Has(string name) => _buffers.ContainsKey(name);

        /// <summary>Redimensiona todos los FrameBuffers registrados.</summary>
        public void ResizeAll(int width, int height)
        {
            foreach (var (name, fb) in _buffers)
            {
                fb.Resize(width, height);
                Console.WriteLine($"[FrameBufferManager] Resized '{name}' → {width}x{height}");
            }
        }

        /// <summary>Redimensiona un FrameBuffer específico por nombre.</summary>
        public void Resize(string name, int width, int height)
            => Get(name).Resize(width, height);

        /// <summary>Destruye y elimina del registro un FrameBuffer por nombre.</summary>
        public void Remove(string name)
        {
            if (_buffers.Remove(name, out var fb))
            {
                fb.Dispose();
                Console.WriteLine($"[FrameBufferManager] Eliminado '{name}'");
            }
        }

        /// <summary>Destruye y elimina todos los FrameBuffers del registro.</summary>
        public void Clear()
        {
            foreach (var fb in _buffers.Values)
                fb.Dispose();

            _buffers.Clear();
            Console.WriteLine("[FrameBufferManager] Todos los buffers eliminados");
        }

        /// <summary>Lista los nombres de todos los buffers registrados.</summary>
        public IReadOnlyCollection<string> Names => _buffers.Keys;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}