using OpenTK.Graphics.OpenGL4;

namespace KrayonCore.Core.Rendering
{
    public class FullscreenQuad : IDisposable
    {
        private int _vao;
        private int _vbo;
        private Material? _material;
        private PostProcessingSettings _settings;
        
        private readonly float[] _vertices = 
        {
            -1.0f,  1.0f,     0.0f, 1.0f,
            -1.0f, -1.0f,     0.0f, 0.0f,
             1.0f, -1.0f,     1.0f, 0.0f,
             
            -1.0f,  1.0f,     0.0f, 1.0f,
             1.0f, -1.0f,     1.0f, 0.0f,
             1.0f,  1.0f,     1.0f, 1.0f
        };

        public FullscreenQuad()
        {
            _settings = new PostProcessingSettings();
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

            Console.WriteLine($"[FullScreen Quad] Quad Started");
        }

        public void SetMaterial(Material material)
        {
            _material = material;
        }

        public PostProcessingSettings GetSettings()
        {
            return _settings;
        }

        public void Render(int textureId, float time, int width, int height)
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
            
            _material.SetFloat("u_Time", time);
            _material.SetVector2("u_Resolution", new OpenTK.Mathematics.Vector2(width, height));

            _material.SetInt("u_PostProcessEnabled", _settings.Enabled ? 1 : 0);

            _material.SetInt("u_ColorCorrectionEnabled", _settings.ColorCorrectionEnabled ? 1 : 0);
            _material.SetFloat("u_Brightness", _settings.Brightness);
            _material.SetFloat("u_Contrast", _settings.Contrast);
            _material.SetFloat("u_Saturation", _settings.Saturation);
            _material.SetVector3("u_ColorFilter", _settings.ColorFilter);

            _material.SetInt("u_BloomEnabled", _settings.BloomEnabled ? 1 : 0);
            _material.SetFloat("u_BloomThreshold", _settings.BloomThreshold);
            _material.SetFloat("u_BloomSoftThreshold", _settings.BloomSoftThreshold);
            _material.SetFloat("u_BloomIntensity", _settings.BloomIntensity);
            _material.SetFloat("u_BloomRadius", _settings.BloomRadius);

            _material.SetInt("u_GrainEnabled", _settings.GrainEnabled ? 1 : 0);
            _material.SetFloat("u_GrainIntensity", _settings.GrainIntensity);
            _material.SetFloat("u_GrainSize", _settings.GrainSize);

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