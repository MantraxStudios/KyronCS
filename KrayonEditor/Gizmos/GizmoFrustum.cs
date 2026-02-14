using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

public static class GizmoFrustum
{
    private static int vao;
    private static int vbo;
    private static int ebo;
    private static int shaderProgram;
    private static bool initialized = false;

    private const string vertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        uniform mat4 view;
        uniform mat4 projection;
        
        void main()
        {
            gl_Position = projection * view * vec4(aPosition, 1.0);
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
        // Inicializar buffers vacíos (se actualizarán en Draw)
        vao = GL.GenVertexArray();
        vbo = GL.GenBuffer();
        ebo = GL.GenBuffer();

        GL.BindVertexArray(vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, 8 * 3 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        // Índices del frustum (12 aristas)
        uint[] indices = {
            // Near plane (rectángulo frontal)
            0, 1,  1, 2,  2, 3,  3, 0,
            // Far plane (rectángulo trasero)
            4, 5,  5, 6,  6, 7,  7, 4,
            // Conexiones near-far
            0, 4,  1, 5,  2, 6,  3, 7
        };

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

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

    public static void DrawPerspective(
        Vector3 position, Vector3 forward, Vector3 up,
        float fovDegrees, float aspectRatio, float nearPlane, float farPlane,
        Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth = 2.0f)
    {
        if (!initialized)
            Initialize();

        // Calcular los 8 vértices del frustum
        Vector3[] vertices = CalculatePerspectiveFrustumVertices(
            position, forward, up, fovDegrees, aspectRatio, nearPlane, farPlane);

        // Actualizar el VBO con los nuevos vértices
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        float[] vertexData = new float[24]; // 8 vértices * 3 componentes
        for (int i = 0; i < 8; i++)
        {
            vertexData[i * 3 + 0] = vertices[i].X;
            vertexData[i * 3 + 1] = vertices[i].Y;
            vertexData[i * 3 + 2] = vertices[i].Z;
        }
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, 24 * sizeof(float), vertexData);

        // Renderizar
        GL.UseProgram(shaderProgram);

        int viewLoc = GL.GetUniformLocation(shaderProgram, "view");
        int projectionLoc = GL.GetUniformLocation(shaderProgram, "projection");
        int colorLoc = GL.GetUniformLocation(shaderProgram, "color");

        GL.UniformMatrix4(viewLoc, false, ref view);
        GL.UniformMatrix4(projectionLoc, false, ref projection);
        GL.Uniform4(colorLoc, color);

        GL.LineWidth(lineWidth);
        GL.Enable(EnableCap.DepthTest);

        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Lines, 24, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public static void DrawOrthographic(
        Vector3 position, Vector3 forward, Vector3 up,
        float orthoSize, float aspectRatio, float nearPlane, float farPlane,
        Matrix4 view, Matrix4 projection, Vector4 color, float lineWidth = 2.0f)
    {
        if (!initialized)
            Initialize();

        // Calcular los 8 vértices del frustum ortográfico (caja rectangular)
        Vector3[] vertices = CalculateOrthographicFrustumVertices(
            position, forward, up, orthoSize, aspectRatio, nearPlane, farPlane);

        // Actualizar el VBO
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        float[] vertexData = new float[24];
        for (int i = 0; i < 8; i++)
        {
            vertexData[i * 3 + 0] = vertices[i].X;
            vertexData[i * 3 + 1] = vertices[i].Y;
            vertexData[i * 3 + 2] = vertices[i].Z;
        }
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, 24 * sizeof(float), vertexData);

        // Renderizar
        GL.UseProgram(shaderProgram);

        int viewLoc = GL.GetUniformLocation(shaderProgram, "view");
        int projectionLoc = GL.GetUniformLocation(shaderProgram, "projection");
        int colorLoc = GL.GetUniformLocation(shaderProgram, "color");

        GL.UniformMatrix4(viewLoc, false, ref view);
        GL.UniformMatrix4(projectionLoc, false, ref projection);
        GL.Uniform4(colorLoc, color);

        GL.LineWidth(lineWidth);
        GL.Enable(EnableCap.DepthTest);

        GL.BindVertexArray(vao);
        GL.DrawElements(PrimitiveType.Lines, 24, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    private static Vector3[] CalculatePerspectiveFrustumVertices(
        Vector3 position, Vector3 forward, Vector3 up,
        float fovDegrees, float aspectRatio, float nearPlane, float farPlane)
    {
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
        up = Vector3.Normalize(Vector3.Cross(right, forward));

        float fovRadians = MathHelper.DegreesToRadians(fovDegrees);

        // Near plane
        float nearHeight = 2.0f * MathF.Tan(fovRadians / 2.0f) * nearPlane;
        float nearWidth = nearHeight * aspectRatio;

        Vector3 nearCenter = position + forward * nearPlane;
        Vector3 nearTopLeft = nearCenter + up * (nearHeight / 2.0f) - right * (nearWidth / 2.0f);
        Vector3 nearTopRight = nearCenter + up * (nearHeight / 2.0f) + right * (nearWidth / 2.0f);
        Vector3 nearBottomRight = nearCenter - up * (nearHeight / 2.0f) + right * (nearWidth / 2.0f);
        Vector3 nearBottomLeft = nearCenter - up * (nearHeight / 2.0f) - right * (nearWidth / 2.0f);

        // Far plane
        float farHeight = 2.0f * MathF.Tan(fovRadians / 2.0f) * farPlane;
        float farWidth = farHeight * aspectRatio;

        Vector3 farCenter = position + forward * farPlane;
        Vector3 farTopLeft = farCenter + up * (farHeight / 2.0f) - right * (farWidth / 2.0f);
        Vector3 farTopRight = farCenter + up * (farHeight / 2.0f) + right * (farWidth / 2.0f);
        Vector3 farBottomRight = farCenter - up * (farHeight / 2.0f) + right * (farWidth / 2.0f);
        Vector3 farBottomLeft = farCenter - up * (farHeight / 2.0f) - right * (farWidth / 2.0f);

        return new Vector3[]
        {
            // Near plane (0-3)
            nearTopLeft, nearTopRight, nearBottomRight, nearBottomLeft,
            // Far plane (4-7)
            farTopLeft, farTopRight, farBottomRight, farBottomLeft
        };
    }

    private static Vector3[] CalculateOrthographicFrustumVertices(
        Vector3 position, Vector3 forward, Vector3 up,
        float orthoSize, float aspectRatio, float nearPlane, float farPlane)
    {
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
        up = Vector3.Normalize(Vector3.Cross(right, forward));

        float halfHeight = orthoSize;
        float halfWidth = orthoSize * aspectRatio;

        // Near plane
        Vector3 nearCenter = position + forward * nearPlane;
        Vector3 nearTopLeft = nearCenter + up * halfHeight - right * halfWidth;
        Vector3 nearTopRight = nearCenter + up * halfHeight + right * halfWidth;
        Vector3 nearBottomRight = nearCenter - up * halfHeight + right * halfWidth;
        Vector3 nearBottomLeft = nearCenter - up * halfHeight - right * halfWidth;

        // Far plane (mismo tamaño en ortográfica)
        Vector3 farCenter = position + forward * farPlane;
        Vector3 farTopLeft = farCenter + up * halfHeight - right * halfWidth;
        Vector3 farTopRight = farCenter + up * halfHeight + right * halfWidth;
        Vector3 farBottomRight = farCenter - up * halfHeight + right * halfWidth;
        Vector3 farBottomLeft = farCenter - up * halfHeight - right * halfWidth;

        return new Vector3[]
        {
            nearTopLeft, nearTopRight, nearBottomRight, nearBottomLeft,
            farTopLeft, farTopRight, farBottomRight, farBottomLeft
        };
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