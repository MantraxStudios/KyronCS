using Assimp;
using KrayonCore;
using KrayonCore.Graphics;
using LightingSystem;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore
{
    public class SceneRenderer
    {
        private Camera _camera;
        private LightManager _lightManager;

        // Cache para instanced rendering de MeshRenderers
        private Dictionary<(Model model, Material material), List<Matrix4>> _meshInstanceGroups = new Dictionary<(Model, Material), List<Matrix4>>();

        // Cache para instanced rendering de SpriteRenderers
        private Dictionary<(Model model, Material material), List<Matrix4>> _spriteInstanceGroups = new Dictionary<(Model, Material), List<Matrix4>>();

        // Modo de renderizado
        public bool WireframeMode { get; set; } = false;

        public void Initialize()
        {
            _camera = new Camera(new Vector3(0, 0, 5), WindowConfig.Width / (float)WindowConfig.Height);
            _lightManager = new LightManager();
        }

        public void Render()
        {
            // Configurar modo de polígono según wireframe
            if (WireframeMode)
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            }
            else
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            }

            Matrix4 view = _camera.GetViewMatrix();
            Matrix4 projection = _camera.GetProjectionMatrix();
            Vector3 cameraPos = _camera.Position;

            // Renderizar MeshRenderers usando instanced rendering
            RenderMeshRenderers(view, projection, cameraPos);

            // Renderizar SpriteRenderers usando instanced rendering
            RenderSpriteRenderers(view, projection, cameraPos);

            // Renderizar TileRenderers
            RenderTileRenderers(view, projection, cameraPos);

            // Restaurar modo fill por defecto
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void RenderMeshRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var meshRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<MeshRenderer>();
            if (meshRenderers == null)
                return;

            // Limpiar grupos de instancias
            _meshInstanceGroups.Clear();

            // PASO 1: Agrupar todos los MeshRenderers por modelo y material
            foreach (var go in meshRenderers)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.Enabled)
                    continue;

                // Asignar material básico si no tiene
                if (renderer.MaterialCount == 0)
                {
                    var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basicMaterial != null)
                    {
                        basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                        renderer.AddMaterial(basicMaterial);
                    }
                }

                // Verificar que tenga modelo y materiales
                if (renderer.Model == null || renderer.MaterialCount == 0)
                    continue;

                var transform = go.GetComponent<Transform>();
                if (transform == null)
                    continue;

                Matrix4 worldMatrix = transform.GetWorldMatrix();

                // Si tiene un solo material, usar ese
                if (renderer.MaterialCount == 1)
                {
                    var material = renderer.Materials[0];
                    if (material != null)
                    {
                        var key = (renderer.Model, material);

                        if (!_meshInstanceGroups.ContainsKey(key))
                        {
                            _meshInstanceGroups[key] = new List<Matrix4>();
                        }

                        _meshInstanceGroups[key].Add(worldMatrix);
                    }
                }
                else
                {
                    // Si tiene múltiples materiales, solo usar el primero para instancing
                    var material = renderer.Materials[0];
                    if (material != null)
                    {
                        var key = (renderer.Model, material);

                        if (!_meshInstanceGroups.ContainsKey(key))
                        {
                            _meshInstanceGroups[key] = new List<Matrix4>();
                        }

                        _meshInstanceGroups[key].Add(worldMatrix);
                    }
                }
            }

            // PASO 2: Renderizar cada grupo usando instanced rendering
            foreach (var kvp in _meshInstanceGroups)
            {
                var model = kvp.Key.model;
                var material = kvp.Key.material;
                var matrices = kvp.Value;

                if (matrices.Count == 0)
                    continue;

                // Setup instancing en el modelo
                model.SetupInstancing(matrices.ToArray());

                // Configurar material
                material.SetPBRProperties();
                material.Use();

                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                // Aplicar luces al shader
                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                // Dibujar todas las instancias de una vez
                model.DrawInstanced(matrices.Count);
            }
        }

        private void RenderSpriteRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var spriteRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<SpriteRenderer>();
            if (spriteRenderers == null)
                return;

            // Limpiar grupos de instancias
            _spriteInstanceGroups.Clear();

            // PASO 1: Agrupar todos los SpriteRenderers por modelo y material
            foreach (var go in spriteRenderers)
            {
                var renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null || !renderer.Enabled)
                    continue;

                // Asignar material básico si no tiene
                if (renderer.Material == null)
                {
                    var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basicMaterial != null)
                    {
                        basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                        renderer.SetMaterial(basicMaterial);
                    }
                }

                // Verificar que tenga modelo y material
                if (renderer.QuadModel == null || renderer.Material == null)
                    continue;

                var transform = go.GetComponent<Transform>();
                if (transform == null)
                    continue;

                Matrix4 worldMatrix = transform.GetWorldMatrix();

                var key = (renderer.QuadModel, renderer.Material);

                if (!_spriteInstanceGroups.ContainsKey(key))
                {
                    _spriteInstanceGroups[key] = new List<Matrix4>();
                }

                _spriteInstanceGroups[key].Add(worldMatrix);
            }

            // PASO 2: Renderizar cada grupo usando instanced rendering
            foreach (var kvp in _spriteInstanceGroups)
            {
                var model = kvp.Key.model;
                var material = kvp.Key.material;
                var matrices = kvp.Value;

                if (matrices.Count == 0)
                    continue;

                // Setup instancing en el modelo
                model.SetupInstancing(matrices.ToArray());

                // Configurar material
                material.SetPBRProperties();
                material.Use();

                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                // Aplicar luces al shader
                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                // Dibujar todas las instancias de una vez
                model.DrawInstanced(matrices.Count);
            }
        }

        private void RenderTileRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var tileRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<TileRenderer>();
            if (tileRenderers == null)
                return;

            foreach (var go in tileRenderers)
            {
                var renderer = go.GetComponent<TileRenderer>();
                if (renderer == null || !renderer.Enabled)
                    continue;

                // Asignar material básico si no tiene
                if (renderer.MaterialCount == 0)
                {
                    var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basicMaterial != null)
                    {
                        basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                        renderer.AddMaterial(basicMaterial);
                    }
                }

                if (renderer.ModelCount == 0 || renderer.MaterialCount == 0)
                    continue;

                if (renderer.TileCount == 0)
                    continue;

                // Actualizar datos de instancias
                renderer.UpdateInstanceData();

                // Renderizar cada grupo de instancias
                foreach (var kvp in renderer.InstanceGroups)
                {
                    int modelIndex = kvp.Key.modelIndex;
                    int materialIndex = kvp.Key.materialIndex;
                    int instanceCount = kvp.Value.Count;

                    if (instanceCount == 0)
                        continue;

                    var model = renderer.GetModel(modelIndex);
                    var material = renderer.GetMaterial(materialIndex);

                    if (model == null || material == null)
                        continue;

                    material.SetPBRProperties();
                    material.Use();

                    material.SetMatrix4("view", view);
                    material.SetMatrix4("projection", projection);
                    material.SetVector3("u_CameraPos", cameraPos);

                    // Aplicar luces al shader
                    _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                    model.DrawInstanced(instanceCount);
                }
            }
        }

        public void ToggleWireframe()
        {
            WireframeMode = !WireframeMode;
            Console.WriteLine($"[SceneRenderer] Wireframe mode: {(WireframeMode ? "ON" : "OFF")}");
        }

        public void SetWireframeMode(bool enabled)
        {
            WireframeMode = enabled;
        }

        public void Resize(int width, int height)
        {
            GL.Viewport(0, 0, width, height);
            _camera.UpdateAspectRatio(width, height);
        }

        public void Update(float deltaTime)
        {
            // Actualizar lógica si es necesario
        }

        public void Shutdown()
        {
        }

        public Camera GetCamera() => _camera;

        public LightManager GetLightManager() => _lightManager;
    }
}