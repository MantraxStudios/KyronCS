using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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
            string fullVertexPath = AssetManager.BasePath + vertexPath;
            string fullFragmentPath = AssetManager.BasePath + fragmentPath;

            if (!File.Exists(fullVertexPath))
                throw new FileNotFoundException($"Vertex shader not found: {vertexPath}");
            if (!File.Exists(fullFragmentPath))
                throw new FileNotFoundException($"Fragment shader not found: {fragmentPath}");

            VertexPath = vertexPath;
            FragmentPath = fragmentPath;

            string vertexSource = File.ReadAllText(fullVertexPath);
            string fragmentSource = File.ReadAllText(fullFragmentPath);

            // Procesar #include en ambos shaders
            vertexSource = ProcessIncludes(fullVertexPath, vertexSource);
            fragmentSource = ProcessIncludes(fullFragmentPath, fragmentSource);

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

        // ===== SISTEMA DE #INCLUDE =====
        
        private static HashSet<string> _currentIncludes;

        private string ProcessIncludes(string shaderPath, string source)
        {
            _currentIncludes = new HashSet<string>();
            return ProcessIncludesRecursive(shaderPath, source);
        }

        private string ProcessIncludesRecursive(string currentPath, string source)
        {
            // Patrón para detectar: #include "ruta/archivo.glsl"
            string pattern = @"^\s*#include\s+""([^""]+)""\s*";
            
            string result = "";
            string[] lines = source.Split('\n');
            string currentDir = Path.GetDirectoryName(currentPath) ?? "";

            foreach (string line in lines)
            {
                Match match = Regex.Match(line.Trim(), pattern);
                
                if (match.Success)
                {
                    string includePath = match.Groups[1].Value;
                    
                    // Resolver ruta completa del include
                    string fullIncludePath;
                    if (Path.IsPathRooted(includePath))
                    {
                        fullIncludePath = includePath;
                    }
                    else
                    {
                        fullIncludePath = Path.Combine(currentDir, includePath);
                    }
                    
                    string normalizedPath = Path.GetFullPath(fullIncludePath);

                    // Prevenir inclusión circular
                    if (_currentIncludes.Contains(normalizedPath))
                    {
                        result += $"// Already included: {includePath}\n";
                        continue;
                    }

                    _currentIncludes.Add(normalizedPath);

                    // Leer y procesar el archivo incluido
                    if (File.Exists(normalizedPath))
                    {
                        string includeSource = File.ReadAllText(normalizedPath);
                        result += $"// ===== Begin include: {includePath} =====\n";
                        result += ProcessIncludesRecursive(normalizedPath, includeSource);
                        result += $"// ===== End include: {includePath} =====\n";
                    }
                    else
                    {
                        Console.WriteLine($"[Shader] Warning: Include file not found: {normalizedPath}");
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

        // Método helper para debug: guardar shader procesado
        public void SaveProcessedShader(string outputPath)
        {
            if (!_isCompiled)
            {
                Console.WriteLine("[Shader] Cannot save processed shader: shader not compiled");
                return;
            }

            string fullVertexPath = AssetManager.BasePath + VertexPath;
            string fullFragmentPath = AssetManager.BasePath + FragmentPath;

            if (File.Exists(fullVertexPath))
            {
                string vertexSource = File.ReadAllText(fullVertexPath);
                vertexSource = ProcessIncludes(fullVertexPath, vertexSource);
                File.WriteAllText(outputPath + ".vert", vertexSource);
            }

            if (File.Exists(fullFragmentPath))
            {
                string fragmentSource = File.ReadAllText(fullFragmentPath);
                fragmentSource = ProcessIncludes(fullFragmentPath, fragmentSource);
                File.WriteAllText(outputPath + ".frag", fragmentSource);
            }

            Console.WriteLine($"[Shader] Processed shader saved to: {outputPath}");
        }
    }
}