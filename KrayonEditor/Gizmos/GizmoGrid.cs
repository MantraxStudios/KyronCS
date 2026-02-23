using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

public static class GizmoGrid
{
    private static int vao;
    private static int vbo;
    private static int shaderProgram;
    private static bool initialized = false;
    private static int lineCount = 0;

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

    private static void Initialize()
    {
        const int halfSize = 50;   // 100x100 unidades totales
        const int step = 1;

        var vertices = new List<float>();

        for (int i = -halfSize; i <= halfSize; i += step)
        {
            // Línea paralela al eje Z
            vertices.Add(i); vertices.Add(0f); vertices.Add(-halfSize);
            vertices.Add(i); vertices.Add(0f); vertices.Add(halfSize);

            // Línea paralela al eje X
            vertices.Add(-halfSize); vertices.Add(0f); vertices.Add(i);
            vertices.Add(halfSize); vertices.Add(0f); vertices.Add(i);
        }

        lineCount = vertices.Count / 3;

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();

        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

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

    public static void Draw(Matrix4 view, Matrix4 projection, Vector4? color = null, float lineWidth = 1.0f)
    {
        if (!initialized)
            Initialize();

        // La grid siempre en el origen del mundo
        Matrix4 model = Matrix4.Identity;
        Vector4 gridColor = color ?? new Vector4(0.3f, 0.3f, 0.3f, 1.0f);

        GL.UseProgram(shaderProgram);

        GL.UniformMatrix4(GL.GetUniformLocation(shaderProgram, "model"), false, ref model);
        GL.UniformMatrix4(GL.GetUniformLocation(shaderProgram, "view"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(shaderProgram, "projection"), false, ref projection);
        GL.Uniform4(GL.GetUniformLocation(shaderProgram, "color"), gridColor);

        GL.LineWidth(lineWidth);
        GL.Enable(EnableCap.DepthTest);

        GL.BindVertexArray(vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, lineCount);
        GL.BindVertexArray(0);
    }

    public static void Cleanup()
    {
        if (initialized)
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            GL.DeleteProgram(shaderProgram);
            initialized = false;
        }
    }
}