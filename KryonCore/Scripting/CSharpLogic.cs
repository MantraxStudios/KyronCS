using KrayonCore.Core.Attributes;
using KrayonCore.Physics;
using KrayonCore.Utilities;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KrayonCore.Core.Components
{
    public class CSharpLogic : Component
    {
        private string __script = string.Empty;
        private object _scriptInstance = null;
        private MethodInfo _startMethod = null;
        private MethodInfo _updateMethod = null;
        private MethodInfo _destroyMethod = null;

        private MethodInfo _onCollisionEnterMethod = null;
        private MethodInfo _onCollisionStayMethod = null;
        private MethodInfo _onCollisionExitMethod = null;
        private MethodInfo _onTriggerEnterMethod = null;
        private MethodInfo _onTriggerStayMethod = null;
        private MethodInfo _onTriggerExitMethod = null;

        private Rigidbody _rigidbody = null;
        private bool _subscribedToEvents = false;

        private static readonly HashSet<Type> _serializableTypes = new()
        {
            typeof(bool),    typeof(int),    typeof(float),
            typeof(double),  typeof(string), typeof(decimal),
            typeof(long),    typeof(short),  typeof(byte),
            typeof(uint),    typeof(ulong),
            typeof(Vector2), typeof(Vector3), typeof(Vector4),
            typeof(Quaternion)
        };

        [ToStorage]
        public string Script
        {
            get => __script;
            set
            {
                if (__script == value) return;
                __script = value;
                LoadScript();
            }
        }

        [ToStorage]
        public Dictionary<string, object> ScriptVariableValues { get; set; } = new Dictionary<string, object>();

        public override void Awake()
        {
            CSharpScriptManager.Instance.Initialize();

            if (!string.IsNullOrEmpty(Script))
                LoadScript();
        }

        public override void Start()
        {
            if (!AppInfo.IsPlayingGame) return;

            SubscribeToCollisionEvents();
            InvokeMethod(_startMethod);
        }

        public override void Update(float deltaTime)
        {
            if (!AppInfo.IsPlayingGame) return;

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
            if (!AppInfo.IsPlayingGame) return;

            UnsubscribeFromCollisionEvents();
            InvokeMethod(_destroyMethod);
            _scriptInstance = null;
        }

        public void LoadScript()
        {
            if (string.IsNullOrEmpty(Script)) return;

            string scriptName = string.Empty;

            if (Guid.TryParse(Script, out Guid guid))
            {
                var asset = AssetManager.Get(guid);
                if (asset != null)
                    scriptName = PathUtils.GetFileNameWithoutExtension(asset.Path);
            }

            if (string.IsNullOrEmpty(scriptName)) return;

            Console.WriteLine($"[CSharp] LoadScript llamado, script: '{scriptName}'");

            try
            {
                CSharpScriptManager.Instance.Initialize();

                _scriptInstance = CSharpScriptManager.Instance.CreateScriptInstance(scriptName, GameObject);

                Console.WriteLine($"[CSharp] Instancia creada: {(_scriptInstance == null ? "NULL" : _scriptInstance.GetType().Name)}");

                if (_scriptInstance == null) return;

                _startMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "Start");
                _updateMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "Update");
                _destroyMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnDestroy");

                _onCollisionEnterMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnCollisionEnter");
                _onCollisionStayMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnCollisionStay");
                _onCollisionExitMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnCollisionExit");
                _onTriggerEnterMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnTriggerEnter");
                _onTriggerStayMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnTriggerStay");
                _onTriggerExitMethod = CSharpScriptManager.Instance.GetMethod(scriptName, "OnTriggerExit");

                ApplyStoredVariables();

                if (AppInfo.IsPlayingGame)
                {
                    SubscribeToCollisionEvents();
                    ResolveGameObjectReferences();
                }
                Console.WriteLine($"[CSharp] Script '{scriptName}' inicializado en {GameObject?.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CSharp Load Error] {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void RestartScript()
        {
            if (string.IsNullOrEmpty(Script))
            {
                Console.WriteLine("[CSharp] No hay script para reiniciar");
                return;
            }

            UnsubscribeFromCollisionEvents();
            InvokeMethod(_destroyMethod);

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

            LoadScript();
            ResolveGameObjectReferences();
            SubscribeToCollisionEvents();
            InvokeMethod(_startMethod);
        }

        // ─────────────────────────────────────────────────────────
        //  Script Variable API
        // ─────────────────────────────────────────────────────────

        public List<(string Name, Type Type, object Value)> GetScriptVariables()
        {
            var result = new List<(string, Type, object)>();
            if (_scriptInstance == null) return result;

            Type t = _scriptInstance.GetType();
            // Solo miramos miembros declarados en el script, no en clases base
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (var field in t.GetFields(bindingFlags))
            {
                if (field.GetCustomAttribute<NoSerializeToInspectorAttribute>() != null) continue;
                result.Add((field.Name, field.FieldType, field.GetValue(_scriptInstance)));
            }

            foreach (var prop in t.GetProperties(bindingFlags))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.GetCustomAttribute<NoSerializeToInspectorAttribute>() != null) continue;
                result.Add((prop.Name, prop.PropertyType, prop.GetValue(_scriptInstance)));
            }

            return result;
        }
        /// <summary>
        /// Setea el valor en la instancia y lo persiste si es serializable.
        /// Para GameObjects guarda el GUID como string.
        /// </summary>
        public void SetScriptVariable(string name, object value)
        {
            if (_scriptInstance == null) return;

            Type t = _scriptInstance.GetType();

            var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_scriptInstance, value);

                if (field.FieldType == typeof(GameObject))
                {
                    var go = value as GameObject;
                    if (go != null)
                        ScriptVariableValues[name] = go.Id.ToString();
                }
                else if (IsSerializable(field.FieldType))
                {
                    ScriptVariableValues[name] = value;
                }
                return;
            }

            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(_scriptInstance, value);

                if (prop.PropertyType == typeof(GameObject))
                {
                    var go = value as GameObject;
                    if (go != null)
                        ScriptVariableValues[name] = go.Id.ToString();
                }
                else if (IsSerializable(prop.PropertyType))
                {
                    ScriptVariableValues[name] = value;
                }
            }
        }

        /// <summary>
        /// Setea el valor en la instancia SIN persistirlo. Para tipos no serializables.
        /// </summary>
        public void SetScriptVariableDirect(string name, object value)
        {
            if (_scriptInstance == null) return;

            Type t = _scriptInstance.GetType();

            var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) { field.SetValue(_scriptInstance, value); return; }

            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite) prop.SetValue(_scriptInstance, value);
        }

        private bool IsSerializable(Type type)
        {
            return _serializableTypes.Contains(type) || type.IsEnum;
        }

        private void ApplyStoredVariables()
        {
            if (_scriptInstance == null || ScriptVariableValues == null) return;

            Type t = _scriptInstance.GetType();

            foreach (var kvp in ScriptVariableValues)
            {
                try
                {
                    var field = t.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        // GameObjects los resolvemos después, saltamos por ahora
                        if (field.FieldType == typeof(GameObject)) continue;

                        object resolved = Convert.ChangeType(kvp.Value, field.FieldType);
                        if (resolved != null) field.SetValue(_scriptInstance, resolved);
                        continue;
                    }

                    var prop = t.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        if (prop.PropertyType == typeof(GameObject)) continue;

                        object resolved = Convert.ChangeType(kvp.Value, prop.PropertyType);
                        if (resolved != null) prop.SetValue(_scriptInstance, resolved);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CSharp] No se pudo restaurar '{kvp.Key}': {ex.Message}");
                }
            }
        }


        public void ResolveGameObjectReferences()
        {
            if (_scriptInstance == null || ScriptVariableValues == null) return;

            Type t = _scriptInstance.GetType();

            foreach (var kvp in ScriptVariableValues)
            {
                try
                {
                    var field = t.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(GameObject))
                    {
                        if (kvp.Value is string guidStr && Guid.TryParse(guidStr, out Guid guid))
                        {
                            var go = SceneManager.FindGameObjectById(guid);
                            Console.WriteLine($"[CSharp] Resolviendo '{kvp.Key}' -> {(go?.Name ?? "NULL")} | Objects In Scene {SceneManager.PrimaryScene.GetAllGameObjects().Count} | GUID: {guid}");
                            field.SetValue(_scriptInstance, go);
                        }
                        continue;
                    }

                    var prop = t.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(GameObject))
                    {
                        if (kvp.Value is string guidStr && Guid.TryParse(guidStr, out Guid guid))
                        {
                            var go = SceneManager.FindGameObjectById(guid);
                            Console.WriteLine($"[CSharp] Resolviendo '{kvp.Key}' -> {(go?.Name ?? "NULL")} | Objects In Scene {SceneManager.PrimaryScene.GetAllGameObjects().Count} | GUID: {guid}");
                            prop.SetValue(_scriptInstance, go);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CSharp] Error resolviendo ref '{kvp.Key}': {ex.Message}");
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Internals
        // ─────────────────────────────────────────────────────────

        private void InvokeMethod(MethodInfo method)
        {
            if (!AppInfo.IsPlayingGame) return;
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

        private void SubscribeToCollisionEvents()
        {
            if (!AppInfo.IsPlayingGame) return;

            UnsubscribeFromCollisionEvents();

            bool hasAny =
                _onCollisionEnterMethod != null || _onCollisionStayMethod != null || _onCollisionExitMethod != null ||
                _onTriggerEnterMethod != null || _onTriggerStayMethod != null || _onTriggerExitMethod != null;

            if (!hasAny) return;

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
            if (!AppInfo.IsPlayingGame) return;
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