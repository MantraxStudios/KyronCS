using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Owns the compiled UI shader and the unit-quad geometry.
    /// Supports solid rects, rounded rects, vertical gradients, and textured quads.
    /// Works in reference-resolution coordinates — UICanvas handles viewport scaling.
    /// </summary>
    public sealed class UIBatch : IDisposable
    {
        // ── GL handles ────────────────────────────────────────────────────
        private int _vao, _vbo;
        private int _program;

        // ── Uniform locations ─────────────────────────────────────────────
        private int _locProjection;
        private int _locPosition, _locSize, _locRotation;
        private int _locColor, _locGradientColor;
        private int _locUseTexture, _locUseGradient;
        private int _locTexture;
        private int _locCornerRadius;

        private Matrix4 _projection;

        private static readonly float[] QuadVerts =
        {
            0f, 0f,  0f, 0f,
            1f, 0f,  1f, 0f,
            1f, 1f,  1f, 1f,
            0f, 0f,  0f, 0f,
            1f, 1f,  1f, 1f,
            0f, 1f,  0f, 1f,
        };

        public UIBatch() { BuildShader(); BuildQuad(); }

        // ── Setup ─────────────────────────────────────────────────────────

        /// <summary>Sets the ortho projection to [0,refW]×[0,refH]. Call once (or when ref resolution changes).</summary>
        public void SetReferenceSize(float refW, float refH)
        {
            Matrix4.CreateOrthographicOffCenter(0f, refW, refH, 0f, -1f, 1f, out _projection);
        }

        // ── Draw API ──────────────────────────────────────────────────────

        /// <summary>Draws a solid axis-aligned rectangle (no rounding).</summary>
        public void DrawRect(Vector2 position, Vector2 size, Vector4 color, float rotationRad = 0f)
        {
            Prepare(position, size, color, rotationRad);
            GL.Uniform1(_locUseTexture, 0);
            GL.Uniform1(_locUseGradient, 0);
            GL.Uniform1(_locCornerRadius, 0f);
            Flush();
        }

        /// <summary>Draws a solid rectangle with rounded corners. cornerRadius is in reference pixels.</summary>
        public void DrawRoundedRect(Vector2 position, Vector2 size, Vector4 color,
                                    float cornerRadius = 6f, float rotationRad = 0f)
        {
            Prepare(position, size, color, rotationRad);
            GL.Uniform1(_locUseTexture, 0);
            GL.Uniform1(_locUseGradient, 0);
            GL.Uniform1(_locCornerRadius, cornerRadius);
            Flush();
        }

        /// <summary>
        /// Draws a vertically-gradient rounded rectangle.
        /// colorTop is at y=0, colorBottom is at y=Size.Y.
        /// </summary>
        public void DrawRoundedRectGradient(Vector2 position, Vector2 size,
                                            Vector4 colorTop, Vector4 colorBottom,
                                            float cornerRadius = 6f, float rotationRad = 0f)
        {
            Prepare(position, size, colorTop, rotationRad);
            GL.Uniform1(_locUseTexture, 0);
            GL.Uniform1(_locUseGradient, 1);
            GL.Uniform4(_locGradientColor, colorBottom.X, colorBottom.Y, colorBottom.Z, colorBottom.W);
            GL.Uniform1(_locCornerRadius, cornerRadius);
            Flush();
        }

        /// <summary>Draws an OpenGL texture as a rounded (or square) quad.</summary>
        public void DrawTexture(Vector2 position, Vector2 size, int textureId, Vector4 color,
                                float rotationRad = 0f, float cornerRadius = 0f)
        {
            Prepare(position, size, color, rotationRad);
            GL.Uniform1(_locUseTexture, 1);
            GL.Uniform1(_locUseGradient, 0);
            GL.Uniform1(_locCornerRadius, cornerRadius);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureId);
            GL.Uniform1(_locTexture, 0);
            Flush();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void Prepare(Vector2 pos, Vector2 size, Vector4 color, float rot)
        {
            GL.UseProgram(_program);
            GL.UniformMatrix4(_locProjection, false, ref _projection);
            GL.Uniform2(_locPosition, pos.X, pos.Y);
            GL.Uniform2(_locSize, size.X, size.Y);
            GL.Uniform1(_locRotation, rot);
            GL.Uniform4(_locColor, color.X, color.Y, color.Z, color.W);
            // Default gradient color to transparent (overridden by gradient calls)
            GL.Uniform4(_locGradientColor, 0f, 0f, 0f, 0f);
        }

        private void Flush()
        {
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        private void BuildShader()
        {
            int vert = Compile(ShaderType.VertexShader,   UIShaders.Vert);
            int frag = Compile(ShaderType.FragmentShader, UIShaders.Frag);

            _program = GL.CreateProgram();
            GL.AttachShader(_program, vert);
            GL.AttachShader(_program, frag);
            GL.LinkProgram(_program);

            GL.GetProgram(_program, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0)
                throw new InvalidOperationException($"[UIBatch] Link error:\n{GL.GetProgramInfoLog(_program)}");

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);

            _locProjection   = GL.GetUniformLocation(_program, "u_Projection");
            _locPosition     = GL.GetUniformLocation(_program, "u_Position");
            _locSize         = GL.GetUniformLocation(_program, "u_Size");
            _locRotation     = GL.GetUniformLocation(_program, "u_Rotation");
            _locColor        = GL.GetUniformLocation(_program, "u_Color");
            _locGradientColor= GL.GetUniformLocation(_program, "u_GradientColor");
            _locUseTexture   = GL.GetUniformLocation(_program, "u_UseTexture");
            _locUseGradient  = GL.GetUniformLocation(_program, "u_UseGradient");
            _locTexture      = GL.GetUniformLocation(_program, "u_Texture");
            _locCornerRadius = GL.GetUniformLocation(_program, "u_CornerRadius");
        }

        private static int Compile(ShaderType type, string src)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
                throw new InvalidOperationException($"[UIBatch] {type}:\n{GL.GetShaderInfoLog(id)}");
            return id;
        }

        private void BuildQuad()
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, QuadVerts.Length * sizeof(float),
                          QuadVerts, BufferUsageHint.StaticDraw);
            int stride = 4 * sizeof(float);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_vbo);
            GL.DeleteProgram(_program);
        }
    }
}
