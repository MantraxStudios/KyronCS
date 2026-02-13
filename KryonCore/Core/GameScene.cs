using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using KrayonCore.Physics;

namespace KrayonCore
{
    public class GameScene
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public bool IsLoaded { get; internal set; }

        private Dictionary<Guid, GameObject> _gameObjects;
        private List<GameObject> _gameObjectsList;
        private List<GameObject> _toDestroy;

        private WorldPhysic _physicsWorld;
        public WorldPhysic PhysicsWorld => _physicsWorld;

        public event Action OnSceneChanged;
        public event Action<GameObject> OnGameObjectAdded;
        public event Action<GameObject> OnGameObjectRemoved;
        public event Action OnSceneCleared;

        public event Action OnComponentAdded;
        public event Action OnComponentRemoved;

        public GameScene(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
            _gameObjects = new Dictionary<Guid, GameObject>();
            _gameObjectsList = new List<GameObject>();
            _toDestroy = new List<GameObject>();
            IsLoaded = false;

            _physicsWorld = new WorldPhysic();
        }

        public GameObject CreateGameObject(string name = "GameObject")
        {
            GameObject go = new GameObject(name);
            AddGameObject(go);
            return go;
        }

        // NUEVO MÉTODO: Clonar GameObject y agregarlo a la escena
        public GameObject Instantiate(GameObject original, bool cloneChildren = true)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            // Clonar el GameObject
            GameObject clone = original.Clone(cloneChildren);
            
            // Asegurarse de que está en esta escena
            if (clone.Scene != this)
            {
                AddGameObject(clone);
            }

            // Si hay hijos clonados, asegurarse de que también estén en la escena
            if (cloneChildren)
            {
                AddClonedChildrenToScene(clone);
            }

            // Inicializar componentes si la escena está cargada
            if (IsLoaded)
            {
                clone.StartComponents();
            }

            return clone;
        }

        // SOBRECARGA: Clonar con posición específica
        public GameObject Instantiate(GameObject original, OpenTK.Mathematics.Vector3 position, bool cloneChildren = true)
        {
            GameObject clone = Instantiate(original, cloneChildren);
            clone.StartComponents();
            clone.Transform.SetPosition(position.X, position.Y, position.Z);
            return clone;
        }

        // SOBRECARGA: Clonar con posición y rotación
        public GameObject Instantiate(GameObject original, OpenTK.Mathematics.Vector3 position, OpenTK.Mathematics.Vector3 rotation, bool cloneChildren = true)
        {
            GameObject clone = Instantiate(original, cloneChildren);
            clone.StartComponents();
            clone.Transform.SetPosition(position.X, position.Y, position.Z);
            clone.Transform.SetRotation(rotation.X, rotation.Y, rotation.Z);
            return clone;
        }

        // SOBRECARGA: Clonar con Transform completo
        public GameObject Instantiate(GameObject original, OpenTK.Mathematics.Vector3 position, OpenTK.Mathematics.Vector3 rotation, OpenTK.Mathematics.Vector3 scale, bool cloneChildren = true)
        {
            GameObject clone = Instantiate(original, cloneChildren);
            clone.Transform.SetPosition(position.X, position.Y, position.Z);
            clone.Transform.SetRotation(rotation.X, rotation.Y, rotation.Z);
            clone.Transform.SetScale(scale.X, scale.Y, scale.Z);
            return clone;
        }

        private void AddClonedChildrenToScene(GameObject parent)
        {
            foreach (var child in parent.Transform.Children)
            {
                GameObject childGO = child.GameObject;
                
                if (!_gameObjects.ContainsKey(childGO.Id))
                {
                    AddGameObject(childGO);
                }
                
                // Recursivo para nietos
                AddClonedChildrenToScene(childGO);
            }
        }

        internal void AddGameObject(GameObject gameObject)
        {
            if (!_gameObjects.ContainsKey(gameObject.Id))
            {
                gameObject.Scene = this;
                _gameObjects[gameObject.Id] = gameObject;
                _gameObjectsList.Add(gameObject);

                gameObject.OnComponentAdded += NotifyComponentAdded;
                gameObject.OnComponentRemoved += NotifyComponentRemoved;

                OnGameObjectAdded?.Invoke(gameObject);
                OnSceneChanged?.Invoke();
            }
        }

        public GameObject GetGameObject(Guid id)
        {
            _gameObjects.TryGetValue(id, out GameObject go);
            return go;
        }

        public GameObject FindGameObject(string name)
        {
            return _gameObjectsList.FirstOrDefault(go => go.Name == name);
        }

        public GameObject[] FindGameObjects(string name)
        {
            return _gameObjectsList.Where(go => go.Name == name).ToArray();
        }

        public GameObject FindGameObjectWithTag(string tag)
        {
            return _gameObjectsList.FirstOrDefault(go => go.Tag == tag);
        }

        public GameObject[] FindGameObjectsWithTag(string tag)
        {
            return _gameObjectsList.Where(go => go.Tag == tag).ToArray();
        }

        public GameObject[] FindGameObjectsWithComponent<T>() where T : Component
        {
            return _gameObjectsList.Where(go => go.HasComponent<T>()).ToArray();
        }

        public void DestroyGameObject(GameObject gameObject)
        {
            if (gameObject == null || !_gameObjects.ContainsKey(gameObject.Id))
                return;

            DestroyChildren(gameObject);

            gameObject.OnComponentAdded -= NotifyComponentAdded;
            gameObject.OnComponentRemoved -= NotifyComponentRemoved;

            gameObject.DestroyComponents();
            _gameObjects.Remove(gameObject.Id);
            _gameObjectsList.Remove(gameObject);

            OnGameObjectRemoved?.Invoke(gameObject);
            OnSceneChanged?.Invoke();
        }

        private void DestroyChildren(GameObject parent)
        {
            foreach (var childTransform in parent.Transform.Children.ToList())
            {
                var child = childTransform.GameObject;
                if (child != null && _gameObjects.ContainsKey(child.Id))
                {
                    DestroyChildren(child);

                    child.OnComponentAdded -= NotifyComponentAdded;
                    child.OnComponentRemoved -= NotifyComponentRemoved;

                    child.DestroyComponents();
                    _gameObjects.Remove(child.Id);
                    _gameObjectsList.Remove(child);

                    OnGameObjectRemoved?.Invoke(child);
                }
            }
        }

        public IReadOnlyList<GameObject> GetAllGameObjects()
        {
            return _gameObjectsList.AsReadOnly();
        }

        public void OnLoad()
        {
            IsLoaded = true;
            ReinitializePhysics();
        }

        public void OnUnload()
        {
            IsLoaded = false;

            foreach (GameObject item in _gameObjectsList)
            {
                item.DestroyComponents();
            }

            CleanupPhysics();
        }

        private void ReinitializePhysics()
        {
            var physicsObjects = FindGameObjectsWithComponent<Rigidbody>();

            foreach (var go in physicsObjects)
            {
                var rigidbody = go.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.ForceReinitialize();
                }
            }
        }

        private void CleanupPhysics()
        {
            var physicsObjects = FindGameObjectsWithComponent<Rigidbody>();

            foreach (var go in physicsObjects)
            {
                var rigidbody = go.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.CleanupPhysics();
                }
            }
        }

        internal void Start()
        {
            foreach (var go in _gameObjectsList)
            {
                go.StartComponents();
            }
        }

        internal void Update(float deltaTime)
        {
            foreach (var go in _gameObjectsList)
            {
                go.UpdateComponents(deltaTime);
            }

            UpdatePhysics(deltaTime);

            ProcessDestructions();
        }

        internal void Render()
        {
            foreach (var go in _gameObjectsList)
            {
                go.RenderComponents();
            }
        }

        private void UpdatePhysics(float deltaTime)
        {
            if (_physicsWorld != null)
            {
                _physicsWorld.Update(deltaTime);
                SyncPhysicsToGameObjects();
            }
        }

        private void SyncPhysicsToGameObjects()
        {
            var physicsObjects = FindGameObjectsWithComponent<Rigidbody>();

            foreach (var go in physicsObjects)
            {
                var rigidbody = go.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    rigidbody.SyncFromPhysics();
                }
            }
        }

        private void ProcessDestructions()
        {
            if (_toDestroy.Count > 0)
            {
                foreach (var go in _toDestroy)
                {
                    go.OnComponentAdded -= NotifyComponentAdded;
                    go.OnComponentRemoved -= NotifyComponentRemoved;

                    go.DestroyComponents();
                    _gameObjects.Remove(go.Id);
                    _gameObjectsList.Remove(go);

                    OnGameObjectRemoved?.Invoke(go);
                }

                _toDestroy.Clear();

                OnSceneChanged?.Invoke();
            }
        }

        internal void NotifyComponentAdded()
        {
            OnComponentAdded?.Invoke();
            OnSceneChanged?.Invoke();
        }

        internal void NotifyComponentRemoved()
        {
            OnComponentRemoved?.Invoke();
            OnSceneChanged?.Invoke();
        }

        public void Clear()
        {
            CleanupPhysics();
            _physicsWorld?.ClearAllBodies();

            foreach (var go in _gameObjectsList)
            {
                go.OnComponentAdded -= NotifyComponentAdded;
                go.OnComponentRemoved -= NotifyComponentRemoved;
                go.DestroyComponents();
            }

            _gameObjects.Clear();
            _gameObjectsList.Clear();
            _toDestroy.Clear();

            OnSceneCleared?.Invoke();
            OnSceneChanged?.Invoke();
        }

        public void Dispose()
        {
            Clear();
            _physicsWorld?.Dispose();
            _physicsWorld = null;
        }
    }
}