using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

public static class GizmoCube
{
    private static int vao;
    private static int vbo;
    private static int ebo;
    private static int shaderProgram;
    private static bool initialized = false;

    // Shader simple para el gizmo
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
        // 8 vértices del cubo (centrado en origen)
        float[] vertices = {
            // Cara trasera
            -0.5f, -0.5f, -0.5f,  // 0: Atrás inferior izquierda
             0.5f, -0.5f, -0.5f,  // 1: Atrás inferior derecha
             0.5f,  0.5f, -0.5f,  // 2: Atrás superior derecha
            -0.5f,  0.5f, -0.5f,  // 3: Atrás superior izquierda
            
            // Cara frontal
            -0.5f, -0.5f,  0.5f,  // 4: Frente inferior izquierda
             0.5f, -0.5f,  0.5f,  // 5: Frente inferior derecha
             0.5f,  0.5f,  0.5f,  // 6: Frente superior derecha
            -0.5f,  0.5f,  0.5f   // 7: Frente superior izquierda
        };

        // Índices para las 12 aristas del cubo
        uint[] indices = {
            // Cara trasera
            0, 1,  1, 2,  2, 3,  3, 0,
            // Cara frontal
            4, 5,  5, 6,  6, 7,  7, 4,
            // Conexiones entre caras
            0, 4,  1, 5,  2, 6,  3, 7
        };

        // Generar VAO, VBO y EBO
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        // VBO
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        // EBO
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        // Atributos
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

    public static void Draw(Matrix4 model, Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth = 2.0f)
    {
        if (!initialized)
            Initialize();

        GL.UseProgram(shaderProgram);

        // Establecer uniforms
        int modelLoc = GL.GetUniformLocation(shaderProgram, "model");
        int viewLoc = GL.GetUniformLocation(shaderProgram, "view");
        int projectionLoc = GL.GetUniformLocation(shaderProgram, "projection");
        int colorLoc = GL.GetUniformLocation(shaderProgram, "color");

        GL.UniformMatrix4(modelLoc, false, ref model);
        GL.UniformMatrix4(viewLoc, false, ref view);
        GL.UniformMatrix4(projectionLoc, false, ref projection);
        GL.Uniform4(colorLoc, color);

        // Configurar grosor de línea
        GL.LineWidth(lineWidth);

        // Habilitar depth test para gizmos 3D
        GL.Enable(EnableCap.DepthTest);

        // Dibujar el cubo (24 índices = 12 líneas)
        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Lines, 24, DrawElementsType.UnsignedInt, 0);
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