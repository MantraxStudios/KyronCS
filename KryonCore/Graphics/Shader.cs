using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace KrayonCore
{
    public sealed class Shader : IDisposable
    {
        private int _programID;
        private bool _isCompiled;

        public string Name { get; }
        public Guid VertexGUID { get; private set; }
        public Guid FragmentGUID { get; private set; }
        public int ProgramID => _programID;
        public bool IsCompiled => _isCompiled;

        public Shader(string name)
        {
            Name = name;
        }

        public void Load(Guid vertexGuid, Guid fragmentGuid)
        {
            VertexGUID = vertexGuid;
            FragmentGUID = fragmentGuid;

            byte[] vertexBytes = AssetManager.GetBytes(vertexGuid);
            byte[] fragmentBytes = AssetManager.GetBytes(fragmentGuid);

            if (vertexBytes == null)
                throw new Exception($"Vertex shader not found for GUID: {vertexGuid}");
            if (fragmentBytes == null)
                throw new Exception($"Fragment shader not found for GUID: {fragmentGuid}");

            string vertexSource = Encoding.UTF8.GetString(vertexBytes);
            string fragmentSource = Encoding.UTF8.GetString(fragmentBytes);

            var vertexAsset = AssetManager.Get(vertexGuid);
            var fragmentAsset = AssetManager.Get(fragmentGuid);

            vertexSource = ProcessIncludes(vertexAsset?.Path, vertexSource);
            fragmentSource = ProcessIncludes(fragmentAsset?.Path, fragmentSource);

            Compile(vertexSource, fragmentSource);
        }

        public void LoadFromBaseName(string baseName)
        {
            string vertexPath = $"{baseName}.vert";
            string fragmentPath = $"{baseName}.frag";

            var vertexAsset = AssetManager.FindByPath(vertexPath);
            var fragmentAsset = AssetManager.FindByPath(fragmentPath);

            if (vertexAsset == null)
                throw new FileNotFoundException($"Vertex shader not found in AssetManager: {vertexPath}");
            if (fragmentAsset == null)
                throw new FileNotFoundException($"Fragment shader not found in AssetManager: {fragmentPath}");

            Load(vertexAsset.Guid, fragmentAsset.Guid);
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

            int vs = CompileShader(ShaderType.VertexShader, vertexSource, "VertexShader");
            int fs = CompileShader(ShaderType.FragmentShader, fragmentSource, "FragmentShader");

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
            {
                Console.WriteLine($"[Shader] Warning: Attempted to use uncompiled shader '{Name}'");
                return;
            }

            GL.UseProgram(_programID);
        }

        public int GetUniformLocation(string name)
        {
            if (!_isCompiled)
            {
                Console.WriteLine($"[Shader] Warning: Attempted to get uniform location from uncompiled shader '{Name}'");
                return -1;
            }

            return GL.GetUniformLocation(_programID, name);
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

        private static int CompileShader(ShaderType type, string source, string shaderName)
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

                throw new Exception($"{shaderName} compile error:\n{log}\n\nShader preview (first 5 lines):\n{preview}");
            }

            return shader;
        }

        private static HashSet<string> _currentIncludes;

        private string ProcessIncludes(string shaderRelativePath, string source)
        {
            _currentIncludes = new HashSet<string>();
            string shaderDir = "";
            if (!string.IsNullOrEmpty(shaderRelativePath))
            {
                shaderDir = Path.GetDirectoryName(shaderRelativePath)?.Replace("\\", "/") ?? "";
            }
            return ProcessIncludesRecursive(shaderDir, source);
        }

        private string ProcessIncludesRecursive(string currentDir, string source)
        {
            string pattern = @"^\s*#include\s+""([^""]+)""\s*";

            string result = "";
            string[] lines = source.Split('\n');

            foreach (string line in lines)
            {
                Match match = Regex.Match(line.Trim(), pattern);

                if (match.Success)
                {
                    string includePath = match.Groups[1].Value.Replace("\\", "/");

                    string resolvedPath = string.IsNullOrEmpty(currentDir)
                        ? includePath
                        : $"{currentDir}/{includePath}";

                    resolvedPath = NormalizePath(resolvedPath);

                    if (_currentIncludes.Contains(resolvedPath))
                    {
                        result += $"// Already included: {includePath}\n";
                        continue;
                    }

                    _currentIncludes.Add(resolvedPath);

                    var includeAsset = AssetManager.FindByPath(resolvedPath);
                    if (includeAsset != null)
                    {
                        byte[] includeBytes = AssetManager.GetBytes(includeAsset.Guid);
                        if (includeBytes != null)
                        {
                            string includeSource = Encoding.UTF8.GetString(includeBytes);
                            string includeDir = Path.GetDirectoryName(resolvedPath)?.Replace("\\", "/") ?? "";
                            result += $"// ===== Begin include: {includePath} =====\n";
                            result += ProcessIncludesRecursive(includeDir, includeSource);
                            result += $"// ===== End include: {includePath} =====\n";
                        }
                        else
                        {
                            Console.WriteLine($"[Shader] Warning: Could not read include: {resolvedPath}");
                            result += $"// ERROR: Could not read include: {includePath}\n";
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Shader] Warning: Include not found in AssetManager: {resolvedPath}");
                        result += $"// ERROR: Include not found: {includePath}\n";
                    }
                }
                else
                {
                    result += line + "\n";
                }
            }

            return result;
        }

        private static string NormalizePath(string path)
        {
            var parts = path.Split('/');
            var stack = new Stack<string>();

            foreach (var part in parts)
            {
                if (part == ".." && stack.Count > 0)
                    stack.Pop();
                else if (part != "." && !string.IsNullOrEmpty(part))
                    stack.Push(part);
            }

            return string.Join("/", stack.Reverse());
        }
    }
}