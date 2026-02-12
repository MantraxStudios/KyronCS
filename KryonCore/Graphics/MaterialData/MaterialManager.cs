using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KrayonCore
{
    public sealed class MaterialManager
    {
        private readonly Dictionary<string, Material> _materials = new();

        public Material Create(string name, Guid vertexGuid, Guid fragmentGuid)
        {
            if (_materials.ContainsKey(name))
            {
                Console.WriteLine($"[MaterialManager] Material '{name}' already exists. Returning existing material.");
                return _materials[name];
            }

            try
            {
                var material = new Material(name, vertexGuid, fragmentGuid);
                _materials[name] = material;
                Console.WriteLine($"[MaterialManager] Material '{name}' created successfully");
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

                    ["VertexShaderGUID"] = MT.VertexShaderGUID != Guid.Empty ? MT.VertexShaderGUID.ToString() : null,
                    ["FragmentShaderGUID"] = MT.FragmentShaderGUID != Guid.Empty ? MT.FragmentShaderGUID.ToString() : null,

                    ["AlbedoTextureGUID"] = MT.AlbedoTextureGUID != Guid.Empty ? MT.AlbedoTextureGUID.ToString() : null,
                    ["NormalTextureGUID"] = MT.NormalTextureGUID != Guid.Empty ? MT.NormalTextureGUID.ToString() : null,
                    ["MetallicTextureGUID"] = MT.MetallicTextureGUID != Guid.Empty ? MT.MetallicTextureGUID.ToString() : null,
                    ["RoughnessTextureGUID"] = MT.RoughnessTextureGUID != Guid.Empty ? MT.RoughnessTextureGUID.ToString() : null,
                    ["AOTextureGUID"] = MT.AOTextureGUID != Guid.Empty ? MT.AOTextureGUID.ToString() : null,
                    ["EmissiveTextureGUID"] = MT.EmissiveTextureGUID != Guid.Empty ? MT.EmissiveTextureGUID.ToString() : null,

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

                    ["UseAlbedoMap"] = MT.UseAlbedoMap,
                    ["UseNormalMap"] = MT.UseNormalMap,
                    ["UseMetallicMap"] = MT.UseMetallicMap,
                    ["UseRoughnessMap"] = MT.UseRoughnessMap,
                    ["UseAOMap"] = MT.UseAOMap,
                    ["UseEmissiveMap"] = MT.UseEmissiveMap,

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

            string savePath = AssetManager.BasePath + "materials.json";
            File.WriteAllText(savePath, data.ToString());
            Console.WriteLine($"[MaterialManager] Saved {_materials.Count} materials to materials.json");
        }

        public void LoadMaterialsData()
        {
            string materialsPath = AssetManager.BasePath + "materials.json";

            string jsonContent;

            if (AppInfo.IsCompiledGame)
            {
                var asset = AssetManager.FindByPath("materials.json");
                if (asset == null)
                {
                    Console.WriteLine("[MaterialManager] materials.json not found in AssetManager");
                    return;
                }
                byte[] bytes = AssetManager.GetBytes(asset.Guid);
                if (bytes == null)
                {
                    Console.WriteLine("[MaterialManager] Could not read materials.json");
                    return;
                }
                jsonContent = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                if (!File.Exists(materialsPath))
                {
                    Console.WriteLine("[MaterialManager] materials.json not found");
                    return;
                }
                jsonContent = File.ReadAllText(materialsPath);
            }

            JObject root = JObject.Parse(jsonContent);
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

                Guid vertexGuid = ParseGuid(matJson["VertexShaderGUID"]);
                Guid fragmentGuid = ParseGuid(matJson["FragmentShaderGUID"]);

                if (string.IsNullOrEmpty(name) || vertexGuid == Guid.Empty || fragmentGuid == Guid.Empty)
                {
                    Console.WriteLine($"[MaterialManager] Skipping invalid material entry: {key}");
                    continue;
                }

                Material material = Create(name, vertexGuid, fragmentGuid);

                if (material == null)
                {
                    Console.WriteLine($"[MaterialManager] Failed to create material '{name}' during load. Skipping.");
                    continue;
                }

                Guid albedoGuid = ParseGuid(matJson["AlbedoTextureGUID"]);
                Guid normalGuid = ParseGuid(matJson["NormalTextureGUID"]);
                Guid metallicGuid = ParseGuid(matJson["MetallicTextureGUID"]);
                Guid roughnessGuid = ParseGuid(matJson["RoughnessTextureGUID"]);
                Guid aoGuid = ParseGuid(matJson["AOTextureGUID"]);
                Guid emissiveGuid = ParseGuid(matJson["EmissiveTextureGUID"]);

                if (albedoGuid != Guid.Empty) material.LoadAlbedoTexture(albedoGuid);
                if (normalGuid != Guid.Empty) material.LoadNormalTexture(normalGuid);
                if (metallicGuid != Guid.Empty) material.LoadMetallicTexture(metallicGuid);
                if (roughnessGuid != Guid.Empty) material.LoadRoughnessTexture(roughnessGuid);
                if (aoGuid != Guid.Empty) material.LoadAOTexture(aoGuid);
                if (emissiveGuid != Guid.Empty) material.LoadEmissiveTexture(emissiveGuid);

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

                material.UseAlbedoMap = matJson["UseAlbedoMap"]?.Value<bool>() ?? false;
                material.UseNormalMap = matJson["UseNormalMap"]?.Value<bool>() ?? false;
                material.UseMetallicMap = matJson["UseMetallicMap"]?.Value<bool>() ?? false;
                material.UseRoughnessMap = matJson["UseRoughnessMap"]?.Value<bool>() ?? false;
                material.UseAOMap = matJson["UseAOMap"]?.Value<bool>() ?? false;
                material.UseEmissiveMap = matJson["UseEmissiveMap"]?.Value<bool>() ?? false;

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

                material.SetPBRProperties();

                Console.WriteLine($"[MaterialManager] Material '{name}' loaded successfully");
            }

            Console.WriteLine($"[MaterialManager] Loaded {_materials.Count} materials from materials.json");
        }

        private static Guid ParseGuid(JToken token)
        {
            string str = token?.ToString();
            if (!string.IsNullOrEmpty(str) && Guid.TryParse(str, out Guid result))
                return result;
            return Guid.Empty;
        }
    }
}