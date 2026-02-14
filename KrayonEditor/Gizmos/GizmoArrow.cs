using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

public static class GizmoArrow
{
    private static int vao;
    private static int vbo;
    private static int ebo;
    private static int shaderProgram;
    private static bool initialized = false;

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
        float[] vertices = {
            // Línea principal
            0.0f, 0.0f, 0.0f,    // Base
            0.0f, 0.0f, 1.0f,    // Punta
            
            // Punta de flecha
            -0.1f, 0.0f, 0.8f,   // Izquierda
            0.1f, 0.0f, 0.8f,    // Derecha
            0.0f, -0.1f, 0.8f,   // Abajo
            0.0f, 0.1f, 0.8f     // Arriba
        };

        uint[] indices = {
            0, 1,  // Línea principal
            1, 2,  // Punta izquierda
            1, 3,  // Punta derecha
            1, 4,  // Punta abajo
            1, 5   // Punta arriba
        };

        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

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

    public static void Draw(Matrix4 model, Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth = 2.5f)
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
        GL.DrawElements(PrimitiveType.Lines, 10, DrawElementsType.UnsignedInt, 0);
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