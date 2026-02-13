using Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KrayonCore
{
    public class GameObject
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public string Tag { get; set; } = "Untagged";
        public bool Active { get; set; } = true;
        public GameScene Scene { get; internal set; }

        private Dictionary<Type, Component> _components;
        private List<Component> _componentsList;
        public Transform Transform { get; private set; }

        public event Action OnComponentAdded;
        public event Action OnComponentRemoved;

        public GameObject(string name = "GameObject")
        {
            Id = Guid.NewGuid();
            Name = name;
            _components = new Dictionary<Type, Component>();
            _componentsList = new List<Component>();

            Transform = AddComponent<Transform>();
        }

        public GameObject Clone(bool cloneChildren = true)
        {
            GameObject clone = new GameObject(this.Name + " (Clone)");
            clone.Tag = this.Tag;
            clone.Active = this.Active;

            clone.Transform.SetPosition(
                this.Transform.X,
                this.Transform.Y,
                this.Transform.Z
            );
            clone.Transform.SetRotation(
                this.Transform.RotationX,
                this.Transform.RotationY,
                this.Transform.RotationZ
            );
            clone.Transform.SetScale(
                this.Transform.ScaleX,
                this.Transform.ScaleY,
                this.Transform.ScaleZ
            );

            foreach (var component in _componentsList)
            {
                if (component is Transform)
                    continue;

                CloneComponent(component, clone);
            }

            if (cloneChildren)
            {
                foreach (var child in this.Transform.Children)
                {
                    GameObject childClone = child.GameObject.Clone(true);
                    childClone.Transform.SetParent(clone.Transform);
                }
            }

            if (SceneManager.ActiveScene != null)
            {
                SceneManager.ActiveScene.AddGameObject(clone);
            }

            return clone;
        }

        private void CloneComponent(Component original, GameObject target)
        {
            Type componentType = original.GetType();

            Component newComponent = target.AddComponent(componentType);

            PropertyInfo[] properties = componentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance
            );

            foreach (var property in properties)
            {
                if (!property.CanWrite || 
                    !property.CanRead || 
                    property.Name == "GameObject" ||
                    property.Name == "Transform")
                    continue;

                try
                {
                    object value = property.GetValue(original);
                    
                    if (value != null && IsCloneableType(property.PropertyType))
                    {
                        value = DeepCloneValue(value);
                    }
                    
                    property.SetValue(newComponent, value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying property {property.Name}: {ex.Message}");
                }
            }

            FieldInfo[] fields = componentType.GetFields(
                BindingFlags.Public | BindingFlags.Instance
            );

            foreach (var field in fields)
            {
                try
                {
                    object value = field.GetValue(original);
                    
                    if (value != null && IsCloneableType(field.FieldType))
                    {
                        value = DeepCloneValue(value);
                    }
                    
                    field.SetValue(newComponent, value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying field {field.Name}: {ex.Message}");
                }
            }
        }

        private bool IsCloneableType(Type type)
        {
            // Tipos que necesitan clonado profundo
            return type.IsArray || 
                   type.IsClass && 
                   type != typeof(string) && 
                   !type.IsSubclassOf(typeof(Component)) &&
                   !type.IsSubclassOf(typeof(GameObject));
        }

        private object DeepCloneValue(object original)
        {
            if (original == null)
                return null;

            Type type = original.GetType();

            // Clonar arrays
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                Array originalArray = (Array)original;
                Array clonedArray = Array.CreateInstance(elementType, originalArray.Length);
                
                for (int i = 0; i < originalArray.Length; i++)
                {
                    object element = originalArray.GetValue(i);
                    if (element != null && IsCloneableType(elementType))
                    {
                        clonedArray.SetValue(DeepCloneValue(element), i);
                    }
                    else
                    {
                        clonedArray.SetValue(element, i);
                    }
                }
                
                return clonedArray;
            }

            // Clonar listas
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (System.Collections.IList)original;
                var clonedList = (System.Collections.IList)Activator.CreateInstance(type);
                
                foreach (var item in list)
                {
                    if (item != null && IsCloneableType(item.GetType()))
                    {
                        clonedList.Add(DeepCloneValue(item));
                    }
                    else
                    {
                        clonedList.Add(item);
                    }
                }
                
                return clonedList;
            }

            // Para otros tipos, retornar el valor original
            return original;
        }

        // Método estático de utilidad
        public static GameObject Instantiate(GameObject original, bool cloneChildren = true)
        {
            return original.Clone(cloneChildren);
        }

        // Resto de métodos existentes...
        public T AddComponent<T>() where T : Component, new()
        {
            Type type = typeof(T);

            if (_components.ContainsKey(type))
            {
                return (T)_components[type];
            }

            T component = new T();
            component.GameObject = this;
            _components[type] = component;
            _componentsList.Add(component);

            component.Awake();

            OnComponentAdded?.Invoke();
            Scene?.NotifyComponentAdded();

            return component;
        }

        internal Component AddComponentWithoutAwake(Type type)
        {
            if (!typeof(Component).IsAssignableFrom(type))
                throw new ArgumentException("Type must inherit from Component");

            if (_components.ContainsKey(type))
            {
                return _components[type];
            }

            Component component = (Component)Activator.CreateInstance(type);
            component.GameObject = this;

            _components[type] = component;
            _componentsList.Add(component);

            return component;
        }

        public Component AddComponent(Type type)
        {
            if (!typeof(Component).IsAssignableFrom(type))
                throw new ArgumentException("Type must inherit from Component");

            if (_components.ContainsKey(type))
            {
                return _components[type];
            }

            Component component = (Component)Activator.CreateInstance(type);
            component.GameObject = this;

            _components[type] = component;
            _componentsList.Add(component);

            component.Awake();

            OnComponentAdded?.Invoke();
            Scene?.NotifyComponentAdded();

            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            Type type = typeof(T);

            if (_components.TryGetValue(type, out Component component))
            {
                return (T)component;
            }

            return null;
        }

        public T GetComponentInChildren<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null) return component;

            foreach (var child in Transform.Children)
            {
                component = child.GameObject.GetComponentInChildren<T>();
                if (component != null) return component;
            }

            return null;
        }

        public T[] GetComponents<T>() where T : Component
        {
            return _componentsList.OfType<T>().ToArray();
        }

        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T));
        }

        public bool RemoveComponent<T>() where T : Component
        {
            Type type = typeof(T);

            if (type == typeof(Transform))
            {
                return false;
            }

            if (_components.TryGetValue(type, out Component component))
            {
                component.OnDestroy();
                _componentsList.Remove(component);
                bool removed = _components.Remove(type);

                if (removed)
                {
                    OnComponentRemoved?.Invoke();
                    Scene?.NotifyComponentRemoved();
                }

                return removed;
            }

            return false;
        }

        public bool RemoveComponent(Component component)
        {
            if (component == null)
                return false;

            if (component is Transform)
            {
                return false;
            }

            Type type = component.GetType();

            if (_components.ContainsKey(type) && _components[type] == component)
            {
                component.OnDestroy();
                _componentsList.Remove(component);
                bool removed = _components.Remove(type);

                if (removed)
                {
                    OnComponentRemoved?.Invoke();
                    Scene?.NotifyComponentRemoved();
                }

                return removed;
            }

            return false;
        }

        public IReadOnlyList<Component> GetAllComponents()
        {
            return _componentsList.AsReadOnly();
        }

        internal void StartComponents()
        {
            foreach (var component in _componentsList)
            {
                if (!component._started && component.Enabled)
                {
                    component.Start();
                    component._started = true;
                }
            }
        }

        internal void UpdateComponents(float deltaTime)
        {
            if (!Active) return;

            foreach (var component in _componentsList)
            {
                if (component.Enabled && component._started)
                {
                    component.Update(deltaTime);
                }
            }
        }

        internal void RenderComponents()
        {
            if (!Active) return;

            foreach (var component in _componentsList)
            {
                if (component.Enabled)
                {
                    component.OnWillRenderObject();
                }
            }
        }

        internal void DestroyComponents()
        {
            foreach (var component in _componentsList)
            {
                component.Enabled = false;
                component.OnDestroy();
            }
            _components.Clear();
            _componentsList.Clear();
        }

        public static GameObject Find(string name)
        {
            return SceneManager.ActiveScene?.FindGameObject(name);
        }

        public static GameObject FindWithTag(string tag)
        {
            return SceneManager.ActiveScene?.FindGameObjectWithTag(tag);
        }

        public static GameObject[] FindGameObjectsWithTag(string tag)
        {
            return SceneManager.ActiveScene?.FindGameObjectsWithTag(tag) ?? new GameObject[0];
        }

        public static void Destroy(GameObject gameObject)
        {
            gameObject?.Scene?.DestroyGameObject(gameObject);
        }
    }
}