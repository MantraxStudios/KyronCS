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

        private Dictionary<(Model model, Material material), List<Matrix4>> _meshInstanceGroups = new Dictionary<(Model, Material), List<Matrix4>>();

        private Dictionary<(Model model, Material material), List<Matrix4>> _spriteInstanceGroups = new Dictionary<(Model, Material), List<Matrix4>>();

        public bool WireframeMode { get; set; } = false;

        public void Initialize()
        {
            _camera = new Camera(new Vector3(0, 0, 5), WindowConfig.Width / (float)WindowConfig.Height);
            _lightManager = new LightManager();
        }

        public void Render()
        {
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

            RenderMeshRenderers(view, projection, cameraPos);
            RenderSpriteRenderers(view, projection, cameraPos);
            RenderTileRenderers(view, projection, cameraPos);

            ClearInstanceGroups();

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void ClearInstanceGroups()
        {
            foreach (var kvp in _meshInstanceGroups)
            {
                kvp.Key.model.ClearInstancing();
            }
            _meshInstanceGroups.Clear();

            foreach (var kvp in _spriteInstanceGroups)
            {
                kvp.Key.model.ClearInstancing();
            }
            _spriteInstanceGroups.Clear();
        }

        private void RenderMeshRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var meshRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<MeshRenderer>();
            if (meshRenderers == null)
                return;

            List<GameObject> multiMaterialObjects = new List<GameObject>();

            foreach (var go in meshRenderers)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.Enabled)
                    continue;

                if (renderer.Model == null)
                    continue;

                ValidateAndFixMaterials(renderer);

                var transform = go.GetComponent<Transform>();
                if (transform == null)
                    continue;

                Matrix4 worldMatrix = transform.GetWorldMatrix();

                int validMaterialCount = CountValidMaterials(renderer);
                
                if (validMaterialCount == 1)
                {
                    Material validMaterial = GetFirstValidMaterial(renderer);
                    if (validMaterial != null)
                    {
                        var key = (renderer.Model, validMaterial);

                        if (!_meshInstanceGroups.ContainsKey(key))
                        {
                            _meshInstanceGroups[key] = new List<Matrix4>();
                        }

                        _meshInstanceGroups[key].Add(worldMatrix);
                    }
                }
                else if (validMaterialCount > 1)
                {
                    multiMaterialObjects.Add(go);
                }
            }
            
            foreach (var kvp in _meshInstanceGroups)
            {
                var model = kvp.Key.model;
                var material = kvp.Key.material;
                var matrices = kvp.Value;

                if (matrices.Count == 0)
                    continue;

                model.SetupInstancing(matrices.ToArray());

                material.SetPBRProperties();
                material.Use();

                material.SetInt("u_UseInstancing", 1);
                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                model.DrawInstanced(matrices.Count);
                
                model.ClearInstancing();
            }
            
            foreach (var go in multiMaterialObjects)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                var transform = go.GetComponent<Transform>();
                
                Matrix4 worldMatrix = transform.GetWorldMatrix();
                
                if (renderer.Model != null)
                {
                    renderer.Model.ClearInstancing();
                    RenderMeshWithMultipleMaterials(renderer, worldMatrix, view, projection, cameraPos);
                }
            }
        }

        private void RenderMeshWithMultipleMaterials(MeshRenderer renderer, Matrix4 worldMatrix, Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (renderer.Model == null)
                return;

            int submeshCount = renderer.Model.SubMeshCount;
            Material fallbackMaterial = null;
            int vao = renderer.Model.GetVAO();
            
            GL.BindVertexArray(vao);
            
            for (int i = 0; i < submeshCount; i++)
            {
                Material material = null;
                
                if (i < renderer.MaterialCount)
                {
                    material = renderer.GetMaterial(i);
                }
                
                if (material == null && renderer.MaterialCount > 0)
                {
                    material = renderer.GetMaterial(0);
                }
                
                if (material == null)
                {
                    if (fallbackMaterial == null)
                    {
                        fallbackMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                        if (fallbackMaterial != null)
                        {
                            fallbackMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 0.0f, 1.0f));
                        }
                    }
                    
                    material = fallbackMaterial;
                }

                if (material == null)
                    continue;
                
                material.SetPBRProperties();
                material.Use();

                material.SetInt("u_UseInstancing", 0);
                material.SetMatrix4("model", worldMatrix);
                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);
                
                int baseVertex = renderer.Model.GetSubmeshBaseVertex(i);
                int indexCount = renderer.Model.GetSubmeshIndexCount(i);
                int baseIndex = renderer.Model.GetSubmeshBaseIndex(i);
                
                GL.DrawElementsBaseVertex(
                    OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles,
                    indexCount,
                    DrawElementsType.UnsignedInt,
                    (IntPtr)(baseIndex * sizeof(uint)),
                    baseVertex
                );
            }
            
            GL.BindVertexArray(0);
        }

        private void ValidateAndFixMaterials(MeshRenderer renderer)
        {
            if (renderer.Model == null)
                return;

            int submeshCount = renderer.Model.SubMeshCount;
            int materialCount = renderer.MaterialCount;

            if (materialCount == 0)
            {
                var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                if (basicMaterial != null)
                {
                    basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                    renderer.ClearMaterials();
                    renderer.AddMaterial(basicMaterial);
                }
                return;
            }

            bool hasNullGaps = false;
            int lastValidIndex = -1;
            
            for (int i = 0; i < materialCount; i++)
            {
                var mat = renderer.GetMaterial(i);
                
                if (mat != null)
                {
                    if (hasNullGaps)
                    {
                        RebuildMaterialArray(renderer);
                        return;
                    }
                    lastValidIndex = i;
                }
                else if (lastValidIndex >= 0)
                {
                    hasNullGaps = true;
                }
            }

            if (!HasAnyValidMaterial(renderer))
            {
                var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                if (basicMaterial != null)
                {
                    basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                    renderer.ClearMaterials();
                    renderer.AddMaterial(basicMaterial);
                }
            }
        }

        private void RebuildMaterialArray(MeshRenderer renderer)
        {
            List<Material> validMaterials = new List<Material>();
            
            for (int i = 0; i < renderer.MaterialCount; i++)
            {
                var mat = renderer.GetMaterial(i);
                if (mat != null)
                {
                    validMaterials.Add(mat);
                }
            }
            
            renderer.ClearMaterials();
            
            foreach (var mat in validMaterials)
            {
                renderer.AddMaterial(mat);
            }
        }

        private bool HasAnyValidMaterial(MeshRenderer renderer)
        {
            for (int i = 0; i < renderer.MaterialCount; i++)
            {
                if (renderer.GetMaterial(i) != null)
                    return true;
            }
            return false;
        }

        private int CountValidMaterials(MeshRenderer renderer)
        {
            int count = 0;
            for (int i = 0; i < renderer.MaterialCount; i++)
            {
                if (renderer.GetMaterial(i) != null)
                    count++;
            }
            return count;
        }

        private Material GetFirstValidMaterial(MeshRenderer renderer)
        {
            for (int i = 0; i < renderer.MaterialCount; i++)
            {
                var material = renderer.GetMaterial(i);
                if (material != null)
                    return material;
            }
            return null;
        }

        private void RenderSpriteRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var spriteRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<SpriteRenderer>();
            if (spriteRenderers == null)
                return;

            foreach (var go in spriteRenderers)
            {
                var renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null || !renderer.Enabled)
                    continue;

                if (renderer.Material == null)
                {
                    var basicMaterial = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basicMaterial != null)
                    {
                        basicMaterial.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));
                        renderer.SetMaterial(basicMaterial);
                    }
                }

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

            foreach (var kvp in _spriteInstanceGroups)
            {
                var model = kvp.Key.model;
                var material = kvp.Key.material;
                var matrices = kvp.Value;

                if (matrices.Count == 0)
                    continue;

                model.SetupInstancing(matrices.ToArray());

                material.SetPBRProperties();
                material.Use();

                material.SetInt("u_UseInstancing", 1);
                material.SetMatrix4("view", view);
                material.SetMatrix4("projection", projection);
                material.SetVector3("u_CameraPos", cameraPos);

                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

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

                renderer.UpdateInstanceData();

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

                    material.SetInt("u_UseInstancing", 1);
                    material.SetMatrix4("view", view);
                    material.SetMatrix4("projection", projection);
                    material.SetVector3("u_CameraPos", cameraPos);

                    _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                    model.DrawInstanced(instanceCount);
                }
            }
        }

        public void ToggleWireframe()
        {
            WireframeMode = !WireframeMode;
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
        }

        public void Shutdown()
        {
            ClearInstanceGroups();
        }

        public Camera GetCamera() => _camera;

        public LightManager GetLightManager() => _lightManager;
    }
}