using KrayonCore.Core;
using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class CameraComponent : Component
    {
        // ── Serialización ────────────────────────────────────────────────────
        [ToStorage] public string CameraName { get; set; } = "";
        [ToStorage] public int Priority { get; set; } = 0;
        [ToStorage] public bool RenderToBuffer { get; set; } = true;
        [ToStorage] public bool UseGBuffer { get; set; } = true;
        [ToStorage] public bool UseEmission { get; set; } = true;
        [ToStorage] public int BufferWidth { get; set; } = 1280;
        [ToStorage] public int BufferHeight { get; set; } = 720;

        // ── Proyección ───────────────────────────────────────────────────────
        [ToStorage] public ProjectionMode ProjectionMode { get; set; } = ProjectionMode.Perspective;
        [ToStorage] public float Fov { get; set; } = 45.0f;
        [ToStorage] public float NearPlane { get; set; } = 0.1f;
        [ToStorage] public float FarPlane { get; set; } = 1000.0f;
        [ToStorage] public float OrthoSize { get; set; } = 10.0f;
        [ToStorage] public float AspectRatio { get; set; } = 16f / 9f;

        // ── Clear ────────────────────────────────────────────────────────────
        [ToStorage] public CameraClearMode ClearMode { get; set; } = CameraClearMode.SolidColor;
        [ToStorage] public Vector4 ClearColor { get; set; } = new Vector4(0.1f, 0.1f, 0.1f, 1f);

        // ── Viewport normalizado (0-1) ────────────────────────────────────────
        [ToStorage] public float ViewportX { get; set; } = 0f;
        [ToStorage] public float ViewportY { get; set; } = 0f;
        [ToStorage] public float ViewportWidth { get; set; } = 1f;
        [ToStorage] public float ViewportHeight { get; set; } = 1f;

        // ── Estado interno ───────────────────────────────────────────────────
        private RenderCamera? _renderCamera;
        private bool _isRegistered = false;

        public RenderCamera? RenderCamera => _renderCamera;

        /// <summary>Nombre efectivo: si CameraName está vacío usa el del GameObject.</summary>
        private string EffectiveName
            => string.IsNullOrEmpty(CameraName)
                ? (GameObject?.Name ?? $"Camera_{GetHashCode()}")
                : CameraName;

        // ── Ciclo de vida ────────────────────────────────────────────────────
        public override void Awake()
        {
            Console.WriteLine($"[CameraComponent] Awake en '{GameObject?.Name}'");
            CreateAndRegisterCamera();
        }

        public override void Start()
        {
            if (!_isRegistered)
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

        /// <summary>Devuelve el buffer de esta cámara o null si no tiene uno dedicado.</summary>
        public FrameBuffer? GetFrameBuffer()
            => _renderCamera?.GetTargetBuffer();

        /// <summary>Devuelve el ID de textura del buffer para mostrar en ImGui.</summary>
        public int GetTextureId()
            => GetFrameBuffer()?.ColorTexture ?? 0;

        /// <summary>Fuerza a esta cámara como la principal del sistema.</summary>
        public void SetAsMain()
        {
            if (_renderCamera is not null)
                CameraManager.Instance.SetMain(_renderCamera.Name);
        }

        /// <summary>Redimensiona el buffer dedicado de esta cámara.</summary>
        public void ResizeBuffer(int width, int height)
        {
            BufferWidth = width;
            BufferHeight = height;
            _renderCamera?.ResizeBuffer(width, height);
        }

        /// <summary>Recrea la cámara con la configuración actual (útil tras cambios en el editor).</summary>
        public void Rebuild()
        {
            UnregisterCamera();
            CreateAndRegisterCamera();
        }

        // ── Privados ─────────────────────────────────────────────────────────
        private void CreateAndRegisterCamera()
        {
            var position = GetPosition();
            var aspectRatio = AspectRatio > 0 ? AspectRatio : 16f / 9f;
            var name = EffectiveName;

            // Si ya existe una cámara con este nombre la reemplazamos
            if (CameraManager.Instance.Has(name))
            {
                Console.WriteLine($"[CameraComponent] Cámara '{name}' ya existe, reemplazando");
                CameraManager.Instance.Remove(name);
            }

            _renderCamera = CameraManager.Instance.Create(name, position, aspectRatio, Priority);

            // Proyección
            ApplyProjectionSettings();

            // Clear
            _renderCamera.ClearMode = ClearMode;
            _renderCamera.ClearColor = new Color4(
                ClearColor.X, ClearColor.Y, ClearColor.Z, ClearColor.W);

            // Viewport
            _renderCamera.ViewportX = ViewportX;
            _renderCamera.ViewportY = ViewportY;
            _renderCamera.ViewportWidth = ViewportWidth;
            _renderCamera.ViewportHeight = ViewportHeight;

            // Buffer dedicado
            if (RenderToBuffer)
            {
                _renderCamera.CreateOwnBuffer(
                    BufferWidth, BufferHeight, UseEmission, UseGBuffer);

                Console.WriteLine(
                    $"[CameraComponent] Buffer creado para '{name}': {BufferWidth}x{BufferHeight}");
            }

            _isRegistered = true;
            Console.WriteLine($"[CameraComponent] Cámara '{name}' registrada (priority={Priority})");
        }

        private void ApplyProjectionSettings()
        {
            if (_renderCamera is null) return;

            var cam = _renderCamera.Camera;
            cam.SetProjectionMode(ProjectionMode);
            cam.Fov = Fov;
            cam.NearPlane = NearPlane;
            cam.FarPlane = FarPlane;
            cam.OrthoSize = OrthoSize;
            cam.AspectRatio = AspectRatio > 0 ? AspectRatio : 16f / 9f;
        }

        private void SyncTransform()
        {
            if (_renderCamera is null) return;

            var transform = GameObject?.GetComponent<Transform>();
            if (transform is null) return;

            var cam = _renderCamera.Camera;

            // ── Posición ─────────────────────────────────────────────────────────
            cam.Position = transform.GetWorldPosition();

            // ── Rotación: Quaternion → yaw/pitch ─────────────────────────────────
            // Extraemos el vector Forward del transform y calculamos yaw y pitch
            Vector3 forward = transform.Forward;

            // Pitch = ángulo vertical respecto al plano XZ
            float pitch = MathHelper.RadiansToDegrees(
                MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f))
            );

            // Yaw = ángulo horizontal en el plano XZ
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