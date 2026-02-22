using KrayonCore.Core.Rendering;
using LightingSystem;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore.Core.Rendering
{
    public sealed class ShadowManager : IDisposable
    {
        public int PointShadowMapSize { get; set; } = 1024;
        public int SpotShadowMapSize { get; set; } = 1024;

        public const int MaxShadowPointLights = 4;
        public const int MaxShadowSpotLights = 4;

        public const int PointShadowUnitBase = 7;
        public const int SpotShadowUnit = 11;

        private readonly int[] _pointFBOs = new int[MaxShadowPointLights];
        private readonly int[] _pointCubeMaps = new int[MaxShadowPointLights];
        private readonly int[] _pointDepthRBOs = new int[MaxShadowPointLights];
        private readonly float[] _pointFarPlanes = Enumerable.Repeat(15f, MaxShadowPointLights).ToArray();

        private int _spotFBO;
        private int _spotTexture;
        private int _spotDepthRBO;
        private Matrix4 _spotMatrix;
        private int _lastNumShadowPointLights = 0;

        private Shader _depthShader;
        private Shader _depthPointShader;

        // Debug visualization
        private Shader _debugShader;
        private int _debugFBO;
        private int _debugColorTexture;
        private int _debugQuadVAO;
        private int _debugQuadVBO;
        private int _debugQuadEBO;

        private bool _initialized;
        private bool _disposed;

        public void Initialize()
        {
            if (_initialized) return;

            CreatePointShadowMaps();
            CreateSpotShadowMap();
            LoadDepthShaders();
            CreateDebugResources();

            _initialized = true;
            Console.WriteLine("[ShadowManager] Initialized (simple mode)");
        }

        private void CreatePointShadowMaps()
        {
            for (int i = 0; i < MaxShadowPointLights; i++)
            {
                _pointCubeMaps[i] = GL.GenTexture();
                GL.BindTexture(TextureTarget.TextureCubeMap, _pointCubeMaps[i]);

                for (int face = 0; face < 6; face++)
                    // Use RGBA32f to store depth as color (sampleable cubemap)
                    GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, 0,
                        PixelInternalFormat.Rgba32f,
                        PointShadowMapSize, PointShadowMapSize, 0,
                        PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);

                _pointFBOs[i] = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pointFBOs[i]);

                // Depth renderbuffer necesario para que el depth test funcione en el shadow pass
                _pointDepthRBOs[i] = GL.GenRenderbuffer();
                GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _pointDepthRBOs[i]);
                GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24,
                    PointShadowMapSize, PointShadowMapSize);
                GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                    RenderbufferTarget.Renderbuffer, _pointDepthRBOs[i]);

                GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
                GL.ReadBuffer(ReadBufferMode.None);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }

        private void CreateSpotShadowMap()
        {
            _spotTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _spotTexture);
            // Use RGBA32f to store depth as color (sampleable texture)
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f,
                SpotShadowMapSize, SpotShadowMapSize, 0,
                PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 1f, 1f, 1f, 1f });

            _spotFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _spotFBO);
            // Attach as color attachment so shader can write depth as color
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _spotTexture, 0);

            // Depth renderbuffer necesario para que el depth test funcione en el shadow pass
            _spotDepthRBO = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _spotDepthRBO);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24,
                SpotShadowMapSize, SpotShadowMapSize);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, _spotDepthRBO);

            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void LoadDepthShaders()
        {
            _depthShader = new Shader("shadow_depth");
            _depthShader.LoadFromBaseName("shaders/shadow_depth");

            _depthPointShader = new Shader("shadow_depth_point");
            _depthPointShader.LoadFromBaseName("shaders/shadow_depth_point");
        }

        private void CreateDebugResources()
        {
            try
            {
                _debugShader = new Shader("debug_shadow_depth");
                _debugShader.LoadFromBaseName("shaders/debug_shadow_depth");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ShadowManager] Warning: Could not load debug shader: {ex.Message}");
                _debugShader = null;
            }

            // Create debug color texture (visualize depth)
            _debugColorTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _debugColorTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                256, 256, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Create debug FBO
            _debugFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _debugFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, _debugColorTexture, 0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine($"[ShadowManager] Debug FBO incomplete: {status}");
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            CreateDebugQuad();
        }

        private void CreateDebugQuad()
        {
            // Fullscreen quad vertices (position, texCoord)
            float[] vertices = new float[]
            {
                -1, -1, 0,  0, 0,
                 1, -1, 0,  1, 0,
                 1,  1, 0,  1, 1,
                -1,  1, 0,  0, 1,
            };
            uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };

            _debugQuadVAO = GL.GenVertexArray();
            _debugQuadVBO = GL.GenBuffer();
            _debugQuadEBO = GL.GenBuffer();

            GL.BindVertexArray(_debugQuadVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _debugQuadVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _debugQuadEBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            int stride = 5 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            GL.BindVertexArray(0);
            Console.WriteLine("[ShadowManager] Debug quad created: VAO=" + _debugQuadVAO + " VBO=" + _debugQuadVBO + " EBO=" + _debugQuadEBO);
        }

        private void RenderDebugVisualization()
        {
            if (_debugShader == null || _spotTexture == 0 || _debugFBO == 0)
            {
                Console.WriteLine($"[ShadowManager] Debug skip: shader={_debugShader != null}, spot={_spotTexture}, fbo={_debugFBO}");
                return;
            }

            GL.Viewport(0, 0, 256, 256);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _debugFBO);
            
            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            _debugShader.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _spotTexture);
            int loc = _debugShader.GetUniformLocation("u_DepthTexture");
            GL.Uniform1(loc, 0);

            GL.BindVertexArray(_debugQuadVAO);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void RenderShadows(
            LightManager lightManager,
            Camera camera,
            Action<Shader, Matrix4, Matrix4> renderDepth)
        {
            if (!_initialized || _depthShader is null || _depthPointShader is null)
                return;

            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1.1f, 4.0f);

            try
            {
                RenderPointShadows(lightManager, renderDepth);
                RenderSpotShadows(lightManager, renderDepth);
                RenderDebugVisualization();
            }
            finally
            {
                // Restaurar clear color a negro para que la escena principal lo sobreescriba correctamente
                GL.ClearColor(0f, 0f, 0f, 1f);
                GL.Disable(EnableCap.PolygonOffsetFill);
                GL.Enable(EnableCap.CullFace);
            }
        }

        private void RenderPointShadows(
            LightManager lightManager,
            Action<Shader, Matrix4, Matrix4> renderDepth)
        {
            var pointLights = lightManager.GetPointLights();

            // Usar contador de slots para que coincida con el orden que LightManager manda al shader
            // (luces con shadow first). Evita gaps en los slots que rompen el mapping shader<->shadowmap.
            int shadowSlot = 0;
            for (int i = 0; i < pointLights.Count && shadowSlot < MaxShadowPointLights; i++)
            {
                if (!pointLights[i].Enabled || !pointLights[i].CastShadows) continue;

                RenderPointCubeMap(pointLights[i], shadowSlot, renderDepth);
                shadowSlot++;
            }
            _lastNumShadowPointLights = shadowSlot;
        }

        private void RenderSpotShadows(
            LightManager lightManager,
            Action<Shader, Matrix4, Matrix4> renderDepth)
        {
            var spotLights = lightManager.GetSpotLights();

            for (int i = 0; i < spotLights.Count && i < MaxShadowSpotLights; i++)
            {
                if (!spotLights[i].Enabled || !spotLights[i].CastShadows) continue;

                RenderSpotShadowMap(spotLights[i], i, renderDepth);
            }
        }

        private void RenderPointCubeMap(
            PointLight light, int slot,
            Action<Shader, Matrix4, Matrix4> renderDepth)
        {
            float farPlane = light.ShadowFarPlane > 0f ? light.ShadowFarPlane : 15f;
            _pointFarPlanes[slot] = farPlane;

            Matrix4 shadowProj = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(90f), 1f, 0.1f, farPlane);

            Vector3 pos = light.Position;
            Matrix4[] faceViews =
            {
                Matrix4.LookAt(pos, pos + Vector3.UnitX,  -Vector3.UnitY),
                Matrix4.LookAt(pos, pos - Vector3.UnitX,  -Vector3.UnitY),
                Matrix4.LookAt(pos, pos + Vector3.UnitY,   Vector3.UnitZ),
                Matrix4.LookAt(pos, pos - Vector3.UnitY,  -Vector3.UnitZ),
                Matrix4.LookAt(pos, pos + Vector3.UnitZ,  -Vector3.UnitY),
                Matrix4.LookAt(pos, pos - Vector3.UnitZ,  -Vector3.UnitY),
            };

            GL.Viewport(0, 0, PointShadowMapSize, PointShadowMapSize);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pointFBOs[slot]);

            _depthPointShader.Use();
            GL.Uniform3(GL.GetUniformLocation(_depthPointShader.ProgramID, "u_LightPos"), pos);
            GL.Uniform1(GL.GetUniformLocation(_depthPointShader.ProgramID, "u_FarPlane"), farPlane);
            int matLoc = GL.GetUniformLocation(_depthPointShader.ProgramID, "u_LightSpaceMatrix");

            // (1,1,1,1) = distancia máxima → áreas sin geometría = sin sombra
            GL.ClearColor(1f, 1f, 1f, 1f);

            for (int face = 0; face < 6; face++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    TextureTarget.TextureCubeMapPositiveX + face,
                    _pointCubeMaps[slot], 0);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                Matrix4 faceMatrix = shadowProj * faceViews[face];
                GL.UniformMatrix4(matLoc, false, ref faceMatrix);
                renderDepth(_depthPointShader, faceViews[face], shadowProj);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void RenderSpotShadowMap(
            SpotLight light, int slot,
            Action<Shader, Matrix4, Matrix4> renderDepth)
        {
            float nearPlane = 0.1f;
            float farPlane = light.ShadowFarPlane > nearPlane ? light.ShadowFarPlane : 50f;

            float outerAngle = MathF.Max(light.OuterCutOff, MathHelper.DegreesToRadians(1f));
            float fovY = MathF.Min(outerAngle * 2f, MathHelper.DegreesToRadians(175f));

            Matrix4 lightProj = Matrix4.CreatePerspectiveFieldOfView(fovY, 1f, nearPlane, farPlane);

            Vector3 dir = Vector3.Normalize(light.Direction);
            Vector3 up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.99f
                ? Vector3.UnitZ
                : Vector3.UnitY;

            Matrix4 lightView = Matrix4.LookAt(light.Position, light.Position + dir, up);
            _spotMatrix = lightProj * lightView;

            GL.Viewport(0, 0, SpotShadowMapSize, SpotShadowMapSize);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _spotFBO);
            // (1,1,1,1) = distancia máxima → áreas sin geometría = sin sombra
            GL.ClearColor(1f, 1f, 1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _depthShader.Use();
            Matrix4 lsm = _spotMatrix;
            GL.UniformMatrix4(GL.GetUniformLocation(_depthShader.ProgramID, "u_LightSpaceMatrix"),
                false, ref lsm);
            renderDepth(_depthShader, lightView, lightProj);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void BindShadowsToShader(int programId)
        {
            if (!_initialized) return;

            for (int i = 0; i < MaxShadowPointLights; i++)
            {
                int unit = PointShadowUnitBase + i;
                GL.ActiveTexture(TextureUnit.Texture0 + unit);
                GL.BindTexture(TextureTarget.TextureCubeMap, _pointCubeMaps[i]);
                SetUniformInt(programId, $"u_PointShadowMaps[{i}]", unit);

                int loc = GL.GetUniformLocation(programId, $"u_PointLightFarPlanes[{i}]");
                if (loc >= 0) GL.Uniform1(loc, _pointFarPlanes[i]);
            }

            GL.ActiveTexture(TextureUnit.Texture0 + SpotShadowUnit);
            GL.BindTexture(TextureTarget.Texture2D, _spotTexture);
            SetUniformInt(programId, "u_SpotShadowMap", SpotShadowUnit);

            int spotMatLoc = GL.GetUniformLocation(programId, "u_SpotLightSpaceMatrices[0]");
            if (spotMatLoc >= 0)
            {
                GL.UniformMatrix4(spotMatLoc, false, ref _spotMatrix);
            }

            SetUniformInt(programId, "u_NumShadowPointLights", _lastNumShadowPointLights);
        }

        public int GetSpotShadowTextureId() => _debugColorTexture;
        public int GetPointCubeMapId(int idx)
        {
            if (idx < 0 || idx >= MaxShadowPointLights) return 0;
            return _pointCubeMaps[idx];
        }
        public float GetPointFarPlane(int idx)
        {
            if (idx < 0 || idx >= MaxShadowPointLights) return 0f;
            return _pointFarPlanes[idx];
        }

        private static void SetUniformInt(int program, string name, int value)
        {
            int loc = GL.GetUniformLocation(program, name);
            if (loc >= 0) GL.Uniform1(loc, value);
        }

        public void Dispose()
        {
            if (_disposed) return;

            for (int i = 0; i < MaxShadowPointLights; i++)
            {
                if (_pointFBOs[i] != 0) GL.DeleteFramebuffer(_pointFBOs[i]);
                if (_pointCubeMaps[i] != 0) GL.DeleteTexture(_pointCubeMaps[i]);
                if (_pointDepthRBOs[i] != 0) GL.DeleteRenderbuffer(_pointDepthRBOs[i]);
            }

            if (_spotFBO != 0) GL.DeleteFramebuffer(_spotFBO);
            if (_spotTexture != 0) GL.DeleteTexture(_spotTexture);
            if (_spotDepthRBO != 0) GL.DeleteRenderbuffer(_spotDepthRBO);

            if (_debugFBO != 0) GL.DeleteFramebuffer(_debugFBO);
            if (_debugColorTexture != 0) GL.DeleteTexture(_debugColorTexture);
            if (_debugQuadVAO != 0) GL.DeleteVertexArray(_debugQuadVAO);
            if (_debugQuadVBO != 0) GL.DeleteBuffer(_debugQuadVBO);
            if (_debugQuadEBO != 0) GL.DeleteBuffer(_debugQuadEBO);

            _depthShader?.Dispose();
            _depthPointShader?.Dispose();
            _debugShader?.Dispose();
            _disposed = true;
        }
    }
}