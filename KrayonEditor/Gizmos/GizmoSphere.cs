using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

public static class GizmoSphere
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

    private static void Initialize()
    {
        const int segments = 32; // Segmentos por círculo
        const int rings = 16;    // Número de anillos verticales

        var vertices = new List<float>();
        var indices = new List<uint>();

        // Generar vértices de la esfera
        for (int ring = 0; ring <= rings; ring++)
        {
            float theta = ring * MathF.PI / rings;
            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);

            for (int seg = 0; seg <= segments; seg++)
            {
                float phi = seg * 2.0f * MathF.PI / segments;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                float x = cosPhi * sinTheta;
                float y = cosTheta;
                float z = sinPhi * sinTheta;

                vertices.Add(x * 0.5f);
                vertices.Add(y * 0.5f);
                vertices.Add(z * 0.5f);
            }
        }

        // Generar índices para líneas
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                uint current = (uint)(ring * (segments + 1) + seg);
                uint next = current + (uint)segments + 1;

                // Línea vertical
                indices.Add(current);
                indices.Add(next);

                // Línea horizontal
                indices.Add(current);
                indices.Add(current + 1);
            }
        }

        indexCount = indices.Count;

        // Crear buffers
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Count * sizeof(float), vertices.ToArray(), BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Count * sizeof(uint), indices.ToArray(), BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // Compilar shaders
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

    public static void Draw(Matrix4 model, Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth = 1.5f)
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