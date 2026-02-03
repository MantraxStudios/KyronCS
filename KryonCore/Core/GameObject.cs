using Assimp;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public GameObject(string name = "GameObject")
        {
            Id = Guid.NewGuid();
            Name = name;
            _components = new Dictionary<Type, Component>();
            _componentsList = new List<Component>();

            Transform = AddComponent<Transform>();
        }

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

            return component;
        }

        /// <summary>
        /// Añade un componente sin llamar a Awake (útil para deserialización)
        /// </summary>
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

            // NO llamar a Awake aquí - se llamará después de deserializar

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
                return _components.Remove(type);
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

        internal void DestroyComponents()
        {
            foreach (var component in _componentsList)
            {
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

        public static GameObject Instantiate(GameObject original)
        {
            var newObj = new GameObject(original.Name + " (Clone)");
            newObj.Tag = original.Tag;
            newObj.Active = original.Active;

            SceneManager.ActiveScene?.AddGameObject(newObj);

            return newObj;
        }
    }
}