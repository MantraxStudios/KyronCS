using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.GraphicsData;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public enum CameraClearMode { SolidColor, DontClear }

    public sealed class RenderCamera : IDisposable
    {
        // ── Identidad ────────────────────────────────────────────────────────
        public string Name { get; }
        public int Priority { get; set; }
        public bool Enabled { get; set; } = true;

        // ── Cámara interna ───────────────────────────────────────────────────
        public Camera Camera { get; }

        // ── Buffers ──────────────────────────────────────────────────────────
        public string? TargetBufferName { get; private set; }
        public string? PostProcessBufferName { get; private set; }

        // ── Post proceso ─────────────────────────────────────────────────────
        public bool PostProcessingEnabled { get; set; } = true;

        // ── Clear ────────────────────────────────────────────────────────────
        public CameraClearMode ClearMode { get; set; } = CameraClearMode.SolidColor;
        public Color4 ClearColor { get; set; } = new Color4(0.1f, 0.1f, 0.1f, 1f);

        // ── Viewport normalizado (0-1) ────────────────────────────────────────
        public float ViewportX { get; set; } = 0f;
        public float ViewportY { get; set; } = 0f;
        public float ViewportWidth { get; set; } = 1f;
        public float ViewportHeight { get; set; } = 1f;

        // ── Constructor ──────────────────────────────────────────────────────
        public RenderCamera(string name, Vector3 position, float aspectRatio, int priority = 0)
        {
            Name = name;
            Priority = priority;
            Camera = new Camera(position, aspectRatio);
        }

        // ── Buffer de escena ─────────────────────────────────────────────────

        /// <summary>
        /// Crea un framebuffer de escena dedicado (con GBuffer y emisión)
        /// y uno de post-proceso para esta cámara.
        /// </summary>
        public void CreateOwnBuffer(int width, int height,
            bool useEmission = true, bool useGBuffer = true)
        {
            TargetBufferName = $"cam_{Name}_scene";
            PostProcessBufferName = $"cam_{Name}_pp";

            FrameBufferManager.Instance.Create(
                TargetBufferName, width, height, useEmission, useGBuffer);

            FrameBufferManager.Instance.Create(
                PostProcessBufferName, width, height,
                useEmission: false, useGBuffer: false);
        }

        /// <summary>Apunta esta cámara a un buffer de escena existente por nombre.</summary>
        public void SetTargetBuffer(string bufferName, string? postProcessBufferName = null)
        {
            TargetBufferName = bufferName;
            PostProcessBufferName = postProcessBufferName;
        }

        // ── Acceso a buffers ─────────────────────────────────────────────────

        /// <summary>Buffer de escena (con GBuffer). Aquí renderiza el SceneRenderer.</summary>
        public FrameBuffer? GetTargetBuffer()
            => TargetBufferName is not null
                ? FrameBufferManager.Instance.TryGet(TargetBufferName)
                : null;

        /// <summary>Buffer de post-proceso. Resultado final que se muestra.</summary>
        public FrameBuffer? GetPostProcessBuffer()
            => PostProcessBufferName is not null
                ? FrameBufferManager.Instance.TryGet(PostProcessBufferName)
                : null;

        /// <summary>
        /// Textura final a mostrar: post-proceso si está habilitado y existe,
        /// o el buffer de escena como fallback.
        /// </summary>
        public int GetFinalTextureId(bool globalPostProcessEnabled)
        {
            if (globalPostProcessEnabled && PostProcessingEnabled)
            {
                var ppBuffer = GetPostProcessBuffer();
                if (ppBuffer is not null) return ppBuffer.ColorTexture;
            }

            return GetTargetBuffer()?.ColorTexture ?? 0;
        }

        public void ResizeBuffer(int width, int height)
        {
            if (TargetBufferName is not null)
                FrameBufferManager.Instance.Resize(TargetBufferName, width, height);

            if (PostProcessBufferName is not null)
                FrameBufferManager.Instance.Resize(PostProcessBufferName, width, height);
        }

        public void Dispose()
        {
            if (TargetBufferName is not null)
                FrameBufferManager.Instance.Remove(TargetBufferName);

            if (PostProcessBufferName is not null)
                FrameBufferManager.Instance.Remove(PostProcessBufferName);
        }
    }
}