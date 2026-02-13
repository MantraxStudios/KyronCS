using KrayonCore;
using KrayonCore.GraphicsData;
using System;

namespace KrayonEditor.UI
{
    /// <summary>
    /// Helper para reconstruir las referencias de materiales después de cargar una escena
    /// </summary>
    public static class SceneLoadHelper
    {
        /// <summary>
        /// Reasigna todos los materiales a los MeshRenderers de una escena cargada
        /// Esto debe llamarse después de LoadScene
        /// </summary>
        public static void ReassignMaterials(GameScene scene)
        {
            if (scene == null)
            {
                Console.WriteLine("[SceneLoadHelper] Error: scene es null");
                return;
            }

            var renderer = GraphicsEngine.Instance?.GetSceneRenderer();
            if (renderer == null)
            {
                Console.WriteLine("[SceneLoadHelper] Error: SceneRenderer no disponible");
                return;
            }

            Console.WriteLine($"[SceneLoadHelper] Reasignando materiales para escena: {scene.Name}");

            int totalMeshRenderers = 0;
            int materialsAssigned = 0;
            int materialsMissing = 0;

            foreach (var gameObject in scene.GetAllGameObjects())
            {
                var meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    continue;

                totalMeshRenderers++;

                // Si tiene rutas de materiales guardadas, intentar asignarlos
                if (meshRenderer.MaterialPaths != null && meshRenderer.MaterialPaths.Length > 0)
                {
                    Console.WriteLine($"[SceneLoadHelper] {gameObject.Name}: {meshRenderer.MaterialPaths.Length} materiales guardados");

                    for (int i = 0; i < meshRenderer.MaterialPaths.Length; i++)
                    {
                        string materialPath = meshRenderer.MaterialPaths[i];

                        if (string.IsNullOrEmpty(materialPath))
                        {
                            Console.WriteLine($"[SceneLoadHelper]   Material {i}: ruta vacía");
                            continue;
                        }

                        if (GraphicsEngine.Instance.Materials.TryGet(materialPath, out var material))
                        {
                            meshRenderer.SetMaterial(i, material);
                            materialsAssigned++;
                            Console.WriteLine($"[SceneLoadHelper]   ✓ Material {i} asignado: {materialPath}");
                        }
                        else
                        {
                            materialsMissing++;
                            Console.WriteLine($"[SceneLoadHelper]   ✗ Material {i} no encontrado: {materialPath}");
                            Console.WriteLine($"[SceneLoadHelper]     Asegúrate de que el material '{materialPath}' exista en el MaterialManager");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[SceneLoadHelper] {gameObject.Name}: Sin rutas de materiales guardadas");
                }
            }

            Console.WriteLine($"[SceneLoadHelper] Resumen:");
            Console.WriteLine($"[SceneLoadHelper]   MeshRenderers procesados: {totalMeshRenderers}");
            Console.WriteLine($"[SceneLoadHelper]   Materiales asignados: {materialsAssigned}");
            Console.WriteLine($"[SceneLoadHelper]   Materiales faltantes: {materialsMissing}");
        }

        /// <summary>
        /// Verifica que todos los modelos se hayan cargado correctamente
        /// </summary>
        public static void VerifyModels(GameScene scene)
        {
            if (scene == null)
                return;

            Console.WriteLine($"[SceneLoadHelper] Verificando modelos para escena: {scene.Name}");

            int totalMeshRenderers = 0;
            int modelsLoaded = 0;
            int modelsMissing = 0;

            foreach (var gameObject in scene.GetAllGameObjects())
            {
                var meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    continue;

                totalMeshRenderers++;

                if (meshRenderer.Model != null)
                {
                    modelsLoaded++;
                    Console.WriteLine($"[SceneLoadHelper] ✓ {gameObject.Name}: Modelo cargado ({meshRenderer.ModelPath})");
                }
                else if (!string.IsNullOrEmpty(meshRenderer.ModelPath))
                {
                    modelsMissing++;
                    Console.WriteLine($"[SceneLoadHelper] ✗ {gameObject.Name}: Modelo NO cargado ({meshRenderer.ModelPath})");
                }
                else
                {
                    Console.WriteLine($"[SceneLoadHelper] ⚠ {gameObject.Name}: Sin ModelPath especificado");
                }
            }

            Console.WriteLine($"[SceneLoadHelper] Resumen de modelos:");
            Console.WriteLine($"[SceneLoadHelper]   MeshRenderers: {totalMeshRenderers}");
            Console.WriteLine($"[SceneLoadHelper]   Modelos cargados: {modelsLoaded}");
            Console.WriteLine($"[SceneLoadHelper]   Modelos faltantes: {modelsMissing}");
        }

        /// <summary>
        /// Proceso completo de carga y configuración de escena
        /// </summary>
        public static GameScene LoadSceneComplete(string filePath, bool setAsActive = true)
        {
            Console.WriteLine($"[SceneLoadHelper] ========================================");
            Console.WriteLine($"[SceneLoadHelper] Cargando escena completa: {filePath}");
            Console.WriteLine($"[SceneLoadHelper] ========================================");

            // 1. Cargar la escena
            if (setAsActive)
            {
                SceneManager.LoadScene(filePath);
            }

            var scene = SceneManager.ActiveScene;

            if (scene == null)
            {
                Console.WriteLine($"[SceneLoadHelper] Error: No se pudo cargar la escena");
                return null;
            }

            // 2. Verificar modelos
            VerifyModels(scene);

            // 3. Reasignar materiales
            ReassignMaterials(scene);

            Console.WriteLine($"[SceneLoadHelper] ========================================");
            Console.WriteLine($"[SceneLoadHelper] Escena cargada completamente: {scene.Name}");
            Console.WriteLine($"[SceneLoadHelper] ========================================");

            return scene;
        }
    }
}