using OpenTK.Graphics.OpenGL4;

namespace KrayonCore
{
    public class FrameBuffer
    {
        private int _fbo;
        private int _colorTexture;
        private int _emissionTexture;
        private int _rbo;
        private int _width;
        private int _height;
        private bool _useEmission;

        public int TextureId => _colorTexture;
        public int ColorTexture => _colorTexture;
        public int EmissionTexture => _emissionTexture;
        public int Width => _width;
        public int Height => _height;

        public FrameBuffer(int width, int height, bool useEmission = false)
        {
            _width = width;
            _height = height;
            _useEmission = useEmission;
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

                GL.DrawBuffers(2, new DrawBuffersEnum[] {
                    DrawBuffersEnum.ColorAttachment0,
                    DrawBuffersEnum.ColorAttachment1
                });
            }

            _rbo = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _rbo);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, _width, _height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment,
                RenderbufferTarget.Renderbuffer, _rbo);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Framebuffer is not complete!");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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

            if (_useEmission)
            {
                GL.BindTexture(TextureTarget.Texture2D, _emissionTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, width, height, 0,
                    PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
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
            if (_useEmission)
                GL.DeleteTexture(_emissionTexture);
            GL.DeleteRenderbuffer(_rbo);
        }
    }
}