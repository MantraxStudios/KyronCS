using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

public static class GizmoCapsule
{
    private static int vao;
    private static int vbo;
    private static int ebo;
    private static int shaderProgram;
    private static bool initialized = false;
    private static int indexCount;

    private const string vertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
        }
    ";

    private const string fragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        uniform vec4 color;
        
        void main()
        {
            FragColor = color;
        }
    ";

    /// <summary>
    /// Genera la geometría de la cápsula en espacio local.
    /// - Radio = 0.5 (el model matrix se encarga del escalado)
    /// - Altura del cilindro central = 1.0  (entre los dos hemisferios)
    /// El total de la cápsula va de Y = -1.0 a Y = +1.0 (radio incluido).
    /// </summary>
    private static void Initialize()
    {
        const int segments = 32;   // Segmentos por círculo
        const int rings = 8;    // Anillos por semiesfera

        float radius = 0.5f;
        float halfHeight = 0.5f;   // Mitad del cilindro central

        var vertices = new List<float>();
        var indices = new List<uint>();

        // ── Hemisferio superior (+Y) ──────────────────────────────────────
        int topHemisphereStart = 0;
        for (int ring = 0; ring <= rings; ring++)
        {
            // theta va de 0 (polo norte) a PI/2 (ecuador)
            float theta = ring * MathF.PI * 0.5f / rings;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int seg = 0; seg <= segments; seg++)
            {
                float phi = seg * 2.0f * MathF.PI / segments;
                float x = MathF.Cos(phi) * sinTheta * radius;
                float y = cosTheta * radius + halfHeight;   // desplazado hacia arriba
                float z = MathF.Sin(phi) * sinTheta * radius;
                vertices.Add(x); vertices.Add(y); vertices.Add(z);
            }
        }

        // ── Hemisferio inferior (−Y) ──────────────────────────────────────
        int botHemisphereStart = (rings + 1) * (segments + 1);
        for (int ring = 0; ring <= rings; ring++)
        {
            // theta va de PI/2 (ecuador) a PI (polo sur)
            float theta = MathF.PI * 0.5f + ring * MathF.PI * 0.5f / rings;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int seg = 0; seg <= segments; seg++)
            {
                float phi = seg * 2.0f * MathF.PI / segments;
                float x = MathF.Cos(phi) * sinTheta * radius;
                float y = cosTheta * radius - halfHeight;   // desplazado hacia abajo
                float z = MathF.Sin(phi) * sinTheta * radius;
                vertices.Add(x); vertices.Add(y); vertices.Add(z);
            }
        }

        int stride = segments + 1;

        // Líneas de la malla — hemisferio superior
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                uint cur = (uint)(topHemisphereStart + ring * stride + seg);
                uint next = cur + (uint)stride;

                indices.Add(cur); indices.Add(next);      // vertical
                indices.Add(cur); indices.Add(cur + 1);   // horizontal
            }
        }

        // Líneas de la malla — hemisferio inferior
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                uint cur = (uint)(botHemisphereStart + ring * stride + seg);
                uint next = cur + (uint)stride;

                indices.Add(cur); indices.Add(next);
                indices.Add(cur); indices.Add(cur + 1);
            }
        }

        // ── Líneas verticales del cilindro central ────────────────────────
        // Une el ecuador del hemisferio superior con el del hemisferio inferior
        int topEquatorRing = rings; // último anillo del hemisferio superior (ecuador)
        int botEquatorRing = 0;     // primer anillo del hemisferio inferior (ecuador)

        for (int seg = 0; seg < segments; seg++)
        {
            uint topVert = (uint)(topHemisphereStart + topEquatorRing * stride + seg);
            uint botVert = (uint)(botHemisphereStart + botEquatorRing * stride + seg);
            indices.Add(topVert); indices.Add(botVert);
        }

        indexCount = indices.Count;

        // ── Crear buffers OpenGL ──────────────────────────────────────────
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // ── Compilar shaders ──────────────────────────────────────────────
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderSource);
        GL.CompileShader(vertexShader);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderSource);
        GL.CompileShader(fragmentShader);

        shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        GL.BindVertexArray(0);
        initialized = true;
    }

    /// <summary>
    /// Dibuja la cápsula.
    /// El model matrix debe escalar X/Z por el diámetro (radio * 2) e Y por la altura total.
    /// Ejemplo:
    ///   var rb = go.GetComponent&lt;Rigidbody&gt;();
    ///   // ShapeSize.X = radio, ShapeSize.Y = halfHeight del cilindro
    ///   float diameter   = rb.ShapeSize.X * 2f;
    ///   float totalHeight = rb.ShapeSize.Y * 2f + rb.ShapeSize.X * 2f; // cilindro + 2 semiesferas
    ///   Matrix4 model = Matrix4.CreateScale(diameter, totalHeight, diameter)
    ///                 * go.Transform.GetWorldMatrix();
    /// </summary>
    public static void Draw(Matrix4 model, Matrix4 view, Matrix4 projection,
                            Vector4 color, float lineWidth = 1.5f)
    {
        if (!initialized)
            Initialize();

        GL.UseProgram(shaderProgram);

        int modelLoc = GL.GetUniformLocation(shaderProgram, "model");
        int viewLoc = GL.GetUniformLocation(shaderProgram, "view");
        int projectionLoc = GL.GetUniformLocation(shaderProgram, "projection");
        int colorLoc = GL.GetUniformLocation(shaderProgram, "color");

        GL.UniformMatrix4(modelLoc, false, ref model);
        GL.UniformMatrix4(viewLoc, false, ref view);
        GL.UniformMatrix4(projectionLoc, false, ref projection);
        GL.Uniform4(colorLoc, color);

        GL.LineWidth(lineWidth);
        GL.Enable(EnableCap.DepthTest);

        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Lines, indexCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public static void Cleanup()
    {
        if (initialized)
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteBuffer(ebo);
            GL.DeleteProgram(shaderProgram);
            initialized = false;
        }
    }
}