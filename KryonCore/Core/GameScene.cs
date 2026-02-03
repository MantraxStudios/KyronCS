using System;
using System.Collections.Generic;
using System.Linq;
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

        // Sistema de física
        private WorldPhysic _physicsWorld;
        public WorldPhysic PhysicsWorld => _physicsWorld;

        public GameScene(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
            _gameObjects = new Dictionary<Guid, GameObject>();
            _gameObjectsList = new List<GameObject>();
            _toDestroy = new List<GameObject>();
            IsLoaded = false;

            // Inicializar el mundo de física
            _physicsWorld = new WorldPhysic();
        }

        public GameObject CreateGameObject(string name = "GameObject")
        {
            GameObject go = new GameObject(name);
            AddGameObject(go);
            return go;
        }

        internal void AddGameObject(GameObject gameObject)
        {
            if (!_gameObjects.ContainsKey(gameObject.Id))
            {
                gameObject.Scene = this;
                _gameObjects[gameObject.Id] = gameObject;
                _gameObjectsList.Add(gameObject);
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
            if (gameObject != null && _gameObjects.ContainsKey(gameObject.Id))
            {
                _toDestroy.Add(gameObject);
            }
        }

        public IReadOnlyList<GameObject> GetAllGameObjects()
        {
            return _gameObjectsList.AsReadOnly();
        }

        public void OnLoad()
        {
            IsLoaded = true;
        }

        public void OnUnload()
        {
            IsLoaded = false;
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
            // Actualizar componentes
            foreach (var go in _gameObjectsList)
            {
                go.UpdateComponents(deltaTime);
            }

            // Actualizar física
            UpdatePhysics(deltaTime);

            // Procesar destrucciones
            ProcessDestructions();
        }

        /// <summary>
        /// Actualiza la simulación de física
        /// </summary>
        private void UpdatePhysics(float deltaTime)
        {
            if (_physicsWorld != null)
            {
                // Actualizar la simulación de física
                _physicsWorld.Update(deltaTime);

                // Sincronizar GameObjects con sus cuerpos físicos
                SyncPhysicsToGameObjects();
            }
        }

        /// <summary>
        /// Sincroniza las posiciones de los cuerpos físicos con los GameObjects
        /// </summary>
        private void SyncPhysicsToGameObjects()
        {
            // Recorrer todos los GameObjects que tienen componentes de física
            var physicsObjects = FindGameObjectsWithComponent<Rigidbody>();

            foreach (var go in physicsObjects)
            {
                var rigidbody = go.GetComponent<Rigidbody>();
                if (rigidbody != null && rigidbody.Body != null)
                {
                    // Sincronizar posición y rotación desde el motor de física
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
                    go.DestroyComponents();
                    _gameObjects.Remove(go.Id);
                    _gameObjectsList.Remove(go);
                }
                _toDestroy.Clear();
            }
        }

        public void Clear()
        {
            foreach (var go in _gameObjectsList)
            {
                go.DestroyComponents();
            }

            _gameObjects.Clear();
            _gameObjectsList.Clear();
            _toDestroy.Clear();

            // Limpiar el mundo de física
            _physicsWorld?.Dispose();
            _physicsWorld = new WorldPhysic();
        }

        /// <summary>
        /// Libera los recursos de la escena
        /// </summary>
        public void Dispose()
        {
            Clear();
            _physicsWorld?.Dispose();
            _physicsWorld = null;
        }
    }
}