using OpenTK.Graphics.OpenGL4;

namespace KrayonCore.Graphics.FrameBuffers
{
    public sealed class FrameBuffer : IDisposable
    {
        // ── GL handles ───────────────────────────────────────────────────────
        private int _fbo;
        private int _colorTexture;
        private int _emissionTexture;
        private int _positionTexture;
        private int _normalTexture;
        private int _rbo;

        // ── Configuración ────────────────────────────────────────────────────
        private readonly bool _useEmission;
        private readonly bool _useGBuffer;
        private bool _disposed;

        // ── Dimensiones ──────────────────────────────────────────────────────
        public int Width { get; private set; }
        public int Height { get; private set; }

        // ── Texturas ─────────────────────────────────────────────────────────
        public int ColorTexture => _colorTexture;
        public int EmissionTexture => _emissionTexture;
        public int PositionTexture => _positionTexture;
        public int NormalTexture => _normalTexture;
        public bool UseGBuffer => _useGBuffer;

        // ── Constructor ──────────────────────────────────────────────────────
        public FrameBuffer(int width, int height, bool useEmission = false, bool useGBuffer = false)
        {
            Width = width;
            Height = height;
            _useEmission = useEmission;
            _useGBuffer = useGBuffer;
            Build();
        }

        // ── API pública ──────────────────────────────────────────────────────
        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.Viewport(0, 0, Width, Height);
        }

        public void Unbind()
            => GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (width == Width && height == Height) return;

            Width = width;
            Height = height;

            ResizeTexture(_colorTexture, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, width, height);

            if (_useEmission && _emissionTexture != 0)
                ResizeTexture(_emissionTexture, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, width, height);

            if (_useGBuffer)
            {
                if (_positionTexture != 0)
                    ResizeTexture(_positionTexture, PixelInternalFormat.Rgb32f, PixelFormat.Rgb, width, height);
                if (_normalTexture != 0)
                    ResizeTexture(_normalTexture, PixelInternalFormat.Rgb16f, PixelFormat.Rgb, width, height);
            }

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.Depth24Stencil8, width, height);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GL.DeleteFramebuffer(_fbo);
            GL.DeleteTexture(_colorTexture);

            if (_useEmission && _emissionTexture != 0)
                GL.DeleteTexture(_emissionTexture);

            if (_useGBuffer)
            {
                if (_positionTexture != 0) GL.DeleteTexture(_positionTexture);
                if (_normalTexture != 0) GL.DeleteTexture(_normalTexture);
            }

            GL.DeleteRenderbuffer(_rbo);
        }

        // ── Construcción interna ─────────────────────────────────────────────
        private void Build()
        {
            _fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            var drawBuffers = new List<DrawBuffersEnum>();

            // Color
            _colorTexture = CreateTexture(PixelInternalFormat.Rgba16f, PixelFormat.Rgba,
                TextureMinFilter.Linear, TextureWrapMode.ClampToEdge);
            Attach(_colorTexture, FramebufferAttachment.ColorAttachment0);
            drawBuffers.Add(DrawBuffersEnum.ColorAttachment0);

            // Emission
            if (_useEmission)
            {
                _emissionTexture = CreateTexture(PixelInternalFormat.Rgba16f, PixelFormat.Rgba,
                    TextureMinFilter.Linear, TextureWrapMode.ClampToEdge);
                Attach(_emissionTexture, FramebufferAttachment.ColorAttachment1);
                drawBuffers.Add(DrawBuffersEnum.ColorAttachment1);
            }

            // GBuffer (posición + normal)
            if (_useGBuffer)
            {
                int posSlot = _useEmission ? 2 : 1;
                int normSlot = _useEmission ? 3 : 2;

                _positionTexture = CreateTexture(PixelInternalFormat.Rgb32f, PixelFormat.Rgb,
                    TextureMinFilter.Nearest, TextureWrapMode.ClampToEdge);
                Attach(_positionTexture, FramebufferAttachment.ColorAttachment0 + posSlot);
                drawBuffers.Add((DrawBuffersEnum)(DrawBuffersEnum.ColorAttachment0 + posSlot));

                _normalTexture = CreateTexture(PixelInternalFormat.Rgb16f, PixelFormat.Rgb,
                    TextureMinFilter.Nearest, TextureWrapMode.ClampToEdge);
                Attach(_normalTexture, FramebufferAttachment.ColorAttachment0 + normSlot);
                drawBuffers.Add((DrawBuffersEnum)(DrawBuffersEnum.ColorAttachment0 + normSlot));
            }

            GL.DrawBuffers(drawBuffers.Count, drawBuffers.ToArray());

            // Depth + stencil
            _rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.Depth24Stencil8, Width, Height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, _rbo);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                throw new Exception($"[FrameBuffer] Incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private int CreateTexture(PixelInternalFormat internalFmt, PixelFormat fmt,
            TextureMinFilter filter, TextureWrapMode wrap)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, internalFmt, Width, Height,
                0, fmt, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                filter == TextureMinFilter.Nearest ? (int)TextureMagFilter.Nearest : (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
            return tex;
        }

        private void Attach(int texture, FramebufferAttachment attachment)
            => GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment,
                TextureTarget.Texture2D, texture, 0);

        private static void ResizeTexture(int texture, PixelInternalFormat internalFmt,
            PixelFormat fmt, int width, int height)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, internalFmt, width, height,
                0, fmt, PixelType.Float, IntPtr.Zero);
        }
    }
}