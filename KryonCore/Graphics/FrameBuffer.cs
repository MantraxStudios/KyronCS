using OpenTK.Graphics.OpenGL4;

namespace KrayonCore
{
    public class FrameBuffer
    {
        private int _fbo;
        private int _colorTexture;
        private int _emissionTexture;
        private int _positionTexture;
        private int _normalTexture;
        private int _rbo;
        private int _width;
        private int _height;
        private bool _useEmission;
        private bool _useGBuffer;

        public int TextureId => _colorTexture;
        public int ColorTexture => _colorTexture;
        public int EmissionTexture => _emissionTexture;
        public int PositionTexture => _positionTexture;
        public int NormalTexture => _normalTexture;
        public int Width => _width;
        public int Height => _height;
        public bool UseGBuffer => _useGBuffer;

        public FrameBuffer(int width, int height, bool useEmission = false, bool useGBuffer = false)
        {
            _width = width;
            _height = height;
            _useEmission = useEmission;
            _useGBuffer = useGBuffer;
            CreateFramebuffer();
        }

        private void CreateFramebuffer()
        {
            _fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);

            _colorTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0,
                PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _colorTexture, 0);

            List<DrawBuffersEnum> drawBuffers = new List<DrawBuffersEnum> { DrawBuffersEnum.ColorAttachment0 };

            if (_useEmission)
            {
                _emissionTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _emissionTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, _width, _height, 0,
                    PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1,
                    TextureTarget.Texture2D, _emissionTexture, 0);
                drawBuffers.Add(DrawBuffersEnum.ColorAttachment1);
            }

            if (_useGBuffer)
            {
                _positionTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _positionTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, _width, _height, 0,
                    PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                
                int posAttachment = _useEmission ? 2 : 1;
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + posAttachment,
                    TextureTarget.Texture2D, _positionTexture, 0);
                drawBuffers.Add((DrawBuffersEnum)(DrawBuffersEnum.ColorAttachment0 + posAttachment));

                _normalTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, _normalTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, _width, _height, 0,
                    PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                
                int normAttachment = _useEmission ? 3 : 2;
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + normAttachment,
                    TextureTarget.Texture2D, _normalTexture, 0);
                drawBuffers.Add((DrawBuffersEnum)(DrawBuffersEnum.ColorAttachment0 + normAttachment));
            }

            GL.DrawBuffers(drawBuffers.Count, drawBuffers.ToArray());

            _rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _width, _height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, _rbo);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"[FrameBuffer] Error: {status}");
                throw new Exception($"Framebuffer is not complete! Status: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            Console.WriteLine($"[FrameBuffer] Created: Color={_colorTexture}, Emission={_emissionTexture}, Position={_positionTexture}, Normal={_normalTexture}");
        }

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
            GL.Viewport(0, 0, _width, _height);
        }

        public void Unbind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (width == _width && height == _height) return;

            _width = width;
            _height = height;

            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, width, height, 0,
                PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            if (_useEmission && _emissionTexture != 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, _emissionTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, width, height, 0,
                    PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            }

            if (_useGBuffer)
            {
                if (_positionTexture != 0)
                {
                    GL.BindTexture(TextureTarget.Texture2D, _positionTexture);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, width, height, 0,
                        PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
                }

                if (_normalTexture != 0)
                {
                    GL.BindTexture(TextureTarget.Texture2D, _normalTexture);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb16f, width, height, 0,
                        PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
                }
            }

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, width, height);
        }

        public int GetColorTexture()
        {
            return _colorTexture;
        }

        public void Dispose()
        {
            GL.DeleteFramebuffer(_fbo);
            GL.DeleteTexture(_colorTexture);
            if (_useEmission && _emissionTexture != 0)
                GL.DeleteTexture(_emissionTexture);
            if (_useGBuffer)
            {
                if (_positionTexture != 0)
                    GL.DeleteTexture(_positionTexture);
                if (_normalTexture != 0)
                    GL.DeleteTexture(_normalTexture);
            }
            GL.DeleteRenderbuffer(_rbo);
        }
    }
}