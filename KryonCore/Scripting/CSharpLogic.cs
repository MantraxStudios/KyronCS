using KrayonCore.Core.Attributes;
using KrayonCore.Physics;
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

        // Collision/Trigger methods (resolved via reflection)
        private MethodInfo _onCollisionEnterMethod = null;
        private MethodInfo _onCollisionStayMethod = null;
        private MethodInfo _onCollisionExitMethod = null;
        private MethodInfo _onTriggerEnterMethod = null;
        private MethodInfo _onTriggerStayMethod = null;
        private MethodInfo _onTriggerExitMethod = null;

        private Rigidbody _rigidbody = null;
        private bool _subscribedToEvents = false;

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
            if (!AppInfo.IsCompiledGame) return;

            CSharpScriptManager.Instance.Initialize();

            if (!string.IsNullOrEmpty(_script))
                LoadScript();
        }

        public override void Start()
        {
            if (!AppInfo.IsCompiledGame) return;

            SubscribeToCollisionEvents();

            InvokeMethod(_startMethod);
        }

        public override void Update(float deltaTime)
        {
            if (!AppInfo.IsCompiledGame) return;

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
            if (!AppInfo.IsCompiledGame) return;

            UnsubscribeFromCollisionEvents();
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

                // Obtener métodos de lifecycle
                _startMethod = CSharpScriptManager.Instance.GetMethod(_script, "Start");
                _updateMethod = CSharpScriptManager.Instance.GetMethod(_script, "Update");
                _destroyMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnDestroy");

                // Obtener métodos de colisión/trigger
                _onCollisionEnterMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnCollisionEnter");
                _onCollisionStayMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnCollisionStay");
                _onCollisionExitMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnCollisionExit");
                _onTriggerEnterMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnTriggerEnter");
                _onTriggerStayMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnTriggerStay");
                _onTriggerExitMethod = CSharpScriptManager.Instance.GetMethod(_script, "OnTriggerExit");

                SubscribeToCollisionEvents();

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

            // Desuscribir eventos y llamar OnDestroy de la instancia actual
            UnsubscribeFromCollisionEvents();
            InvokeMethod(_destroyMethod);

            // Limpiar la instancia actual
            _scriptInstance = null;
            _startMethod = null;
            _updateMethod = null;
            _destroyMethod = null;
            _onCollisionEnterMethod = null;
            _onCollisionStayMethod = null;
            _onCollisionExitMethod = null;
            _onTriggerEnterMethod = null;
            _onTriggerStayMethod = null;
            _onTriggerExitMethod = null;

            // Cargar nueva instancia
            LoadScript();
            SubscribeToCollisionEvents();

            // Llamar Start en la nueva instancia
            InvokeMethod(_startMethod);

            Console.WriteLine($"[CSharp] Script '{_script}' reiniciado correctamente");
        }

        private void InvokeMethod(MethodInfo method)
        {
            if (!AppInfo.IsCompiledGame) return;

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

        // ─────────────────────────────────────────────────────────
        //  Collision / Trigger event wiring
        // ─────────────────────────────────────────────────────────

        private void SubscribeToCollisionEvents()
        {
            if (!AppInfo.IsCompiledGame) return;

            UnsubscribeFromCollisionEvents();

            bool hasAnyCollisionMethod =
                _onCollisionEnterMethod != null || _onCollisionStayMethod != null || _onCollisionExitMethod != null ||
                _onTriggerEnterMethod != null || _onTriggerStayMethod != null || _onTriggerExitMethod != null;

            if (!hasAnyCollisionMethod) return;

            _rigidbody = GameObject?.GetComponent<Rigidbody>();
            if (_rigidbody == null) return;

            if (_onCollisionEnterMethod != null) _rigidbody.CollisionEnter += HandleCollisionEnter;
            if (_onCollisionStayMethod != null) _rigidbody.CollisionStay += HandleCollisionStay;
            if (_onCollisionExitMethod != null) _rigidbody.CollisionExit += HandleCollisionExit;
            if (_onTriggerEnterMethod != null) _rigidbody.TriggerEnter += HandleTriggerEnter;
            if (_onTriggerStayMethod != null) _rigidbody.TriggerStay += HandleTriggerStay;
            if (_onTriggerExitMethod != null) _rigidbody.TriggerExit += HandleTriggerExit;

            _subscribedToEvents = true;
        }

        private void UnsubscribeFromCollisionEvents()
        {
            if (!AppInfo.IsCompiledGame) return;

            if (!_subscribedToEvents || _rigidbody == null) return;

            _rigidbody.CollisionEnter -= HandleCollisionEnter;
            _rigidbody.CollisionStay -= HandleCollisionStay;
            _rigidbody.CollisionExit -= HandleCollisionExit;
            _rigidbody.TriggerEnter -= HandleTriggerEnter;
            _rigidbody.TriggerStay -= HandleTriggerStay;
            _rigidbody.TriggerExit -= HandleTriggerExit;

            _rigidbody = null;
            _subscribedToEvents = false;
        }

        private GameObject ResolveOtherGameObject(ContactInfo contact)
        {
            var eventSystem = GameObject?.Scene?.PhysicsWorld?.EventSystem;
            return eventSystem?.GetGameObject(contact.OtherCollidable);
        }

        private void InvokeCollisionMethod(MethodInfo method, ContactInfo contact)
        {
            if (!AppInfo.IsCompiledGame) return;

            if (method == null || _scriptInstance == null) return;
            try
            {
                var otherGo = ResolveOtherGameObject(contact);
                method.Invoke(_scriptInstance, new object[] { otherGo });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSharp Collision Error] {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private void HandleCollisionEnter(ContactInfo contact) => InvokeCollisionMethod(_onCollisionEnterMethod, contact);
        private void HandleCollisionStay(ContactInfo contact) => InvokeCollisionMethod(_onCollisionStayMethod, contact);
        private void HandleCollisionExit(ContactInfo contact) => InvokeCollisionMethod(_onCollisionExitMethod, contact);
        private void HandleTriggerEnter(ContactInfo contact) => InvokeCollisionMethod(_onTriggerEnterMethod, contact);
        private void HandleTriggerStay(ContactInfo contact) => InvokeCollisionMethod(_onTriggerStayMethod, contact);
        private void HandleTriggerExit(ContactInfo contact) => InvokeCollisionMethod(_onTriggerExitMethod, contact);
    }
}