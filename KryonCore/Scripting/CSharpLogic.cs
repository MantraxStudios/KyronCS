using KrayonCore.Core.Attributes;
using System;
using System.Reflection;

namespace KrayonCore.Core.Components
{
    public class CSharpLogic : Component
    {
        private string _script = string.Empty;
        private object _scriptInstance = null;
        private MethodInfo _startMethod = null;
        private MethodInfo _updateMethod = null;
        private MethodInfo _destroyMethod = null;

        [ToStorage]
        public string Script
        {
            get => _script;
            set
            {
                if (_script == value) return;
                _script = value;
                LoadScript();
            }
        }

        public override void Awake()
        {
            // Asegurar que el manager está inicializado
            CSharpScriptManager.Instance.Initialize();

            if (!string.IsNullOrEmpty(_script))
                LoadScript();
        }

        public override void Start()
        {
            InvokeMethod(_startMethod);
        }

        public override void Update(float deltaTime)
        {
            if (_updateMethod == null) return;
            try
            {
                _updateMethod.Invoke(_scriptInstance, new object[] { deltaTime });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSharp Update Error] {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public override void OnDestroy()
        {
            InvokeMethod(_destroyMethod);
            _scriptInstance = null;
        }

        public void LoadScript()
        {
            if (string.IsNullOrEmpty(_script)) return;

            try
            {
                // Crear instancia a través del manager
                _scriptInstance = CSharpScriptManager.Instance.CreateScriptInstance(_script, GameObject);

                if (_scriptInstance == null) return;

                // Obtener métodos
                _startMethod = CSharpScriptManager.Instance.GetMethod(_script, "Start");
                _updateMethod = CSharpScriptManager.Instance.GetMethod(_script, "Update");
                _destroyMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnDestroy");

                Console.WriteLine($"[CSharp] Script '{_script}' inicializado en {GameObject.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSharp Load Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Reinicia el script actual destruyendo la instancia y creando una nueva
        /// </summary>
        public void RestartScript()
        {
            if (string.IsNullOrEmpty(_script))
            {
                Console.WriteLine("[CSharp] No hay script para reiniciar");
                return;
            }

            Console.WriteLine($"[CSharp] Reiniciando script '{_script}' en {GameObject.Name}...");

            // Llamar OnDestroy de la instancia actual
            InvokeMethod(_destroyMethod);

            // Limpiar la instancia actual
            _scriptInstance = null;
            _startMethod = null;
            _updateMethod = null;
            _destroyMethod = null;

            // Cargar nueva instancia
            LoadScript();

            // Llamar Start en la nueva instancia
            InvokeMethod(_startMethod);

            Console.WriteLine($"[CSharp] Script '{_script}' reiniciado correctamente");
        }

        private void InvokeMethod(MethodInfo method)
        {
            if (method == null) return;
            try
            {
                method.Invoke(_scriptInstance, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSharp Error] {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }
}