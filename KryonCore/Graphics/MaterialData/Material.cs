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

        public TextureLoader MainTexture { get; private set; }

        // Referencias a texturas PBR específicas
        public TextureLoader AlbedoTexture { get; private set; }
        public TextureLoader NormalTexture { get; private set; }
        public TextureLoader MetallicTexture { get; private set; }
        public TextureLoader RoughnessTexture { get; private set; }
        public TextureLoader AOTexture { get; private set; }
        public TextureLoader EmissiveTexture { get; private set; }

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

        public void SetPBRProperties()
        {
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
            SetFloatCached("u_Metallic", Metallic);
            SetFloatCached("u_Roughness", Roughness);
            SetFloatCached("u_AO", AO);
            SetVector3Cached("u_EmissiveColor", EmissiveColor);

            // NUEVO: Propiedades de normal map e iluminación
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

        // ============================================
        // FUNCIONES PARA CARGAR TEXTURAS INDIVIDUALES
        // ============================================

        /// <summary>
        /// Carga la textura principal como Albedo (compatible con sistemas legacy)
        /// </summary>
        public void LoadMainTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            MainTexture?.Dispose();
            MainTexture = new TextureLoader("mainTexture", path, generateMipmaps, flip);

            // IMPORTANTE: MainTexture debe ser tratada como AlbedoTexture
            AlbedoTexture?.Dispose();
            AlbedoTexture = MainTexture;

            SetTexture("u_AlbedoMap", MainTexture, 0);
            UseAlbedoMap = true;

            Console.WriteLine($"[Material '{Name}'] ✓ Main texture loaded as Albedo: {path}");
            Console.WriteLine($"[Material '{Name}']   UseAlbedoMap = {UseAlbedoMap}");
        }

        /// <summary>
        /// Carga la textura de Albedo/Diffuse (Color base)
        /// </summary>
        public void LoadAlbedoTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            AlbedoTexture?.Dispose();
            AlbedoTexture = new TextureLoader("u_AlbedoMap", path, generateMipmaps, flip);
            SetTexture("u_AlbedoMap", AlbedoTexture, 0);
            UseAlbedoMap = true;
            Console.WriteLine($"[Material] Albedo texture loaded: {path}");
        }

        /// <summary>
        /// Carga la textura de Normal Map (para detalles de superficie)
        /// </summary>
        public void LoadNormalTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            NormalTexture?.Dispose();
            NormalTexture = new TextureLoader("u_NormalMap", path, generateMipmaps, flip);
            SetTexture("u_NormalMap", NormalTexture, 1);
            UseNormalMap = true;
            Console.WriteLine($"[Material] Normal texture loaded: {path}");
        }

        /// <summary>
        /// Carga la textura de Metallic (qué tan metálico es el material)
        /// </summary>
        public void LoadMetallicTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            MetallicTexture?.Dispose();
            MetallicTexture = new TextureLoader("u_MetallicMap", path, generateMipmaps, flip);
            SetTexture("u_MetallicMap", MetallicTexture, 2);
            UseMetallicMap = true;
            Console.WriteLine($"[Material] Metallic texture loaded: {path}");
        }

        /// <summary>
        /// Carga la textura de Roughness (qué tan rugoso/suave es el material)
        /// </summary>
        public void LoadRoughnessTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            RoughnessTexture?.Dispose();
            RoughnessTexture = new TextureLoader("u_RoughnessMap", path, generateMipmaps, flip);
            SetTexture("u_RoughnessMap", RoughnessTexture, 3);
            UseRoughnessMap = true;
            Console.WriteLine($"[Material] Roughness texture loaded: {path}");
        }

        /// <summary>
        /// Carga la textura de Ambient Occlusion (sombras en cavidades)
        /// </summary>
        public void LoadAOTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            AOTexture?.Dispose();
            AOTexture = new TextureLoader("u_AOMap", path, generateMipmaps, flip);
            SetTexture("u_AOMap", AOTexture, 4);
            UseAOMap = true;
            Console.WriteLine($"[Material] AO texture loaded: {path}");
        }

        /// <summary>
        /// Carga la textura de Emissive (áreas que emiten luz)
        /// </summary>
        public void LoadEmissiveTexture(string path, bool generateMipmaps = true, bool flip = true)
        {
            path = AssetManager.BasePath + path;
            EmissiveTexture?.Dispose();
            EmissiveTexture = new TextureLoader("u_EmissiveMap", path, generateMipmaps, flip);
            SetTexture("u_EmissiveMap", EmissiveTexture, 5);
            UseEmissiveMap = true;
            Console.WriteLine($"[Material] Emissive texture loaded: {path}");
        }

        /// <summary>
        /// Carga todas las texturas PBR de una sola vez
        /// </summary>
        public void LoadPBRTextures(
            string albedoPath = null,
            string normalPath = null,
            string metallicPath = null,
            string roughnessPath = null,
            string aoPath = null,
            string emissivePath = null,
            bool generateMipmaps = true,
            bool flip = true)
        {
            if (!string.IsNullOrEmpty(albedoPath))
                LoadAlbedoTexture(albedoPath, generateMipmaps, flip);

            if (!string.IsNullOrEmpty(normalPath))
                LoadNormalTexture(normalPath, generateMipmaps, flip);

            if (!string.IsNullOrEmpty(metallicPath))
                LoadMetallicTexture(metallicPath, generateMipmaps, flip);

            if (!string.IsNullOrEmpty(roughnessPath))
                LoadRoughnessTexture(roughnessPath, generateMipmaps, flip);

            if (!string.IsNullOrEmpty(aoPath))
                LoadAOTexture(aoPath, generateMipmaps, flip);

            if (!string.IsNullOrEmpty(emissivePath))
                LoadEmissiveTexture(emissivePath, generateMipmaps, flip);

            SetPBRProperties();
            Console.WriteLine($"[Material] PBR textures loaded for material: {Name}");
        }

        /// <summary>
        /// Configura las propiedades PBR sin texturas (solo valores)
        /// </summary>
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
            Console.WriteLine($"[Material] PBR properties set for material: {Name}");
        }

        /// <summary>
        /// Ajusta la intensidad del normal map (0 = sin efecto, 1 = efecto completo, >1 = exagerado)
        /// </summary>
        public void SetNormalMapIntensity(float intensity)
        {
            NormalMapIntensity = Math.Max(0f, intensity);
            SetFloatCached("u_NormalMapIntensity", NormalMapIntensity);
        }

        /// <summary>
        /// Ajusta la luz ambiental global
        /// </summary>
        public void SetAmbientLight(Vector3 color)
        {
            AmbientLight = color;
            SetVector3Cached("u_AmbientLight", AmbientLight);
        }

        /// <summary>
        /// Ajusta la luz ambiental usando valores RGB (0-255)
        /// </summary>
        public void SetAmbientLight(float r, float g, float b)
        {
            AmbientLight = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_AmbientLight", AmbientLight);
        }

        /// <summary>
        /// Ajusta la intensidad de la luz ambiental
        /// </summary>
        public void SetAmbientStrength(float strength)
        {
            AmbientStrength = Math.Max(0f, strength);
            SetFloatCached("u_AmbientStrength", AmbientStrength);
        }


        /// <summary>
        /// Remueve una textura de Albedo
        /// </summary>
        public void RemoveAlbedoTexture()
        {
            AlbedoTexture?.Dispose();
            AlbedoTexture = null;
            RemoveTexture(0);
            UseAlbedoMap = false;
        }

        /// <summary>
        /// Remueve una textura de Normal
        /// </summary>
        public void RemoveNormalTexture()
        {
            NormalTexture?.Dispose();
            NormalTexture = null;
            RemoveTexture(1);
            UseNormalMap = false;
        }

        /// <summary>
        /// Remueve una textura de Metallic
        /// </summary>
        public void RemoveMetallicTexture()
        {
            MetallicTexture?.Dispose();
            MetallicTexture = null;
            RemoveTexture(2);
            UseMetallicMap = false;
        }

        /// <summary>
        /// Remueve una textura de Roughness
        /// </summary>
        public void RemoveRoughnessTexture()
        {
            RoughnessTexture?.Dispose();
            RoughnessTexture = null;
            RemoveTexture(3);
            UseRoughnessMap = false;
        }

        /// <summary>
        /// Remueve una textura de AO
        /// </summary>
        public void RemoveAOTexture()
        {
            AOTexture?.Dispose();
            AOTexture = null;
            RemoveTexture(4);
            UseAOMap = false;
        }

        /// <summary>
        /// Remueve una textura de Emissive
        /// </summary>
        public void RemoveEmissiveTexture()
        {
            EmissiveTexture?.Dispose();
            EmissiveTexture = null;
            RemoveTexture(5);
            UseEmissiveMap = false;
        }

        /// <summary>
        /// Remueve todas las texturas PBR
        /// </summary>
        public void RemoveAllPBRTextures()
        {
            RemoveAlbedoTexture();
            RemoveNormalTexture();
            RemoveMetallicTexture();
            RemoveRoughnessTexture();
            RemoveAOTexture();
            RemoveEmissiveTexture();
            Console.WriteLine($"[Material] All PBR textures removed for material: {Name}");
        }

        // ============================================
        // FUNCIONES GENÉRICAS (mantener compatibilidad)
        // ============================================

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

        // ============================================
        // UNIFORMS
        // ============================================

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

        // ============================================
        // FUNCIONES PARA AJUSTAR PROPIEDADES PBR
        // ============================================

        /// <summary>
        /// Ajusta el color base del material (Albedo)
        /// </summary>
        public void SetAlbedo(Vector3 color)
        {
            AlbedoColor = color;
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
        }

        /// <summary>
        /// Ajusta el color base del material usando valores RGB (0-255)
        /// </summary>
        public void SetAlbedo(float r, float g, float b)
        {
            AlbedoColor = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_AlbedoColor", AlbedoColor);
        }

        /// <summary>
        /// Ajusta qué tan metálico es el material (0 = dieléctrico, 1 = metálico)
        /// </summary>
        public void SetMetallic(float value)
        {
            Metallic = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_Metallic", Metallic);
        }

        /// <summary>
        /// Ajusta qué tan rugoso es el material (0 = suave/brillante, 1 = rugoso/mate)
        /// </summary>
        public void SetRoughness(float value)
        {
            Roughness = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_Roughness", Roughness);
        }

        /// <summary>
        /// Ajusta el ambient occlusion (0 = completamente ocluido, 1 = sin oclusión)
        /// </summary>
        public void SetAO(float value)
        {
            AO = MathHelper.Clamp(value, 0f, 1f);
            SetFloatCached("u_AO", AO);
        }

        /// <summary>
        /// Ajusta el color emisivo (luz propia del material)
        /// </summary>
        public void SetEmissive(Vector3 color)
        {
            EmissiveColor = color;
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        /// <summary>
        /// Ajusta el color emisivo usando valores RGB (0-255)
        /// </summary>
        public void SetEmissive(float r, float g, float b)
        {
            EmissiveColor = new Vector3(r / 255f, g / 255f, b / 255f);
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        /// <summary>
        /// Ajusta la intensidad del emisivo
        /// </summary>
        public void SetEmissiveIntensity(float intensity)
        {
            EmissiveColor *= intensity;
            SetVector3Cached("u_EmissiveColor", EmissiveColor);
        }

        /// <summary>
        /// Configura todas las propiedades PBR de una vez
        /// </summary>
        public void SetPBRValues(
            Vector3? albedo = null,
            float? metallic = null,
            float? roughness = null,
            float? ao = null,
            Vector3? emissive = null)
        {
            if (albedo.HasValue)
                AlbedoColor = albedo.Value;

            if (metallic.HasValue)
                Metallic = MathHelper.Clamp(metallic.Value, 0f, 1f);

            if (roughness.HasValue)
                Roughness = MathHelper.Clamp(roughness.Value, 0f, 1f);

            if (ao.HasValue)
                AO = MathHelper.Clamp(ao.Value, 0f, 1f);

            if (emissive.HasValue)
                EmissiveColor = emissive.Value;

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