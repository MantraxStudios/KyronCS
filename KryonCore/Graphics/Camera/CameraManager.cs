using OpenTK.Mathematics;

namespace KrayonCore
{
    public class CameraManager
    {
        private readonly Dictionary<string, RenderCamera> _cameras = new();
        private string? _mainCamera = null;

        /// <summary>Registra una cámara. La primera registrada se convierte en main.</summary>
        public RenderCamera Add(RenderCamera camera)
        {
            if (_cameras.TryGetValue(camera.Name, out var existing))
            {
                Console.WriteLine($"[CameraManager] Reemplazando '{camera.Name}'");
                existing.Dispose();
            }

            _cameras[camera.Name] = camera;
            _mainCamera ??= camera.Name;

            Console.WriteLine($"[CameraManager] Registrada '{camera.Name}' (priority={camera.Priority})");
            return camera;
        }

        /// <summary>Crea y registra una cámara en una sola llamada.</summary>
        public RenderCamera Create(string name, Vector3 position,
            float aspectRatio, int priority = 0)
        {
            string uniqueName = name;
            int counter = 1;

            while (_cameras.ContainsKey(uniqueName))
            {
                uniqueName = $"{name}_{counter}";
                counter++;
            }

            Console.WriteLine($"Camera: { _cameras.Count}");

            return Add(new RenderCamera(uniqueName, position, aspectRatio, priority));
        }

        /// <summary>Obtiene una cámara por nombre. Lanza excepción si no existe.</summary>
        public RenderCamera Get(string name)
        {
            if (_cameras.TryGetValue(name, out var cam)) return cam;
            throw new KeyNotFoundException($"[CameraManager] Cámara '{name}' no encontrada.");
        }

        /// <summary>Obtiene una cámara por nombre o null.</summary>
        public RenderCamera? TryGet(string name)
            => _cameras.TryGetValue(name, out var cam) ? cam : null;

        public bool Has(string name) => _cameras.ContainsKey(name);

        /// <summary>Establece la cámara principal por nombre.</summary>
        public void SetMain(string name)
        {
            if (!_cameras.ContainsKey(name))
                throw new KeyNotFoundException($"[CameraManager] '{name}' no encontrada.");

            _mainCamera = name;
            Console.WriteLine($"[CameraManager] Main → '{name}'");
        }

        /// <summary>Cámara principal activa.</summary>
        public RenderCamera? Main
            => _mainCamera is not null ? TryGet(_mainCamera) : null;

        /// <summary>Cámaras habilitadas ordenadas por prioridad ascendente.</summary>
        public IEnumerable<RenderCamera> GetRenderOrder()
            => _cameras.Values
                .Where(c => c.Enabled)
                .OrderBy(c => c.Priority);

        /// <summary>Actualiza el aspect ratio de todas las cámaras.</summary>
        public void ResizeAll(int width, int height)
        {
            foreach (var cam in _cameras.Values)
            {
                cam.Camera.UpdateAspectRatio(width, height);
                cam.ResizeBuffer(width, height);
            }
        }

        /// <summary>Elimina y destruye una cámara por nombre.</summary>
        public void Remove(string name)
        {
            if (!_cameras.Remove(name, out var cam)) return;

            cam.Dispose();
            if (_mainCamera == name)
                _mainCamera = _cameras.Keys.FirstOrDefault();

            Console.WriteLine($"[CameraManager] Eliminada '{name}'");
        }

        public void Clear()
        {
            foreach (var cam in _cameras.Values)
                cam.Dispose();

            _cameras.Clear();
            _mainCamera = null;
        }

        public int Count => _cameras.Count;

        public IReadOnlyCollection<string> Names => _cameras.Keys;
    }
}