using KrayonCore.Core.Attributes;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore
{
    public class Material : IDisposable
    {
        private Shader _shader;
        private readonly Dictionary<string, int> _uniformLocations = new();
        private readonly Dictionary<int, TextureLoader> _textures = new();
        private readonly Dictionary<string, object> _uniformCache = new();

        public string Name { get; }
        public Shader Shader => _shader;
        public int Program => _shader?.ProgramID ?? 0;

        public Guid VertexShaderGUID { get; set; } = Guid.Empty;
        public Guid FragmentShaderGUID { get; set; } = Guid.Empty;

        public TextureLoader MainTexture { get; private set; }

        public TextureLoader AlbedoTexture { get; private set; }
        public TextureLoader NormalTexture { get; private set; }
        public TextureLoader MetallicTexture { get; private set; }
        public TextureLoader RoughnessTexture { get; private set; }
        public TextureLoader AOTexture { get; private set; }
        public TextureLoader EmissiveTexture { get; private set; }

        public Guid AlbedoTextureGUID { get; set; } = Guid.Empty;
        public Guid NormalTextureGUID { get; set; } = Guid.Empty;
        public Guid MetallicTextureGUID { get; set; } = Guid.Empty;
        public Guid RoughnessTextureGUID { get; set; } = Guid.Empty;
        public Guid AOTextureGUID { get; set; } = Guid.Empty;
        public Guid EmissiveTextureGUID { get; set; } = Guid.Empty;

        public Vector3 AlbedoColor { get; set; } = Vector3.One;
        public float Metallic { get; set; } = 0.0f;
        public float Roughness { get; set; } = 0.5f;
        public float AO { get; set; } = 1.0f;
        public Vector3 EmissiveColor { get; set; } = Vector3.Zero;

        public bool UseAlbedoMap { get; set; } = false;
        public bool UseNormalMap { get; set; } = false;
        public bool UseMetallicMap { get; set; } = false;
        public bool UseRoughnessMap { get; set; } = false;
        public bool UseAOMap { get; set; } = false;
        public bool UseEmissiveMap { get; set; } = false;
        public float NormalMapIntensity { get; set; } = 1.0f;
        public Vector3 AmbientLight { get; set; } = new Vector3(0.03f, 0.03f, 0.03f);
        public float AmbientStrength { get; set; } = 1.0f;

        public Material(string name, Guid vertexGuid, Guid fragmentGuid)
        {
            Name = name;
            VertexShaderGUID = vertexGuid;
            FragmentShaderGUID = fragmentGuid;
            _shader = new Shader(name);
            _shader.Load(vertexGuid, fragmentGuid);
        }

        public Material(string name, string shaderBaseName)
        {
            Name = name;
            _shader = new Shader(name);
            _shader.LoadFromBaseName(shaderBaseName);
            VertexShaderGUID = _shader.VertexGUID;
            FragmentShaderGUID = _shader.FragmentGUID;
        }

        public Material(string name, Shader shader)
        {
            Name = name;
            _shader = shader ?? throw new ArgumentNullException(nameof(shader));
            VertexShaderGUID = shader.VertexGUID;
            FragmentShaderGUID = shader.FragmentGUID;
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

                int location = GetUniformLocationSilent(name);
                if (location == -1)
                    continue;

                switch (value)
                {
                    case int intValue:
                        GL.Uniform1(location, intValue);
                        break;
                    case float floatValue:
                        GL.Uniform1(location, floatValue);
                        break;
                    case Vector2 vec2Value:
                        GL.Uniform2(location, vec2Value);
                        break;
                    case Vector3 vec3Value:
                        GL.Uniform3(location, vec3Value);
                        break;
                    case Vector4 vec4Value:
                        GL.Uniform4(location, vec4Value);
                        break;
                    case Color4 colorValue:
                        GL.Uniform4(location, colorValue);
                        break;
                    case Matrix4 matrixValue:
                        Matrix4 mat = matrixValue;
                        GL.UniformMatrix4(location, false, ref mat);
                        break;
                }
            }
        }

        public void SetPBRProperties()
        {
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
            SetFloatCached("u_Metallic", Metallic);
            SetFloatCached("u_Roughness", Roughness);
            SetFloatCached("u_AO", AO);
            SetVector3Cached("u_EmissiveColor", EmissiveColor);

            SetFloatCached("u_NormalMapIntensity", NormalMapIntensity);
            SetVector3Cached("u_AmbientLight", AmbientLight);
            SetFloatCached("u_AmbientStrength", AmbientStrength);

            SetIntCached("u_UseAlbedoMap", UseAlbedoMap ? 1 : 0);
            SetIntCached("u_UseNormalMap", UseNormalMap ? 1 : 0);
            SetIntCached("u_UseMetallicMap", UseMetallicMap ? 1 : 0);
            SetIntCached("u_UseRoughnessMap", UseRoughnessMap ? 1 : 0);
            SetIntCached("u_UseAOMap", UseAOMap ? 1 : 0);
            SetIntCached("u_UseEmissiveMap", UseEmissiveMap ? 1 : 0);

            if (UseAlbedoMap) SetIntCached("u_AlbedoMap", 0);
            if (UseNormalMap) SetIntCached("u_NormalMap", 1);
            if (UseMetallicMap) SetIntCached("u_MetallicMap", 2);
            if (UseRoughnessMap) SetIntCached("u_RoughnessMap", 3);
            if (UseAOMap) SetIntCached("u_AOMap", 4);
            if (UseEmissiveMap) SetIntCached("u_EmissiveMap", 5);
        }

        public void LoadMainTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            MainTexture?.Dispose();
            MainTexture = new TextureLoader("mainTexture", guid, generateMipmaps, flip);

            AlbedoTexture?.Dispose();
            AlbedoTexture = MainTexture;
            AlbedoTextureGUID = guid;

            SetTexture("u_AlbedoMap", MainTexture, 0);
            UseAlbedoMap = true;
        }

        public void LoadAlbedoTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            AlbedoTexture?.Dispose();
            AlbedoTexture = new TextureLoader("u_AlbedoMap", guid, generateMipmaps, flip);
            AlbedoTextureGUID = guid;
            SetTexture("u_AlbedoMap", AlbedoTexture, 0);
            UseAlbedoMap = true;
        }

        public void LoadNormalTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            NormalTexture?.Dispose();
            NormalTexture = new TextureLoader("u_NormalMap", guid, generateMipmaps, flip);
            NormalTextureGUID = guid;
            SetTexture("u_NormalMap", NormalTexture, 1);
            UseNormalMap = true;
        }

        public void LoadMetallicTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            MetallicTexture?.Dispose();
            MetallicTexture = new TextureLoader("u_MetallicMap", guid, generateMipmaps, flip);
            MetallicTextureGUID = guid;
            SetTexture("u_MetallicMap", MetallicTexture, 2);
            UseMetallicMap = true;
        }

        public void LoadRoughnessTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            RoughnessTexture?.Dispose();
            RoughnessTexture = new TextureLoader("u_RoughnessMap", guid, generateMipmaps, flip);
            RoughnessTextureGUID = guid;
            SetTexture("u_RoughnessMap", RoughnessTexture, 3);
            UseRoughnessMap = true;
        }

        public void LoadAOTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            AOTexture?.Dispose();
            AOTexture = new TextureLoader("u_AOMap", guid, generateMipmaps, flip);
            AOTextureGUID = guid;
            SetTexture("u_AOMap", AOTexture, 4);
            UseAOMap = true;
        }

        public void LoadEmissiveTexture(Guid guid, bool generateMipmaps = true, bool flip = true)
        {
            EmissiveTexture?.Dispose();
            EmissiveTexture = new TextureLoader("u_EmissiveMap", guid, generateMipmaps, flip);
            EmissiveTextureGUID = guid;
            SetTexture("u_EmissiveMap", EmissiveTexture, 5);
            UseEmissiveMap = true;
        }

        public void LoadPBRTextures(
            Guid? albedoGuid = null,
            Guid? normalGuid = null,
            Guid? metallicGuid = null,
            Guid? roughnessGuid = null,
            Guid? aoGuid = null,
            Guid? emissiveGuid = null,
            bool generateMipmaps = true,
            bool flip = true)
        {
            if (albedoGuid.HasValue && albedoGuid.Value != Guid.Empty)
                LoadAlbedoTexture(albedoGuid.Value, generateMipmaps, flip);

            if (normalGuid.HasValue && normalGuid.Value != Guid.Empty)
                LoadNormalTexture(normalGuid.Value, generateMipmaps, flip);

            if (metallicGuid.HasValue && metallicGuid.Value != Guid.Empty)
                LoadMetallicTexture(metallicGuid.Value, generateMipmaps, flip);

            if (roughnessGuid.HasValue && roughnessGuid.Value != Guid.Empty)
                LoadRoughnessTexture(roughnessGuid.Value, generateMipmaps, flip);

            if (aoGuid.HasValue && aoGuid.Value != Guid.Empty)
                LoadAOTexture(aoGuid.Value, generateMipmaps, flip);

            if (emissiveGuid.HasValue && emissiveGuid.Value != Guid.Empty)
                LoadEmissiveTexture(emissiveGuid.Value, generateMipmaps, flip);

            SetPBRProperties();
        }

        public void SetupPBRMaterial(
            Vector3 albedo,
            float metallic = 0.0f,
            float roughness = 0.5f,
            float ao = 1.0f,
            Vector3? emissive = null)
        {
            AlbedoColor = albedo;
            Metallic = metallic;
            Roughness = roughness;
            AO = ao;
            EmissiveColor = emissive ?? Vector3.Zero;
            SetPBRProperties();
        }

        public void SetNormalMapIntensity(float intensity)
        {
            NormalMapIntensity = Math.Max(0f, intensity);
            SetFloatCached("u_NormalMapIntensity", NormalMapIntensity);
        }

        public void SetAmbientLight(Vector3 color)
        {
            AmbientLight = color;
            SetVector3Cached("u_AmbientLight", AmbientLight);
        }

        public void SetAmbientLight(float r, float g, float b)
        {
            AmbientLight = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_AmbientLight", AmbientLight);
        }

        public void SetAmbientStrength(float strength)
        {
            AmbientStrength = Math.Max(0f, strength);
            SetFloatCached("u_AmbientStrength", AmbientStrength);
        }

        public void RemoveAlbedoTexture()
        {
            AlbedoTexture?.Dispose();
            AlbedoTexture = null;
            AlbedoTextureGUID = Guid.Empty;
            RemoveTexture(0);
            UseAlbedoMap = false;
        }

        public void RemoveNormalTexture()
        {
            NormalTexture?.Dispose();
            NormalTexture = null;
            NormalTextureGUID = Guid.Empty;
            RemoveTexture(1);
            UseNormalMap = false;
        }

        public void RemoveMetallicTexture()
        {
            MetallicTexture?.Dispose();
            MetallicTexture = null;
            MetallicTextureGUID = Guid.Empty;
            RemoveTexture(2);
            UseMetallicMap = false;
        }

        public void RemoveRoughnessTexture()
        {
            RoughnessTexture?.Dispose();
            RoughnessTexture = null;
            RoughnessTextureGUID = Guid.Empty;
            RemoveTexture(3);
            UseRoughnessMap = false;
        }

        public void RemoveAOTexture()
        {
            AOTexture?.Dispose();
            AOTexture = null;
            AOTextureGUID = Guid.Empty;
            RemoveTexture(4);
            UseAOMap = false;
        }

        public void RemoveEmissiveTexture()
        {
            EmissiveTexture?.Dispose();
            EmissiveTexture = null;
            EmissiveTextureGUID = Guid.Empty;
            RemoveTexture(5);
            UseEmissiveMap = false;
        }

        public void RemoveAllPBRTextures()
        {
            RemoveAlbedoTexture();
            RemoveNormalTexture();
            RemoveMetallicTexture();
            RemoveRoughnessTexture();
            RemoveAOTexture();
            RemoveEmissiveTexture();
        }

        public void LoadTexture(string uniformName, Guid guid, int slot = 0, bool generateMipmaps = true, bool flip = true)
        {
            if (_textures.ContainsKey(slot))
                _textures[slot].Dispose();

            var texture = new TextureLoader(uniformName, guid, generateMipmaps, flip);
            SetTexture(uniformName, texture, slot);
        }

        public void SetTexture(string uniformName, TextureLoader texture, int slot = 0)
        {
            if (_textures.ContainsKey(slot))
                _textures[slot].Dispose();

            _textures[slot] = texture;
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
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform1(location, value);
        }

        public void SetFloat(string name, float value)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform1(location, value);
        }

        public void SetVector2(string name, Vector2 value)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform2(location, value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform3(location, value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform4(location, value);
        }

        public void SetColor(string name, Color4 color)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.Uniform4(location, color);
        }

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            _shader.Use();
            int location = GetUniformLocation(name);
            if (location != -1)
                GL.UniformMatrix4(location, false, ref matrix);
        }

        public void SetBool(string name, bool value)
        {
            SetInt(name, value ? 1 : 0);
        }

        public void SetIntCached(string name, int value) => _uniformCache[name] = value;
        public void SetFloatCached(string name, float value) => _uniformCache[name] = value;
        public void SetVector2Cached(string name, Vector2 value) => _uniformCache[name] = value;
        public void SetVector3Cached(string name, Vector3 value) => _uniformCache[name] = value;
        public void SetVector4Cached(string name, Vector4 value) => _uniformCache[name] = value;
        public void SetColorCached(string name, Color4 color) => _uniformCache[name] = color;
        public void SetMatrix4Cached(string name, Matrix4 matrix) => _uniformCache[name] = matrix;

        public T GetCached<T>(string name)
        {
            if (_uniformCache.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        private int GetUniformLocationSilent(string name)
        {
            if (_uniformLocations.TryGetValue(name, out var location))
                return location;

            location = _shader.GetUniformLocation(name);
            _uniformLocations[name] = location;
            return location;
        }

        private int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out var location))
                return location;

            location = _shader.GetUniformLocation(name);
            _uniformLocations[name] = location;

            if (location == -1)
                Console.WriteLine($"Warning: Uniform '{name}' not found in shader for material '{Name}'");

            return location;
        }

        public void ClearCache() => _uniformCache.Clear();

        public void SetAlbedo(Vector3 color)
        {
            AlbedoColor = color;
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
        }

        public void SetAlbedo(float r, float g, float b)
        {
            AlbedoColor = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
        }

        public void SetMetallic(float value)
        {
            Metallic = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_Metallic", Metallic);
        }

        public void SetRoughness(float value)
        {
            Roughness = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_Roughness", Roughness);
        }

        public void SetAO(float value)
        {
            AO = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_AO", AO);
        }

        public void SetEmissive(Vector3 color)
        {
            EmissiveColor = color;
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        public void SetEmissive(float r, float g, float b)
        {
            EmissiveColor = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        public void SetEmissiveIntensity(float intensity)
        {
            EmissiveColor *= intensity;
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        public void SetPBRValues(
            Vector3? albedo = null,
            float? metallic = null,
            float? roughness = null,
            float? ao = null,
            Vector3? emissive = null)
        {
            if (albedo.HasValue) AlbedoColor = albedo.Value;
            if (metallic.HasValue) Metallic = MathHelper.Clamp(metallic.Value, 0f, 1f);
            if (roughness.HasValue) Roughness = MathHelper.Clamp(roughness.Value, 0f, 1f);
            if (ao.HasValue) AO = MathHelper.Clamp(ao.Value, 0f, 1f);
            if (emissive.HasValue) EmissiveColor = emissive.Value;

            SetPBRProperties();
        }

        public void Dispose()
        {
            MainTexture?.Dispose();
            AlbedoTexture?.Dispose();
            NormalTexture?.Dispose();
            MetallicTexture?.Dispose();
            RoughnessTexture?.Dispose();
            AOTexture?.Dispose();
            EmissiveTexture?.Dispose();

            foreach (var texture in _textures.Values)
                texture.Dispose();
            _textures.Clear();

            _shader?.Dispose();
        }
    }
}