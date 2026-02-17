using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using KrayonCore.Components.RenderComponents;
using OpenTK.Mathematics;

namespace KrayonCore
{
    /// <summary>
    /// Extensión del ComponentSerializer para manejar casos especiales
    /// </summary>
    public static class ComponentSerializerExtensions
    {
        /// <summary>
        /// Antes de guardar una escena, prepara los MeshRenderers para serialización
        /// guardando los nombres de los materiales
        /// </summary>
        public static void PrepareForSerialization(this MeshRenderer meshRenderer)
        {
            if (meshRenderer == null)
                return;
            var materials = meshRenderer.Materials;
            if (materials == null || materials.Length == 0)
            {
                meshRenderer.MaterialPaths = new string[0];
                return;
            }
            var paths = new List<string>();
            foreach (var material in materials)
            {
                if (material != null && !string.IsNullOrEmpty(material.Name))
                {
                    paths.Add(material.Name);
                }
                else
                {
                    paths.Add("");
                }
            }
            meshRenderer.MaterialPaths = paths.ToArray();
            Console.WriteLine($"[ComponentSerializer] MeshRenderer en {meshRenderer.GameObject?.Name}: Guardados {paths.Count} nombres de materiales");
            for (int i = 0; i < paths.Count; i++)
            {
                Console.WriteLine($"[ComponentSerializer]   Material {i}: {paths[i]}");
            }
        }

        /// <summary>
        /// Antes de guardar una escena, prepara los TileRenderers para serialización
        /// guardando los nombres de los materiales
        /// </summary>
        public static void PrepareForSerialization(this TileRenderer tileRenderer)
        {
            if (tileRenderer == null)
                return;
            var materials = tileRenderer.Materials;
            if (materials == null || materials.Length == 0)
            {
                tileRenderer.MaterialPaths = new string[0];
                return;
            }
            var paths = new List<string>();
            foreach (var material in materials)
            {
                if (material != null && !string.IsNullOrEmpty(material.Name))
                {
                    paths.Add(material.Name);
                }
                else
                {
                    paths.Add("");
                }
            }
            tileRenderer.MaterialPaths = paths.ToArray();
            Console.WriteLine($"[ComponentSerializer] TileRenderer en {tileRenderer.GameObject?.Name}: Guardados {paths.Count} nombres de materiales");
            for (int i = 0; i < paths.Count; i++)
            {
                Console.WriteLine($"[ComponentSerializer]   Material {i}: {paths[i]}");
            }
        }

        /// <summary>
        /// Prepara todos los MeshRenderers y TileRenderers de una escena antes de guardar
        /// </summary>
        public static void PrepareSceneForSerialization(GameScene scene)
        {
            if (scene == null)
                return;
            Console.WriteLine($"[ComponentSerializer] Preparando escena '{scene.Name}' para serialización...");
            int meshRendererCount = 0;
            int tileRendererCount = 0;
            foreach (var gameObject in scene.GetAllGameObjects())
            {
                var meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.PrepareForSerialization();
                    meshRendererCount++;
                }

                var tileRenderer = gameObject.GetComponent<TileRenderer>();
                if (tileRenderer != null)
                {
                    tileRenderer.PrepareForSerialization();
                    tileRendererCount++;
                }
            }
            Console.WriteLine($"[ComponentSerializer] {meshRendererCount} MeshRenderers y {tileRendererCount} TileRenderers preparados para serialización");
        }
    }
}