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
                byte[] bytes;

                if (AppInfo.IsCompiledGame)
                {
                    bytes = AssetManager.GetBytes("Engine.Client.KrayonClient");
                    if (bytes == null)
                    {
                        Console.WriteLine("[ScriptManager Error] No se pudo leer KrayonClient.dll desde Pak");
                        return;
                    }
                }
                else
                {
                    string filePath = Path.Combine(AssetManager.ClientDLLPath, "KrayonClient.dll");
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"[ScriptManager Error] No se encontró el DLL en: {filePath}");
                        return;
                    }
                    bytes = File.ReadAllBytes(filePath);
                }

                _loadContext = new AssemblyLoadContext("KrayonScriptContext", isCollectible: true);

                using var ms = new MemoryStream(bytes);
                _clientAssembly = _loadContext.LoadFromStream(ms);

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
                Console.WriteLine($"[ScriptManager Error] {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[ScriptManager Error] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[ScriptManager Error] Inner: {ex.InnerException.Message}");
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
                var weakRef = new WeakReference(_loadContext);
                _loadContext.Unload();
                _loadContext = null;

                for (int i = 0; i < 10 && weakRef.IsAlive; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (weakRef.IsAlive)
                    Console.WriteLine("[ScriptManager Warning] El contexto anterior no se liberó completamente.");
            }

            _isLoaded = false;
            Console.WriteLine("[ScriptManager] DLL descargado.");
        }
    }
}