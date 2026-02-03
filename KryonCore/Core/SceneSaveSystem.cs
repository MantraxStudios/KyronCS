using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KrayonCore
{
    public static class SceneSaveSystem
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        /// <summary>
        /// Guarda una escena en un archivo JSON
        /// </summary>
        public static void SaveScene(GameScene scene, string filePath)
        {
            if (scene == null)
                throw new ArgumentNullException(nameof(scene));

            // NUEVO: Preparar la escena antes de serializar
            ComponentSerializerExtensions.PrepareSceneForSerialization(scene);

            var sceneData = SerializeScene(scene);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(sceneData, JsonOptions);
            File.WriteAllText(filePath, json);

            Console.WriteLine($"Escena '{scene.Name}' guardada en: {filePath}");
        }

        /// <summary>
        /// Carga una escena desde un archivo JSON
        /// </summary>
        public static GameScene LoadScene(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"No se encontró el archivo: {filePath}");

            var json = File.ReadAllText(filePath);
            var sceneData = JsonSerializer.Deserialize<SceneData>(json, JsonOptions);

            if (sceneData == null)
                throw new Exception("Error al deserializar los datos de la escena");

            var scene = DeserializeScene(sceneData);

            Console.WriteLine($"Escena '{scene.Name}' cargada desde: {filePath}");

            return scene;
        }

        /// <summary>
        /// Serializa una escena completa a SceneData
        /// </summary>
        private static SceneData SerializeScene(GameScene scene)
        {
            var sceneData = new SceneData
            {
                SceneName = scene.Name
            };

            foreach (var gameObject in scene.GetAllGameObjects())
            {
                var goData = SerializeGameObject(gameObject);
                sceneData.GameObjects.Add(goData);
            }

            return sceneData;
        }

        /// <summary>
        /// Serializa un GameObject completo
        /// </summary>
        private static GameObjectData SerializeGameObject(GameObject gameObject)
        {
            var goData = new GameObjectData
            {
                Id = gameObject.Id,
                Name = gameObject.Name,
                Tag = gameObject.Tag,
                Active = gameObject.Active
            };

            foreach (var component in gameObject.GetAllComponents())
            {
                var componentData = ComponentSerializer.Serialize(component);
                goData.Components.Add(componentData);
            }

            return goData;
        }

        /// <summary>
        /// Deserializa SceneData a una GameScene
        /// </summary>
        private static GameScene DeserializeScene(SceneData sceneData)
        {
            var scene = new GameScene(sceneData.SceneName);

            // Primero crear todos los GameObjects sin componentes
            var gameObjectMap = new System.Collections.Generic.Dictionary<Guid, GameObject>();

            foreach (var goData in sceneData.GameObjects)
            {
                // Crear GameObject básico (esto crea un Transform por defecto)
                var gameObject = new GameObject(goData.Name)
                {
                    Tag = goData.Tag,
                    Active = goData.Active
                };

                scene.AddGameObject(gameObject);
                gameObjectMap[goData.Id] = gameObject;
            }

            // Luego deserializar los componentes
            for (int i = 0; i < sceneData.GameObjects.Count; i++)
            {
                var goData = sceneData.GameObjects[i];
                var gameObject = gameObjectMap[goData.Id];

                DeserializeComponents(gameObject, goData.Components);
            }

            return scene;
        }

        /// <summary>
        /// Deserializa componentes en un GameObject
        /// </summary>
        private static void DeserializeComponents(GameObject gameObject, System.Collections.Generic.List<ComponentData> componentsData)
        {
            foreach (var componentData in componentsData)
            {
                try
                {
                    var componentType = Type.GetType(componentData.TypeName);

                    if (componentType == null)
                    {
                        Console.WriteLine($"Advertencia: No se encontró el tipo de componente {componentData.TypeName}");
                        continue;
                    }

                    // Si el componente ya existe (como Transform), obtenerlo
                    Component component = gameObject.GetAllComponents()
                        .FirstOrDefault(c => c.GetType() == componentType);

                    bool isNewComponent = false;

                    // Si no existe, crearlo SIN llamar a Awake todavía
                    if (component == null)
                    {
                        component = gameObject.AddComponentWithoutAwake(componentType);
                        isNewComponent = true;
                    }

                    // Deserializar los datos en el componente
                    ComponentSerializer.Deserialize(component, componentData);

                    // CORRECCIÓN: Llamar a Awake() después de deserializar
                    // Solo para componentes nuevos (Transform ya tiene Awake llamado)
                    if (isNewComponent)
                    {
                        try
                        {
                            component.Awake();
                            Console.WriteLine($"[SceneSaveSystem] Awake llamado en {componentType.Name} de {gameObject.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SceneSaveSystem] Error en Awake del componente {componentData.TypeName}: {ex.Message}");
                            Console.WriteLine($"Stack: {ex.StackTrace}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneSaveSystem] Error al deserializar componente {componentData.TypeName}: {ex.Message}");
                    Console.WriteLine($"Stack: {ex.StackTrace}");
                }
            }
        }
    }
}