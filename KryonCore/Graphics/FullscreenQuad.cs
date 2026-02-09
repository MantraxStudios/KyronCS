using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore.Core.Rendering
{
    public class FullscreenQuad : IDisposable
    {
        private int _vao;
        private int _vbo;
        private Material? _material;
        private PostProcessingSettings _settings;
        private int _noiseTexture;
        private int _dummyTexture;
        private Vector3[] _ssaoKernel;
        
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
            GenerateSSAOKernel();
            GenerateNoiseTexture();
            CreateDummyTexture();
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

        private void CreateDummyTexture()
        {
            _dummyTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _dummyTexture);
            float[] blackPixel = { 0f, 0f, 0f, 1f };
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0,
                PixelFormat.Rgba, PixelType.Float, blackPixel);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        }

        private void GenerateSSAOKernel()
        {
            Random rand = new Random(42);
            _ssaoKernel = new Vector3[64];

            for (int i = 0; i < 64; i++)
            {
                Vector3 sample = new Vector3(
                    (float)rand.NextDouble() * 2.0f - 1.0f,
                    (float)rand.NextDouble() * 2.0f - 1.0f,
                    (float)rand.NextDouble()
                );
                sample = Vector3.Normalize(sample);
                
                float scale = (float)i / 64.0f;
                scale = MathHelper.Lerp(0.1f, 1.0f, scale * scale);
                sample *= (float)rand.NextDouble() * scale;

                _ssaoKernel[i] = sample;
            }
        }

        private void GenerateNoiseTexture()
        {
            Random rand = new Random(42);
            Vector3[] noise = new Vector3[16];

            for (int i = 0; i < 16; i++)
            {
                noise[i] = Vector3.Normalize(new Vector3(
                    (float)rand.NextDouble() * 2.0f - 1.0f,
                    (float)rand.NextDouble() * 2.0f - 1.0f,
                    0.0f
                ));
            }

            _noiseTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);

            float[] noiseData = new float[16 * 3];
            for (int i = 0; i < 16; i++)
            {
                noiseData[i * 3 + 0] = noise[i].X;
                noiseData[i * 3 + 1] = noise[i].Y;
                noiseData[i * 3 + 2] = noise[i].Z;
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb32f, 4, 4, 0,
                PixelFormat.Rgb, PixelType.Float, noiseData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        }

        public void SetMaterial(Material material)
        {
            _material = material;
        }

        public PostProcessingSettings GetSettings()
        {
            return _settings;
        }

        public void Render(int colorTextureId, int emissionTextureId, int positionTextureId, int normalTextureId, 
                        float time, int width, int height, Matrix4 projection, Matrix4 view)
        {
            if (_material == null)
                return;

            bool depthTestEnabled = GL.IsEnabled(EnableCap.DepthTest);
            bool cullFaceEnabled = GL.IsEnabled(EnableCap.CullFace);

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            _material.Use();
            
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, colorTextureId);
            _material.SetInt("u_ScreenTexture", 0);
            
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, emissionTextureId != 0 ? emissionTextureId : _dummyTexture);
            _material.SetInt("u_EmissionTexture", 1);

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, positionTextureId != 0 ? positionTextureId : _dummyTexture);
            _material.SetInt("u_PositionTexture", 2);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, normalTextureId != 0 ? normalTextureId : _dummyTexture);
            _material.SetInt("u_NormalTexture", 3);

            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2D, _noiseTexture);
            _material.SetInt("u_NoiseTexture", 4);
            
            _material.SetFloat("u_Time", time);
            _material.SetVector2("u_Resolution", new Vector2(width, height));
            _material.SetMatrix4("u_Projection", projection);
            _material.SetMatrix4("u_View", view);

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

            bool ssaoEnabled = _settings.SSAOEnabled && positionTextureId != 0 && normalTextureId != 0;
            _material.SetInt("u_SSAOEnabled", ssaoEnabled ? 1 : 0);
            _material.SetInt("u_SSAOKernelSize", _settings.SSAOKernelSize);
            _material.SetFloat("u_SSAORadius", _settings.SSAORadius);
            _material.SetFloat("u_SSAOBias", _settings.SSAOBias);
            _material.SetFloat("u_SSAOPower", _settings.SSAOPower);

            for (int i = 0; i < _ssaoKernel.Length; i++)
            {
                _material.SetVector3($"u_SSAOKernel[{i}]", _ssaoKernel[i]);
            }

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
            GL.DeleteTexture(_noiseTexture);
            GL.DeleteTexture(_dummyTexture);
        }
    }
}