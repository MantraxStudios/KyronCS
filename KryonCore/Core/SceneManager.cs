using System.Collections.Generic;
using System.IO;

namespace KrayonCore
{
    public static class SceneManager
    {
        private static Dictionary<string, GameScene> _scenes = new Dictionary<string, GameScene>();
        private static GameScene _activeScene;

        public static GameScene ActiveScene
        {
            get => _activeScene;
            private set => _activeScene = value;
        }

        public static GameScene CreateScene(string name)
        {
            if (_scenes.ContainsKey(name))
            {
                return _scenes[name];
            }

            GameScene scene = new GameScene(name);
            _scenes[name] = scene;

            return scene;
        }

        public static void LoadScene(string name)
        {
            if (!_scenes.ContainsKey(name))
            {
                return;
            }

            GraphicsEngine.Instance.GetSceneRenderer().Shutdown();

            if (_activeScene != null && _activeScene.IsLoaded)
            {
                _activeScene.OnUnload();
            }

            _activeScene = _scenes[name];
            _activeScene.OnLoad();
            _activeScene.Start();
        }

        public static GameScene GetScene(string name)
        {
            _scenes.TryGetValue(name, out GameScene scene);
            return scene;
        }

        public static void UnloadScene(string name)
        {
            if (_scenes.TryGetValue(name, out GameScene scene))
            {
                if (scene == _activeScene)
                {
                    _activeScene = null;
                }

                scene.OnUnload();
                scene.Clear();
                _scenes.Remove(name);
            }
        }

        public static void Update(float deltaTime)
        {
            _activeScene?.Update(deltaTime);
        }

        public static IEnumerable<GameScene> GetAllScenes()
        {
            return _scenes.Values;
        }

        public static int SceneCount => _scenes.Count;

        #region Save/Load Methods

        /// <summary>
        /// Guarda la escena activa en un archivo
        /// </summary>
        /// <param name="filePath">Ruta del archivo donde guardar la escena</param>
        public static void SaveActiveScene(string filePath)
        {
            if (_activeScene == null)
            {
                System.Console.WriteLine("Error: No hay una escena activa para guardar");
                return;
            }

            SceneSaveSystem.SaveScene(_activeScene, filePath);
        }

        /// <summary>
        /// Guarda una escena específica en un archivo
        /// </summary>
        /// <param name="sceneName">Nombre de la escena a guardar</param>
        /// <param name="filePath">Ruta del archivo donde guardar la escena</param>
        public static void SaveScene(string sceneName, string filePath)
        {
            if (!_scenes.TryGetValue(sceneName, out GameScene scene))
            {
                System.Console.WriteLine($"Error: No se encontró la escena '{sceneName}'");
                return;
            }

            SceneSaveSystem.SaveScene(scene, filePath);
        }

        /// <summary>
        /// Carga una escena desde un archivo y la añade al SceneManager
        /// </summary>
        /// <param name="filePath">Ruta del archivo de escena</param>
        /// <param name="setAsActive">Si true, establece la escena como activa después de cargarla</param>
        /// <returns>La escena cargada</returns>
        public static GameScene LoadSceneFromFile(string filePath, bool setAsActive = false)
        {
            if (!File.Exists(filePath))
            {
                System.Console.WriteLine($"Error: No se encontró el archivo '{filePath}'");
                return null;
            }

            var scene = SceneSaveSystem.LoadScene(filePath);

            // Si ya existe una escena con ese nombre, reemplazarla
            if (_scenes.ContainsKey(scene.Name))
            {
                UnloadScene(scene.Name);
            }

            _scenes[scene.Name] = scene;

            if (setAsActive)
            {
                LoadScene(scene.Name);
            }

            return scene;
        }

        /// <summary>
        /// Guarda todas las escenas cargadas en un directorio
        /// </summary>
        /// <param name="directoryPath">Directorio donde guardar las escenas</param>
        public static void SaveAllScenes(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            foreach (var scene in _scenes.Values)
            {
                string filePath = Path.Combine(directoryPath, $"{scene.Name}.scene");
                SceneSaveSystem.SaveScene(scene, filePath);
            }

            System.Console.WriteLine($"Se guardaron {_scenes.Count} escenas en '{directoryPath}'");
        }

        /// <summary>
        /// Carga todas las escenas desde un directorio
        /// </summary>
        /// <param name="directoryPath">Directorio desde donde cargar las escenas</param>
        public static void LoadAllScenesFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                System.Console.WriteLine($"Error: No se encontró el directorio '{directoryPath}'");
                return;
            }

            var sceneFiles = Directory.GetFiles(directoryPath, "*.scene");

            foreach (var filePath in sceneFiles)
            {
                try
                {
                    LoadSceneFromFile(filePath, false);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"Error al cargar escena desde '{filePath}': {ex.Message}");
                }
            }

            System.Console.WriteLine($"Se cargaron {sceneFiles.Length} escenas desde '{directoryPath}'");
        }

        #endregion
    }
}