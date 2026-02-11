using OpenTK.Graphics.OpenGL4;

namespace KrayonCore.Core.Rendering
{
    public class ScreenQuad : IDisposable
    {
        private int _vao;
        private int _vbo;
        private Material? _material;

        private readonly float[] _vertices =
        {
            -1.0f,  1.0f,     0.0f, 1.0f,
            -1.0f, -1.0f,     0.0f, 0.0f,
             1.0f, -1.0f,     1.0f, 0.0f,

            -1.0f,  1.0f,     0.0f, 1.0f,
             1.0f, -1.0f,     1.0f, 0.0f,
             1.0f,  1.0f,     1.0f, 1.0f
        };

        public ScreenQuad()
        {
            Initialize();
        }

        private void Initialize()
        {
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float),
                         _vertices, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false,
                                  4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false,
                                  4 * sizeof(float), 2 * sizeof(float));

            GL.BindVertexArray(0);

            Console.WriteLine($"[ScreenQuad] Initialized");
        }

        public void SetMaterial(Material material)
        {
            _material = material;
        }

        public void Render(int textureId)
        {
            if (_material == null)
                return;

            bool depthTestEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool cullFaceEnabled = GL.IsEnabled(EnableCap.CullFace);

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            _material.Use();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            _material.SetInt("u_ScreenTexture", 0);

            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);

            if (depthTestEnabled)
                GL.Enable(EnableCap.DepthTest);
            if (cullFaceEnabled)
                GL.Enable(EnableCap.CullFace);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
        }
    }
}