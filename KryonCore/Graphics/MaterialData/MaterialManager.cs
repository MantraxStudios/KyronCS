using System;
using System.Collections.Generic;
using System.IO;
using KrayonCore.Core.Attributes;
using Newtonsoft;
using Newtonsoft.Json.Linq;

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
                    ["Name"] = kvp.Value.Name,
                    ["AlbedoPath"] = MT.MainTexture.GetTexturePath,
                    ["ShaderPath"] = MT.Shader.VertexPath,
                    ["GUID"] = MT.Guid
                };

                materialsObj[kvp.Key] = mat;
            }

            data["Materials"] = materialsObj;

            File.WriteAllText("materials.json", data.ToString());
        }

        public void LoadMaterialsData()
        {
            if (!File.Exists("materials.json"))
                return;

            JObject root = JObject.Parse(File.ReadAllText("materials.json"));
            JObject materialsObj = (JObject)root["Materials"];

            if (materialsObj == null)
                return;

            _materials.Clear();

            foreach (var matProp in materialsObj.Properties())
            {
                string key = matProp.Name;
                JObject matJson = (JObject)matProp.Value;

                string name = matJson["Name"]?.ToString();
                string albedoPath = matJson["AlbedoPath"]?.ToString();
                string fragPath = matJson["ShaderPath"]?.ToString();

                string shaderBasePath = Path.Combine(
                    Path.GetDirectoryName(fragPath) ?? "",
                    Path.GetFileNameWithoutExtension(fragPath)
                );

                Material material = Create(name, shaderBasePath);

                // VERIFICAR SI EL MATERIAL SE CREÓ CORRECTAMENTE
                if (material == null)
                {
                    Console.WriteLine($"[MaterialManager] Failed to create material '{name}' during load. Skipping.");
                    continue;
                }

                var guidToken = matJson?["GUID"];
                var guidString = guidToken?.ToString();
                if (!string.IsNullOrEmpty(guidString) && Guid.TryParse(guidString, out var guid))
                {
                    material.Guid = guid;
                }

                if (!string.IsNullOrEmpty(albedoPath))
                {
                    material.LoadMainTexture(albedoPath);
                }

                Console.WriteLine($"*********** Material '{name}' Cargado");
            }
        }
    }
}