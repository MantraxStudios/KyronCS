using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore
{
    public class Material : AssetData, IDisposable
    {
        private Shader _shader;
        private readonly Dictionary<string, int> _uniformLocations = new();
        private readonly Dictionary<int, TextureLoader> _textures = new();
        private readonly Dictionary<string, object> _uniformCache = new();

        public string Name { get; }
        public Shader Shader => _shader;
        public int Program => _shader?.ProgramID ?? 0;

        public TextureLoader MainTexture { get; private set; }

        public Material(string name, string vertexPath, string fragmentPath)
        {
            Name = name;
            _shader = new Shader(name);
            _shader.LoadFromFile(vertexPath, fragmentPath);
        }

        public Material(string name, string shaderBasePath)
        {
            Name = name;
            _shader = new Shader(name);
            _shader.LoadFromFile(shaderBasePath);
        }

        public Material(string name, Shader shader)
        {
            Name = name;
            _shader = shader ?? throw new ArgumentNullException(nameof(shader));
        }

        public void Use()
        {
            _shader.Use();
            ApplyCachedUniforms();

            foreach (var kvp in _textures)
            {
                kvp.Value.Bind(TextureUnit.Texture0 + kvp.Key);
            }
        }

        private void ApplyCachedUniforms()
        {
            foreach (var kvp in _uniformCache)
            {
                string name = kvp.Key;
                object value = kvp.Value;

                switch (value)
                {
                    case int intValue:
                        GL.Uniform1(GetUniformLocation(name), intValue);
                        break;
                    case float floatValue:
                        GL.Uniform1(GetUniformLocation(name), floatValue);
                        break;
                    case Vector2 vec2Value:
                        GL.Uniform2(GetUniformLocation(name), vec2Value);
                        break;
                    case Vector3 vec3Value:
                        GL.Uniform3(GetUniformLocation(name), vec3Value);
                        break;
                    case Vector4 vec4Value:
                        GL.Uniform4(GetUniformLocation(name), vec4Value);
                        break;
                    case Color4 colorValue:
                        GL.Uniform4(GetUniformLocation(name), colorValue);
                        break;
                    case Matrix4 matrixValue:
                        Matrix4 mat = matrixValue;
                        GL.UniformMatrix4(GetUniformLocation(name), false, ref mat);
                        break;
                }
            }
        }

        public void LoadMainTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            MainTexture?.Dispose();
            MainTexture = new TextureLoader("mainTexture", path, generateMipmaps, flip);
            SetTexture("mainTexture", MainTexture, 0);
        }

        public void LoadTexture(string uniformName, string path, int slot = 0, bool generateMipmaps = true, bool flip = true)
        {
            if (_textures.ContainsKey(slot))
                _textures[slot].Dispose();

            var texture = new TextureLoader(uniformName, path, generateMipmaps, flip);
            SetTexture(uniformName, texture, slot);
        }

        public void SetTexture(string uniformName, TextureLoader texture, int slot = 0)
        {
            if (_textures.ContainsKey(slot))
                _textures[slot].Dispose();

            _textures[slot] = texture;

            // Guardar en caché para que se aplique cuando se use el material
            SetIntCached(uniformName, slot);
        }

        public TextureLoader GetTexture(int slot)
        {
            return _textures.TryGetValue(slot, out var texture) ? texture : null;
        }

        public void RemoveTexture(int slot)
        {
            if (_textures.TryGetValue(slot, out var texture))
            {
                texture.Dispose();
                _textures.Remove(slot);
            }
        }

        public void SetInt(string name, int value)
        {
            _shader.Use();
            GL.Uniform1(GetUniformLocation(name), value);
        }

        public void SetFloat(string name, float value)
        {
            _shader.Use();
            GL.Uniform1(GetUniformLocation(name), value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            _shader.Use();
            GL.Uniform2(GetUniformLocation(name), value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            _shader.Use();
            GL.Uniform3(GetUniformLocation(name), value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            _shader.Use();
            GL.Uniform4(GetUniformLocation(name), value);
        }

        public void SetColor(string name, Color4 color)
        {
            _shader.Use();
            GL.Uniform4(GetUniformLocation(name), color);
        }

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            _shader.Use();
            GL.UniformMatrix4(GetUniformLocation(name), false, ref matrix);
        }

        public void SetBool(string name, bool value)
        {
            SetInt(name, value ? 1 : 0);
        }

        public void SetIntCached(string name, int value)
        {
            _uniformCache[name] = value;
        }

        public void SetFloatCached(string name, float value)
        {
            _uniformCache[name] = value;
        }

        public void SetVector2Cached(string name, Vector2 value)
        {
            _uniformCache[name] = value;
        }

        public void SetVector3Cached(string name, Vector3 value)
        {
            _uniformCache[name] = value;
        }

        public void SetVector4Cached(string name, Vector4 value)
        {
            _uniformCache[name] = value;
        }

        public void SetColorCached(string name, Color4 color)
        {
            _uniformCache[name] = color;
        }

        public void SetMatrix4Cached(string name, Matrix4 matrix)
        {
            _uniformCache[name] = matrix;
        }

        public T GetCached<T>(string name)
        {
            if (_uniformCache.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        private int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out var location))
                return location;

            location = _shader.GetUniformLocation(name);
            _uniformLocations[name] = location;

            if (location == -1)
            {
                Console.WriteLine($"Warning: Uniform '{name}' not found in shader for material '{Name}'");
            }

            return location;
        }

        public void ClearCache()
        {
            _uniformCache.Clear();
        }

        public void Dispose()
        {
            MainTexture?.Dispose();
            foreach (var texture in _textures.Values)
                texture.Dispose();
            _textures.Clear();

            _shader?.Dispose();
        }
    }
}