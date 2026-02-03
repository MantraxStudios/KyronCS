using System;
using System.Collections.Generic;
using System.IO;
using KrayonCore.Core.Attributes;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public sealed class MaterialManager
    {
        private readonly Dictionary<string, Material> _materials = new();

        public Material Create(string name, Shader shader)
        {
            if (_materials.ContainsKey(name))
            {
                Console.WriteLine($"[MaterialManager] Material '{name}' already exists. Returning existing material.");
                return _materials[name];
            }

            if (shader == null)
            {
                Console.WriteLine($"[MaterialManager] Cannot create material '{name}': shader is null");
                return null;
            }

            var material = new Material(name, shader);
            _materials[name] = material;
            Console.WriteLine($"[MaterialManager] Material '{name}' created successfully");

            AssetManager.Register(material);
            return material;
        }

        public Material Create(string name, string vertexPath, string fragmentPath)
        {
            if (_materials.ContainsKey(name))
            {
                Console.WriteLine($"[MaterialManager] Material '{name}' already exists. Returning existing material.");
                return _materials[name];
            }

            if (!File.Exists(vertexPath))
            {
                Console.WriteLine($"[MaterialManager] Cannot create material '{name}': vertex shader not found at {vertexPath}");
                return null;
            }

            if (!File.Exists(fragmentPath))
            {
                Console.WriteLine($"[MaterialManager] Cannot create material '{name}': fragment shader not found at {fragmentPath}");
                return null;
            }

            try
            {
                var material = new Material(name, vertexPath, fragmentPath);
                _materials[name] = material;
                Console.WriteLine($"[MaterialManager] Material '{name}' created successfully");

                AssetManager.Register(material);
                return material;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialManager] Error creating material '{name}': {ex.Message}");
                return null;
            }
        }

        public Material Create(string name, string shaderBasePath)
        {
            if (_materials.ContainsKey(name))
            {
                Console.WriteLine($"[MaterialManager] Material '{name}' already exists. Returning existing material.");
                return _materials[name];
            }

            string vertPath = $"{shaderBasePath}.vert";
            string fragPath = $"{shaderBasePath}.frag";

            if (!File.Exists(vertPath))
            {
                Console.WriteLine($"[MaterialManager] Cannot create material '{name}': vertex shader not found at {vertPath}");
                return null;
            }

            if (!File.Exists(fragPath))
            {
                Console.WriteLine($"[MaterialManager] Cannot create material '{name}': fragment shader not found at {fragPath}");
                return null;
            }

            try
            {
                var material = new Material(name, shaderBasePath);
                _materials[name] = material;
                Console.WriteLine($"[MaterialManager] Material '{name}' created successfully");

                AssetManager.Register(material);
                return material;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialManager] Error creating material '{name}': {ex.Message}");
                return null;
            }
        }

        public Material Get(string name)
        {
            if (!_materials.TryGetValue(name, out var material))
            {
                Console.WriteLine($"[MaterialManager] Material '{name}' not found");
                return null;
            }
            return material;
        }

        public bool TryGet(string name, out Material material)
        {
            return _materials.TryGetValue(name, out material);
        }

        public bool Exists(string name)
        {
            return _materials.ContainsKey(name);
        }

        public void Remove(string name)
        {
            if (_materials.TryGetValue(name, out var material))
            {
                material.Dispose();
                _materials.Remove(name);
                Console.WriteLine($"[MaterialManager] Material '{name}' removed successfully");
            }
            else
            {
                Console.WriteLine($"[MaterialManager] Cannot remove material '{name}': not found");
            }
        }

        public void Clear()
        {
            int count = _materials.Count;
            foreach (var material in _materials.Values)
                material.Dispose();
            _materials.Clear();
            Console.WriteLine($"[MaterialManager] Cleared {count} materials");
        }

        public IEnumerable<Material> GetAll()
        {
            return _materials.Values;
        }

        public int Count => _materials.Count;

        public void SaveMaterialsData()
        {
            JObject data = new JObject();
            JObject materialsObj = new JObject();

            foreach (var kvp in _materials)
            {
                Material MT = kvp.Value;
                JObject mat = new JObject
                {
                    ["Name"] = MT.Name,
                    ["GUID"] = MT.Guid,
                    ["ShaderPath"] = MT.Shader.VertexPath,

                    // Texturas PBR
                    ["AlbedoPath"] = MT.AlbedoTexture?.GetTexturePath,
                    ["NormalPath"] = MT.NormalTexture?.GetTexturePath,
                    ["MetallicPath"] = MT.MetallicTexture?.GetTexturePath,
                    ["RoughnessPath"] = MT.RoughnessTexture?.GetTexturePath,
                    ["AOPath"] = MT.AOTexture?.GetTexturePath,
                    ["EmissivePath"] = MT.EmissiveTexture?.GetTexturePath,

                    // Propiedades PBR
                    ["AlbedoColor"] = new JObject
                    {
                        ["X"] = MT.AlbedoColor.X,
                        ["Y"] = MT.AlbedoColor.Y,
                        ["Z"] = MT.AlbedoColor.Z
                    },
                    ["Metallic"] = MT.Metallic,
                    ["Roughness"] = MT.Roughness,
                    ["AO"] = MT.AO,
                    ["EmissiveColor"] = new JObject
                    {
                        ["X"] = MT.EmissiveColor.X,
                        ["Y"] = MT.EmissiveColor.Y,
                        ["Z"] = MT.EmissiveColor.Z
                    },

                    // Flags de uso de texturas
                    ["UseAlbedoMap"] = MT.UseAlbedoMap,
                    ["UseNormalMap"] = MT.UseNormalMap,
                    ["UseMetallicMap"] = MT.UseMetallicMap,
                    ["UseRoughnessMap"] = MT.UseRoughnessMap,
                    ["UseAOMap"] = MT.UseAOMap,
                    ["UseEmissiveMap"] = MT.UseEmissiveMap,

                    // Propiedades adicionales
                    ["NormalMapIntensity"] = MT.NormalMapIntensity,
                    ["AmbientLight"] = new JObject
                    {
                        ["X"] = MT.AmbientLight.X,
                        ["Y"] = MT.AmbientLight.Y,
                        ["Z"] = MT.AmbientLight.Z
                    },
                    ["AmbientStrength"] = MT.AmbientStrength
                };

                materialsObj[kvp.Key] = mat;
            }

            data["Materials"] = materialsObj;

            File.WriteAllText("materials.json", data.ToString());
            Console.WriteLine($"[MaterialManager] Saved {_materials.Count} materials to materials.json");
        }

        public void LoadMaterialsData()
        {
            if (!File.Exists("materials.json"))
            {
                Console.WriteLine("[MaterialManager] materials.json not found");
                return;
            }

            JObject root = JObject.Parse(File.ReadAllText("materials.json"));
            JObject materialsObj = (JObject)root["Materials"];

            if (materialsObj == null)
            {
                Console.WriteLine("[MaterialManager] No materials found in JSON");
                return;
            }

            _materials.Clear();

            foreach (var matProp in materialsObj.Properties())
            {
                string key = matProp.Name;
                JObject matJson = (JObject)matProp.Value;

                string name = matJson["Name"]?.ToString();
                string fragPath = matJson["ShaderPath"]?.ToString();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fragPath))
                {
                    Console.WriteLine($"[MaterialManager] Skipping invalid material entry: {key}");
                    continue;
                }

                string shaderBasePath = Path.Combine(
                    Path.GetDirectoryName(fragPath) ?? "",
                    Path.GetFileNameWithoutExtension(fragPath)
                );

                Material material = Create(name, shaderBasePath);

                if (material == null)
                {
                    Console.WriteLine($"[MaterialManager] Failed to create material '{name}' during load. Skipping.");
                    continue;
                }

                // Cargar GUID
                var guidToken = matJson["GUID"];
                var guidString = guidToken?.ToString();
                if (!string.IsNullOrEmpty(guidString) && Guid.TryParse(guidString, out var guid))
                {
                    material.Guid = guid;
                }

                // Cargar texturas PBR
                string albedoPath = matJson["AlbedoPath"]?.ToString();
                string normalPath = matJson["NormalPath"]?.ToString();
                string metallicPath = matJson["MetallicPath"]?.ToString();
                string roughnessPath = matJson["RoughnessPath"]?.ToString();
                string aoPath = matJson["AOPath"]?.ToString();
                string emissivePath = matJson["EmissivePath"]?.ToString();

                if (!string.IsNullOrEmpty(albedoPath))
                    material.LoadAlbedoTexture(albedoPath);

                if (!string.IsNullOrEmpty(normalPath))
                    material.LoadNormalTexture(normalPath);

                if (!string.IsNullOrEmpty(metallicPath))
                    material.LoadMetallicTexture(metallicPath);

                if (!string.IsNullOrEmpty(roughnessPath))
                    material.LoadRoughnessTexture(roughnessPath);

                if (!string.IsNullOrEmpty(aoPath))
                    material.LoadAOTexture(aoPath);

                if (!string.IsNullOrEmpty(emissivePath))
                    material.LoadEmissiveTexture(emissivePath);

                // Cargar propiedades PBR
                var albedoColorObj = matJson["AlbedoColor"] as JObject;
                if (albedoColorObj != null)
                {
                    material.AlbedoColor = new Vector3(
                        albedoColorObj["X"]?.Value<float>() ?? 1f,
                        albedoColorObj["Y"]?.Value<float>() ?? 1f,
                        albedoColorObj["Z"]?.Value<float>() ?? 1f
                    );
                }

                material.Metallic = matJson["Metallic"]?.Value<float>() ?? 0f;
                material.Roughness = matJson["Roughness"]?.Value<float>() ?? 0.5f;
                material.AO = matJson["AO"]?.Value<float>() ?? 1f;

                var emissiveColorObj = matJson["EmissiveColor"] as JObject;
                if (emissiveColorObj != null)
                {
                    material.EmissiveColor = new Vector3(
                        emissiveColorObj["X"]?.Value<float>() ?? 0f,
                        emissiveColorObj["Y"]?.Value<float>() ?? 0f,
                        emissiveColorObj["Z"]?.Value<float>() ?? 0f
                    );
                }

                // Cargar flags de uso de texturas
                material.UseAlbedoMap = matJson["UseAlbedoMap"]?.Value<bool>() ?? false;
                material.UseNormalMap = matJson["UseNormalMap"]?.Value<bool>() ?? false;
                material.UseMetallicMap = matJson["UseMetallicMap"]?.Value<bool>() ?? false;
                material.UseRoughnessMap = matJson["UseRoughnessMap"]?.Value<bool>() ?? false;
                material.UseAOMap = matJson["UseAOMap"]?.Value<bool>() ?? false;
                material.UseEmissiveMap = matJson["UseEmissiveMap"]?.Value<bool>() ?? false;

                // Cargar propiedades adicionales
                material.NormalMapIntensity = matJson["NormalMapIntensity"]?.Value<float>() ?? 1f;

                var ambientLightObj = matJson["AmbientLight"] as JObject;
                if (ambientLightObj != null)
                {
                    material.AmbientLight = new Vector3(
                        ambientLightObj["X"]?.Value<float>() ?? 0.03f,
                        ambientLightObj["Y"]?.Value<float>() ?? 0.03f,
                        ambientLightObj["Z"]?.Value<float>() ?? 0.03f
                    );
                }

                material.AmbientStrength = matJson["AmbientStrength"]?.Value<float>() ?? 1f;

                // Aplicar todas las propiedades PBR
                material.SetPBRProperties();

                Console.WriteLine($"[MaterialManager] ✓ Material '{name}' loaded successfully");
            }

            Console.WriteLine($"[MaterialManager] Loaded {_materials.Count} materials from materials.json");
        }
    }
}