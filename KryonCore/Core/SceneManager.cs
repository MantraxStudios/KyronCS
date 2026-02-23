using Acornima.Ast;
using Assimp;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.GraphicsData;
using System.Collections.Generic;
using System.IO;

namespace KrayonCore
{
    public static class SceneManager
    {
        private static Dictionary<string, GameScene> _scenes = new Dictionary<string, GameScene>();
        private static List<GameScene> _PrimaryScenes = new List<GameScene>();

        public static event Action<GameScene, string> OnSceneSaved;
        public static event Action<GameScene> OnSceneLoaded;
        public static event Action<GameScene> OnSceneUnloaded;

        public static IReadOnlyList<GameScene> PrimaryScenes => _PrimaryScenes;

        public static GameScene PrimaryScene => _PrimaryScenes.Count > 0 ? _PrimaryScenes[0] : null;
        public static byte[] CurrentSceneData;

        public static GameScene CreateScene(string name)
        {
            if (_scenes.ContainsKey(name))
                return _scenes[name];

            GameScene scene = new GameScene(name);
            _scenes[name] = scene;
            return scene;
        }

        public static void LoadScene(string nameOrPath, bool additive = false)
        {
            GameScene sceneToLoad = null;
            bool isFile = nameOrPath.EndsWith(".scene", StringComparison.OrdinalIgnoreCase);

            if (isFile)
            {
                if (!additive)
                    UnloadAllPrimaryScenes();

                GraphicsEngine.Instance.GetSceneRenderer().ClearAllRenderers();

                if (AppInfo.IsCompiledGame)
                {
                    string key = $"Scene.{Path.GetFileNameWithoutExtension(nameOrPath)}";
                    byte[] bytes = AssetManager.GetBytes(key);
                    if (bytes == null)
                    {
                        Console.WriteLine($"Error: No se pudo cargar la escena '{nameOrPath}' desde Pak");
                        return;
                    }
                    sceneToLoad = SceneSaveSystem.LoadScene(bytes);
                }
                else
                {
                    if (!File.Exists(nameOrPath))
                    {
                        Console.WriteLine($"Error: No se encontró el archivo '{nameOrPath}'");
                        return;
                    }
                    sceneToLoad = SceneSaveSystem.LoadScene(nameOrPath);
                }

                if (sceneToLoad == null) return;

                if (_scenes.ContainsKey(sceneToLoad.Name))
                {
                    var oldScene = _scenes[sceneToLoad.Name];
                    if (!_PrimaryScenes.Contains(oldScene))
                    {
                        oldScene.OnUnload();
                        oldScene.Dispose();
                        OnSceneUnloaded?.Invoke(oldScene);
                    }
                }

                _scenes[sceneToLoad.Name] = sceneToLoad;
            }
            else
            {
                if (!_scenes.ContainsKey(nameOrPath))
                {
                    Console.WriteLine($"Error: No se encontró la escena '{nameOrPath}'");
                    return;
                }

                sceneToLoad = _scenes[nameOrPath];

                if (!additive)
                {
                    UnloadAllPrimaryScenes();
                    GraphicsEngine.Instance.GetSceneRenderer().ClearAllRenderers();
                }
            }

            if (!_PrimaryScenes.Contains(sceneToLoad))
            {
                _PrimaryScenes.Add(sceneToLoad);
                sceneToLoad.OnLoad();
                sceneToLoad.Start();
                OnSceneLoaded?.Invoke(sceneToLoad);
            }

            foreach (var go in sceneToLoad.GetAllGameObjects())
            {
                var csl = go.GetComponent<CSharpLogic>();
                if (csl != null)
                    csl.ResolveGameObjectReferences();
            }
        }

        public static void LoadSceneFromBytes(Byte[] scene_bytes)
        {
            UnloadAllPrimaryScenes();

            GraphicsEngine.Instance.GetSceneRenderer().ClearAllRenderers();

            GameScene sceneToLoad = SceneSaveSystem.LoadScene(scene_bytes);

            if (sceneToLoad == null) return;

            if (_scenes.ContainsKey(sceneToLoad.Name))
            {
                var oldScene = _scenes[sceneToLoad.Name];
                if (!_PrimaryScenes.Contains(oldScene))
                {
                    oldScene.OnUnload();
                    oldScene.Dispose();
                    OnSceneUnloaded?.Invoke(oldScene);
                }
            }

            _scenes[sceneToLoad.Name] = sceneToLoad;

            if (!_PrimaryScenes.Contains(sceneToLoad))
            {
                _PrimaryScenes.Add(sceneToLoad);
                sceneToLoad.OnLoad();
                sceneToLoad.Start();
                OnSceneLoaded?.Invoke(sceneToLoad);
            }

            foreach (var go in sceneToLoad.GetAllGameObjects())
            {
                var csl = go.GetComponent<CSharpLogic>();
                if (csl != null)
                    csl.ResolveGameObjectReferences();
            }
        }


        public static void UnloadScene(string name)
        {
            if (_scenes.TryGetValue(name, out GameScene scene))
            {
                _PrimaryScenes.Remove(scene);
                scene.OnUnload();
                scene.Dispose();
                _scenes.Remove(name);
                OnSceneUnloaded?.Invoke(scene);
            }
        }

        public static void UnloadPrimaryScene(string name)
        {
            if (_scenes.TryGetValue(name, out GameScene scene) && _PrimaryScenes.Contains(scene))
            {
                _PrimaryScenes.Remove(scene);
                scene.OnUnload();
                OnSceneUnloaded?.Invoke(scene);
            }
        }

        private static void UnloadAllPrimaryScenes()
        {
            foreach (var scene in _PrimaryScenes)
            {
                scene.OnUnload();
                OnSceneUnloaded?.Invoke(scene);
            }

            _PrimaryScenes.Clear();
        }

        public static bool IsSceneActive(string name)
        {
            return _scenes.TryGetValue(name, out GameScene scene) && _PrimaryScenes.Contains(scene);
        }

        public static GameScene GetScene(string name)
        {
            _scenes.TryGetValue(name, out GameScene scene);
            return scene;
        }

        public static void Update(float deltaTime)
        {
            if (!AppInfo.IsPlayingGame) return;

            foreach (var scene in _PrimaryScenes)
                scene.Update(deltaTime);
        }

        public static void Render()
        {
            foreach (var scene in _PrimaryScenes)
                scene.Render();
        }

        public static IEnumerable<GameScene> GetAllScenes() => _scenes.Values;

        public static int SceneCount => _scenes.Count;

        public static int PrimarySceneCount => _PrimaryScenes.Count;

        #region GameObject Cross-Scene Utilities

        public static bool ContainsGameObject(GameScene scene, GameObject go)
        {
            foreach (var obj in scene.GetAllGameObjects())
                if (obj == go) return true;
            return false;
        }

        public static void MoveGameObjectToScene(GameObject go, GameScene targetScene)
        {
            if (go == null || targetScene == null) return;

            GameScene sourceScene = null;
            foreach (var scene in _PrimaryScenes)
            {
                if (scene == targetScene) continue;
                if (ContainsGameObject(scene, go))
                {
                    sourceScene = scene;
                    break;
                }
            }

            if (sourceScene == null)
            {
                Console.WriteLine($"Warning: No se encontró la escena origen de '{go.Name}'");
                return;
            }

            if (sourceScene == targetScene) return;

            go.Transform.SetParent(null);

            sourceScene.RemoveGameObject(go);
            targetScene.AddGameObject(go);

            Console.WriteLine($"'{go.Name}' moved from '{sourceScene.Name}' to '{targetScene.Name}'");
        }

        public static GameObject FindGameObjectById(Guid id)
        {
            if (PrimaryScene != null)
            {
                foreach (var obj in PrimaryScene.GetAllGameObjects())
                {
                    if (obj.Id == id) return obj;
                }
            }
            return null;
        }

        public static GameScene GetOwnerScene(GameObject go)
        {
            foreach (var scene in _PrimaryScenes)
            {
                if (ContainsGameObject(scene, go)) return scene;
            }
            return null;
        }

        #endregion

        #region Save/Load Methods

        public static void SavePrimaryScene(string filePath)
        {
            if (PrimaryScene == null)
            {
                Console.WriteLine("Error: No hay una escena activa para guardar");
                return;
            }

            SceneSaveSystem.SaveScene(PrimaryScene, filePath);
            OnSceneSaved?.Invoke(PrimaryScene, filePath);
        }

        public static void SaveScene(string sceneName, string filePath)
        {
            if (!_scenes.TryGetValue(sceneName, out GameScene scene))
            {
                Console.WriteLine($"Error: No se encontró la escena '{sceneName}'");
                return;
            }

            SceneSaveSystem.SaveScene(scene, filePath);
            OnSceneSaved?.Invoke(scene, filePath);
        }

        public static byte[] CloneSceneToBytes(GameScene original)
        {
            if (original == null)
                return null;

            return SceneSaveSystem.SaveSceneToBytes(original);
        }

        public static void SaveAllScenes(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            foreach (var scene in _scenes.Values)
            {
                string filePath = Path.Combine(directoryPath, $"{scene.Name}.scene");
                SceneSaveSystem.SaveScene(scene, filePath);
                OnSceneSaved?.Invoke(scene, filePath);
            }

            Console.WriteLine($"Se guardaron {_scenes.Count} escenas en '{directoryPath}'");
        }

        public static void LoadAllScenesFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Error: No se encontró el directorio '{directoryPath}'");
                return;
            }

            var sceneFiles = Directory.GetFiles(directoryPath, "*.scene");

            foreach (var filePath in sceneFiles)
            {
                try
                {
                    var scene = SceneSaveSystem.LoadScene(filePath);

                    if (_scenes.ContainsKey(scene.Name))
                        _scenes[scene.Name].Clear();

                    _scenes[scene.Name] = scene;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al cargar escena desde '{filePath}': {ex.Message}");
                }
            }

            Console.WriteLine($"Se cargaron {sceneFiles.Length} escenas desde '{directoryPath}'");
        }

        #endregion
    }
}