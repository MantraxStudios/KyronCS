using OpenTK.Graphics.OpenGL4;
using System;
using System.IO;
using System.Linq;

namespace KrayonCore
{
    public sealed class Shader : IDisposable
    {
        private int _programID;
        private bool _isCompiled;

        public string Name { get; }
        public string VertexPath { get; private set; }
        public string FragmentPath { get; private set; }
        public int ProgramID => _programID;
        public bool IsCompiled => _isCompiled;

        public Shader(string name)
        {
            Name = name;
        }

        public void LoadFromFile(string vertexPath, string fragmentPath)
        {
            if (!File.Exists(vertexPath))
                throw new FileNotFoundException($"Vertex shader not found: {vertexPath}");
            if (!File.Exists(fragmentPath))
                throw new FileNotFoundException($"Fragment shader not found: {fragmentPath}");

            VertexPath = vertexPath;
            FragmentPath = fragmentPath;

            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            Compile(vertexSource, fragmentSource);
        }

        public void LoadFromFile(string basePath)
        {
            string vertexPath = $"{basePath}.vert";
            string fragmentPath = $"{basePath}.frag";
            LoadFromFile(vertexPath, fragmentPath);
        }

        public void LoadFromSource(string vertexSource, string fragmentSource)
        {
            Compile(vertexSource, fragmentSource);
        }

        private void Compile(string vertexSource, string fragmentSource)
        {
            if (_isCompiled)
            {
                Dispose();
            }

            int vs = CompileShader(ShaderType.VertexShader, vertexSource);
            int fs = CompileShader(ShaderType.FragmentShader, fragmentSource);

            _programID = GL.CreateProgram();
            GL.AttachShader(_programID, vs);
            GL.AttachShader(_programID, fs);
            GL.LinkProgram(_programID);

            GL.GetProgram(_programID, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetProgramInfoLog(_programID);
                GL.DeleteProgram(_programID);
                throw new Exception($"Shader link error ({Name}):\n{log}");
            }

            GL.DeleteShader(vs);
            GL.DeleteShader(fs);

            _isCompiled = true;
            Console.WriteLine($"[Shader] '{Name}' compiled successfully");
        }

        public void Use()
        {
            if (!_isCompiled)
                throw new InvalidOperationException($"Shader '{Name}' is not compiled");

            GL.UseProgram(_programID);
        }

        public int GetUniformLocation(string name)
        {
            if (!_isCompiled)
                throw new InvalidOperationException($"Shader '{Name}' is not compiled");

            int location = GL.GetUniformLocation(_programID, name);
            return location;
        }

        public void Dispose()
        {
            if (_isCompiled)
            {
                GL.DeleteProgram(_programID);
                _programID = 0;
                _isCompiled = false;
            }
        }

        private static int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string log = GL.GetShaderInfoLog(shader);
                GL.DeleteShader(shader);

                string[] lines = source.Split('\n');
                string preview = string.Join("\n", lines.Take(5));

                throw new Exception($"{type} compile error:\n{log}\n\nShader preview (first 5 lines):\n{preview}");
            }

            return shader;
        }
    }
}