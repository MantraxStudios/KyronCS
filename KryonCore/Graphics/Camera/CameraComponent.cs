using KrayonCore.Core;
using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;

namespace KrayonCore.Graphics.Camera
{
    public class CameraComponent : Component
    {
        // ── Backing fields ───────────────────────────────────────────────────
        private string _cameraName = "";
        private int _priority = 0;
        private bool _renderToBuffer = true;
        private bool _useGBuffer = true;
        private bool _useEmission = true;
        private int _bufferWidth = 1280;
        private int _bufferHeight = 720;

        private ProjectionMode _projectionMode = ProjectionMode.Perspective;
        private float _fov = 45.0f;
        private float _nearPlane = 0.1f;
        private float _farPlane = 1000.0f;
        private float _orthoSize = 10.0f;
        private float _aspectRatio = 16f / 9f;

        private CameraClearMode _clearMode = CameraClearMode.SolidColor;
        private Vector4 _clearColor = new Vector4(0.1f, 0.1f, 0.1f, 1f);

        private float _viewportX = 0f;
        private float _viewportY = 0f;
        private float _viewportWidth = 1f;
        private float _viewportHeight = 1f;

        // ── Propiedades reactivas ────────────────────────────────────────────
        // Cada setter aplica el cambio inmediatamente si la cámara ya existe.
        // Esto permite modificar desde scripts en runtime y ver el efecto al instante.

        [ToStorage, NoSerializeToInspector]
        public string CameraName
        {
            get => _cameraName;
            set { _cameraName = value; }  // Cambiar nombre requiere Rebuild() explícito
        }

        [ToStorage]
        public int Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                if (_renderCamera is not null) _renderCamera.Priority = value;
            }
        }

        /// <summary>Cambiar esto en runtime requiere llamar Rebuild() para recrear el buffer.</summary>
        [ToStorage] public bool RenderToBuffer { get => _renderToBuffer; set => _renderToBuffer = value; }
        [ToStorage] public bool UseGBuffer { get => _useGBuffer; set => _useGBuffer = value; }
        [ToStorage] public bool UseEmission { get => _useEmission; set => _useEmission = value; }

        [ToStorage]
        public int BufferWidth
        {
            get => _bufferWidth;
            set
            {
                _bufferWidth = value;
                _renderCamera?.ResizeBuffer(_bufferWidth, _bufferHeight);
            }
        }

        [ToStorage]
        public int BufferHeight
        {
            get => _bufferHeight;
            set
            {
                _bufferHeight = value;
                _renderCamera?.ResizeBuffer(_bufferWidth, _bufferHeight);
            }
        }

        // ── Proyección ───────────────────────────────────────────────────────

        [ToStorage]
        public ProjectionMode ProjectionMode
        {
            get => _projectionMode;
            set { _projectionMode = value; _renderCamera?.Camera.SetProjectionMode(value); }
        }

        [ToStorage]
        public float Fov
        {
            get => _fov;
            set { _fov = value; if (_renderCamera is not null) _renderCamera.Camera.Fov = value; }
        }

        [ToStorage]
        public float NearPlane
        {
            get => _nearPlane;
            set { _nearPlane = value; if (_renderCamera is not null) _renderCamera.Camera.NearPlane = value; }
        }

        [ToStorage]
        public float FarPlane
        {
            get => _farPlane;
            set { _farPlane = value; if (_renderCamera is not null) _renderCamera.Camera.FarPlane = value; }
        }

        [ToStorage]
        public float OrthoSize
        {
            get => _orthoSize;
            set { _orthoSize = value; if (_renderCamera is not null) _renderCamera.Camera.OrthoSize = value; }
        }

        [ToStorage]
        public float AspectRatio
        {
            get => _aspectRatio;
            set
            {
                _aspectRatio = value;
                if (_renderCamera is not null)
                    _renderCamera.Camera.AspectRatio = value > 0 ? value : 16f / 9f;
            }
        }

        // ── Clear ────────────────────────────────────────────────────────────

        [ToStorage]
        public CameraClearMode ClearMode
        {
            get => _clearMode;
            set { _clearMode = value; if (_renderCamera is not null) _renderCamera.ClearMode = value; }
        }

        [ToStorage]
        public Vector4 ClearColor
        {
            get => _clearColor;
            set
            {
                _clearColor = value;
                if (_renderCamera is not null)
                    _renderCamera.ClearColor = new Color4(value.X, value.Y, value.Z, value.W);
            }
        }

        // ── Viewport ─────────────────────────────────────────────────────────

        [ToStorage]
        public float ViewportX
        {
            get => _viewportX;
            set { _viewportX = value; if (_renderCamera is not null) _renderCamera.ViewportX = value; }
        }

        [ToStorage]
        public float ViewportY
        {
            get => _viewportY;
            set { _viewportY = value; if (_renderCamera is not null) _renderCamera.ViewportY = value; }
        }

        [ToStorage]
        public float ViewportWidth
        {
            get => _viewportWidth;
            set { _viewportWidth = value; if (_renderCamera is not null) _renderCamera.ViewportWidth = value; }
        }

        [ToStorage]
        public float ViewportHeight
        {
            get => _viewportHeight;
            set { _viewportHeight = value; if (_renderCamera is not null) _renderCamera.ViewportHeight = value; }
        }

        // ── Estado interno ───────────────────────────────────────────────────
        private RenderCamera? _renderCamera;
        private bool _isRegistered = false;

        public RenderCamera? RenderCamera => _renderCamera;

        private string EffectiveName
            => string.IsNullOrEmpty(_cameraName)
                ? (GameObject?.Name ?? $"Camera_{GetHashCode()}")
                : _cameraName;

        // ── Ciclo de vida ────────────────────────────────────────────────────

        public override void Awake()
        {
            Console.WriteLine($"[CameraComponent] Awake en '{GameObject?.Name}' — esperando Start");
        }

        /// <summary>
        /// Start: los [ToStorage] ya están deserializados, se crea la cámara
        /// con todos los valores correctos del archivo.
        /// </summary>
        public override void Start()
        {
            CreateAndRegisterCamera();
            SyncTransform();
            Console.WriteLine($"[CameraComponent] Start completado - Cámara: '{EffectiveName}'");
        }

        public override void OnWillRenderObject()
        {
            if (_renderCamera is null) return;
            SyncTransform();
        }

        public override void OnDestroy()
        {
            UnregisterCamera();
        }

        // ── API pública ──────────────────────────────────────────────────────

        public FrameBuffer? GetFrameBuffer() => _renderCamera?.GetTargetBuffer();
        public int GetTextureId() => GetFrameBuffer()?.ColorTexture ?? 0;

        public void SetAsMain()
        {
            if (_renderCamera is not null)
                CameraManager.Instance.SetMain(_renderCamera.Name);
        }

        public void ResizeBuffer(int width, int height)
        {
            // Usar las propiedades para que queden en sync los backing fields
            BufferWidth = width;
            BufferHeight = height;
        }

        /// <summary>
        /// Fuerza la recreación completa de la cámara y su buffer.
        /// Necesario solo al cambiar CameraName, RenderToBuffer, UseGBuffer o UseEmission.
        /// </summary>
        public void Rebuild()
        {
            UnregisterCamera();
            CreateAndRegisterCamera();
            SyncTransform();
        }

        // ── Privados ─────────────────────────────────────────────────────────
        private void CreateAndRegisterCamera()
        {
            var name = EffectiveName;

            if (CameraManager.Instance.Has(name))
            {
                Console.WriteLine($"[CameraComponent] Cámara '{name}' ya existe, reemplazando");
                CameraManager.Instance.Remove(name);
            }

            var position = GetPosition();
            var aspectRatio = _aspectRatio > 0 ? _aspectRatio : 16f / 9f;

            _renderCamera = CameraManager.Instance.Create(name, position, aspectRatio, _priority);

            // Aplicar toda la configuración desde los backing fields
            ApplyAllSettings();

            _isRegistered = true;
            Console.WriteLine($"[CameraComponent] Cámara '{name}' registrada (priority={_priority})");
        }

        /// <summary>Aplica todos los backing fields a la RenderCamera de una vez.</summary>
        private void ApplyAllSettings()
        {
            if (_renderCamera is null) return;

            // Proyección
            var cam = _renderCamera.Camera;
            cam.SetProjectionMode(_projectionMode);
            cam.Fov = _fov;
            cam.NearPlane = _nearPlane;
            cam.FarPlane = _farPlane;
            cam.OrthoSize = _orthoSize;
            cam.AspectRatio = _aspectRatio > 0 ? _aspectRatio : 16f / 9f;

            // Clear
            _renderCamera.ClearMode = _clearMode;
            _renderCamera.ClearColor = new Color4(_clearColor.X, _clearColor.Y, _clearColor.Z, _clearColor.W);

            // Viewport
            _renderCamera.ViewportX = _viewportX;
            _renderCamera.ViewportY = _viewportY;
            _renderCamera.ViewportWidth = _viewportWidth;
            _renderCamera.ViewportHeight = _viewportHeight;

            // Priority
            _renderCamera.Priority = _priority;

            // Buffer
            if (_renderToBuffer)
            {
                _renderCamera.CreateOwnBuffer(_bufferWidth, _bufferHeight, _useEmission, _useGBuffer);
                Console.WriteLine($"[CameraComponent] Buffer creado: {_bufferWidth}x{_bufferHeight}");
            }
        }

        private void SyncTransform()
        {
            if (_renderCamera is null) return;

            var transform = GameObject?.GetComponent<Transform>();
            if (transform is null) return;

            var cam = _renderCamera.Camera;
            cam.Position = transform.GetWorldPosition();

            Vector3 forward = Vector3.Normalize(transform.Forward);

            float pitch = MathHelper.RadiansToDegrees(
                MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f))
            );
            float yaw = MathHelper.RadiansToDegrees(
                MathF.Atan2(forward.Z, forward.X)
            );

            cam.Yaw = yaw;
            cam.Pitch = pitch;
        }

        private Vector3 GetPosition()
            => GameObject?.GetComponent<Transform>()?.GetWorldPosition() ?? Vector3.Zero;

        private void UnregisterCamera()
        {
            if (_renderCamera is null || !_isRegistered) return;

            CameraManager.Instance.Remove(_renderCamera.Name);
            _renderCamera = null;
            _isRegistered = false;

            Console.WriteLine($"[CameraComponent] Cámara '{EffectiveName}' eliminada");
        }
    }
}