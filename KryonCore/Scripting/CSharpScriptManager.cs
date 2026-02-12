using KrayonCore.Core.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace KrayonCore.Core.Components
{
    public class CSharpScriptManager
    {
        private static CSharpScriptManager _instance;
        public static CSharpScriptManager Instance => _instance ??= new CSharpScriptManager();

        private AssemblyLoadContext _loadContext;
        private Assembly _clientAssembly;
        private Dictionary<string, Type> _scriptTypes = new Dictionary<string, Type>();
        private bool _isLoaded = false;

        private CSharpScriptManager() { }

        public void Initialize()
        {
            if (_isLoaded) return;

            try
            {
                string filePath = AssetManager.ClientDLLPath + "KrayonClient.dll";

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[ScriptManager Error] No se encontró el DLL en: {filePath}");
                    return;
                }

                byte[] bytes = File.ReadAllBytes(filePath);
                _loadContext = new AssemblyLoadContext("KrayonScriptContext", isCollectible: true);

                using var ms = new MemoryStream(bytes);
                _clientAssembly = _loadContext.LoadFromStream(ms);

                // Cachear todos los tipos de scripts
                var scriptTypes = _clientAssembly.GetTypes()
                    .Where(t => t.BaseType?.Name == "KrayonBehaviour");

                foreach (var type in scriptTypes)
                {
                    _scriptTypes[type.Name] = type;
                }

                _isLoaded = true;
                Console.WriteLine($"[ScriptManager] DLL cargado correctamente. {_scriptTypes.Count} scripts encontrados.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptManager Error] {ex.Message}");
            }
        }

        public object CreateScriptInstance(string scriptName, GameObject gameObject)
        {
            if (!_isLoaded)
            {
                Console.WriteLine("[ScriptManager Error] Manager no inicializado. Llama a Initialize() primero.");
                return null;
            }

            if (!_scriptTypes.TryGetValue(scriptName, out Type scriptType))
            {
                Console.WriteLine($"[ScriptManager Error] No se encontró la clase '{scriptName}' en el DLL");
                return null;
            }

            try
            {
                var instance = Activator.CreateInstance(scriptType);

                // Asignar GameObject
                var goProp = scriptType.GetProperty("GameObject");
                goProp?.SetValue(instance, gameObject);

                return instance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptManager Error] No se pudo crear instancia de '{scriptName}': {ex.Message}");
                return null;
            }
        }

        public MethodInfo GetMethod(string scriptName, string methodName)
        {
            if (!_scriptTypes.TryGetValue(scriptName, out Type scriptType))
                return null;

            return scriptType.GetMethod(methodName);
        }

        public void Reload()
        {
            Unload();
            Initialize();
        }

        public void Unload()
        {
            _scriptTypes.Clear();
            _clientAssembly = null;

            if (_loadContext != null)
            {
                _loadContext.Unload();
                _loadContext = null;
            }

            _isLoaded = false;
            Console.WriteLine("[ScriptManager] DLL descargado.");
        }
    }
}