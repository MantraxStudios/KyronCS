using System;
using System.Collections.Generic;
using System.Linq;

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

        public GameScene(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
            _gameObjects = new Dictionary<Guid, GameObject>();
            _gameObjectsList = new List<GameObject>();
            _toDestroy = new List<GameObject>();
            IsLoaded = false;
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
            foreach (var go in _gameObjectsList)
            {
                go.UpdateComponents(deltaTime);
            }

            ProcessDestructions();
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
        }
    }
}