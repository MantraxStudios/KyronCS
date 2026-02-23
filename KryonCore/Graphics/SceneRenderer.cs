using KrayonCore.Animation;
using KrayonCore.Components;
using KrayonCore.Components.RenderComponents;
using KrayonCore.Core.Rendering;
using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.Graphics.GameUI;
using KrayonCore.GraphicsData;
using LightingSystem;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class SceneRenderer
    {
        private LightManager _lightManager = new();

        private readonly Dictionary<(Model model, Material material), List<Matrix4>>
            _meshInstanceGroups = new(),
            _spriteInstanceGroups = new();

        private readonly List<SkyboxRenderer> _skyboxRenderers = new();
        private readonly List<MeshRenderer> _meshRenderers = new();
        private readonly List<AnimatedMeshRenderer> _animatedMeshRenderers = new();
        private readonly List<SpriteRenderer> _spriteRenderers = new();
        private readonly List<TileRenderer> _tileRenderers = new();

        private readonly Dictionary<string, Action<Matrix4, Matrix4, Vector3>> _renderAttachments = new();

        private bool _needsCleanup = false;

        public CameraManager ManagerCams = new();
        public FrameBufferManager Buffers = new();
        public bool WireframeMode { get; set; } = false;

        public SceneRenderer() => Initialize();

        public void Initialize()
        {
            _lightManager = new LightManager();
            
            Buffers.Create("scene", 1280, 720, useEmission: true, useGBuffer: true);
            Buffers.Create("postProcess", 1280, 720, useEmission: false, useGBuffer: false);

            ManagerCams.Create("main", new Vector3(0, 0, 5),
                WindowConfig.Width / (float)WindowConfig.Height, priority: 0);

            Console.WriteLine($"Esta Escena Tiene un total de {ManagerCams.Count}");
        }

        public void RegisterRenderer<T>(T renderer) where T : class
        {
            switch (renderer)
            {
                case SkyboxRenderer r when !_skyboxRenderers.Contains(r): _skyboxRenderers.Add(r); break;
                case AnimatedMeshRenderer r when !_animatedMeshRenderers.Contains(r): _animatedMeshRenderers.Add(r); break;
                case MeshRenderer r when !_meshRenderers.Contains(r): _meshRenderers.Add(r); break;
                case SpriteRenderer r when !_spriteRenderers.Contains(r): _spriteRenderers.Add(r); break;
                case TileRenderer r when !_tileRenderers.Contains(r): _tileRenderers.Add(r); break;
            }
        }

        public void UnregisterRenderer<T>(T renderer) where T : class
        {
            bool removed = renderer switch
            {
                SkyboxRenderer r => _skyboxRenderers.Remove(r),
                AnimatedMeshRenderer r => _animatedMeshRenderers.Remove(r),
                MeshRenderer r => _meshRenderers.Remove(r),
                SpriteRenderer r => _spriteRenderers.Remove(r),
                TileRenderer r => _tileRenderers.Remove(r),
                _ => false
            };
            if (removed) _needsCleanup = true;
        }

        public void ClearAllRenderers()
        {
            _skyboxRenderers.Clear();
            _meshRenderers.Clear();
            _animatedMeshRenderers.Clear();
            _spriteRenderers.Clear();
            _tileRenderers.Clear();
            _needsCleanup = false;
        }

        private void CleanupNullRenderers()
        {
            if (!_needsCleanup) return;
            _skyboxRenderers.RemoveAll(r => r is null || r.GameObject is null);
            _meshRenderers.RemoveAll(r => r is null || r.GameObject is null);
            _animatedMeshRenderers.RemoveAll(r => r is null || r.GameObject is null);
            _spriteRenderers.RemoveAll(r => r is null || r.GameObject is null);
            _tileRenderers.RemoveAll(r => r is null || r.GameObject is null);
            _needsCleanup = false;
        }

        public void AttachRender(string name, Action<Matrix4, Matrix4, Vector3> renderCallback)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre no puede estar vacío", nameof(name));
            _renderAttachments[name] = renderCallback;
        }

        public bool DetachRender(string name) => _renderAttachments.Remove(name);
        public void ClearRenderAttachments() => _renderAttachments.Clear();
        public Action<Matrix4, Matrix4, Vector3>? GetRenderAttachment(string name)
            => _renderAttachments.TryGetValue(name, out var cb) ? cb : null;
        public bool HasRenderAttachment(string name) => _renderAttachments.ContainsKey(name);

        public UICanvas CreateCanvas(string name, int sortOrder = 0) => UICanvasManager.Create(name, this, sortOrder);
        public UICanvas? GetCanvas(string name) => UICanvasManager.Get(name);

        public void Update(float deltaTime) => UICanvasManager.Update(deltaTime);
        public void Resize(int width, int height)
        {
            Buffers.ResizeAll(width, height);
            ManagerCams.ResizeAll(width, height);
        }
        public Camera GetCamera() => ManagerCams.Main?.Camera
            ?? throw new InvalidOperationException("No hay cámara principal registrada.");
        public LightManager GetLightManager() => _lightManager;
        public void ToggleWireframe() => WireframeMode = !WireframeMode;
        public void SetWireframeMode(bool val) => WireframeMode = val;
        public int GetRegisteredRenderersCount()
            => _skyboxRenderers.Count + _meshRenderers.Count +
               _animatedMeshRenderers.Count + _spriteRenderers.Count + _tileRenderers.Count;
        public (int skybox, int mesh, int animatedMesh, int sprite, int tile) GetRendererCounts()
            => (_skyboxRenderers.Count, _meshRenderers.Count,
                _animatedMeshRenderers.Count, _spriteRenderers.Count, _tileRenderers.Count);

        public void Shutdown()
        {
            ClearInstanceGroups();
            ClearAllRenderers();
            UICanvasManager.Shutdown();
            Buffers.Dispose();
        }

        public void Render()
        {
            CleanupNullRenderers();
            GL.PolygonMode(MaterialFace.FrontAndBack, WireframeMode ? PolygonMode.Line : PolygonMode.Fill);
            foreach (var renderCam in ManagerCams.GetRenderOrder())
                RenderFromCamera(renderCam);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void RenderFromCamera(RenderCamera renderCam)
        {
            var target = renderCam.GetTargetBuffer() ?? Buffers.TryGet("scene");
            if (target is null) return;

            target.Bind();
            GL.Viewport(
                (int)(renderCam.ViewportX * target.Width),
                (int)(renderCam.ViewportY * target.Height),
                (int)(renderCam.ViewportWidth * target.Width),
                (int)(renderCam.ViewportHeight * target.Height));

            switch (renderCam.ClearMode)
            {
                case CameraClearMode.SolidColor:
                    var c = renderCam.ClearColor;
                    GL.ClearColor(c.R, c.G, c.B, c.A);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    break;
                case CameraClearMode.DontClear:
                    GL.Clear(ClearBufferMask.DepthBufferBit);
                    break;
            }

            var cam = renderCam.Camera;
            var view = cam.GetViewMatrix();
            var projection = cam.GetProjectionMatrix();
            var cameraPos = cam.Position;

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            RenderSkybox(view, projection, cameraPos);
            RenderMeshRenderers(view, projection, cameraPos);
            RenderAnimatedMeshRenderers(view, projection, cameraPos);
            RenderSpriteRenderers(view, projection, cameraPos);
            RenderTileRenderers(view, projection, cameraPos);
            RenderAttachments(view, projection, cameraPos);

            ClearInstanceGroups();
            target.Unbind();
        }

        private void RenderAttachments(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_renderAttachments.Count == 0) return;
            foreach (var kvp in _renderAttachments.OrderBy(k => k.Key))
            {
                try { kvp.Value?.Invoke(view, projection, cameraPos); }
                catch (Exception ex) { Console.WriteLine($"[SceneRenderer] Error en render attachment '{kvp.Key}': {ex.Message}"); }
            }
        }

        private void RenderSkybox(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_skyboxRenderers.Count == 0) return;

            GL.DepthFunc(DepthFunction.Lequal);
            bool cullFaceEnabled = GL.IsEnabled(EnableCap.CullFace);
            int prevCullMode = cullFaceEnabled ? GL.GetInteger(GetPName.CullFaceMode) : 0;
            if (!cullFaceEnabled) GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);

            foreach (var renderer in _skyboxRenderers)
            {
                if (renderer is null || !renderer.Enabled || renderer.GameObject is null)
                { _needsCleanup = true; continue; }

                var transform = renderer.GameObject.GetComponent<Transform>();
                if (transform is null) continue;

                if (renderer.Material is null)
                {
                    var skyboxMat = GraphicsEngine.Instance.Materials.Get("Sky");
                    if (skyboxMat is not null) renderer.SetMaterial(skyboxMat);
                }
                if (renderer.SphereModel is null || renderer.Material is null) continue;

                Matrix4 worldMatrix = renderer.GetRotationMatrix() * transform.GetWorldMatrix();

                renderer.Material.Use();
                renderer.Material.SetMatrix4("model", worldMatrix);
                renderer.Material.SetMatrix4("view", view);
                renderer.Material.SetMatrix4("projection", projection);
                renderer.Material.SetVector3("u_CameraPos", cameraPos);
                renderer.Material.SetInt("u_UseInstancing", 0);

                if (renderer.Material.AlbedoTexture is null)
                    renderer.Material.SetVector3("u_AlbedoColor",
                        new Vector3(renderer.Color.X, renderer.Color.Y, renderer.Color.Z));

                renderer.Material.SetFloat("u_Alpha", renderer.Color.W);
                renderer.Draw();
            }

            GL.DepthFunc(DepthFunction.Less);
            if (cullFaceEnabled) GL.CullFace((CullFaceMode)prevCullMode);
            else GL.Disable(EnableCap.CullFace);
        }

        private void RenderMeshRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_meshRenderers.Count == 0) return;

            var multiMat = new List<(MeshRenderer renderer, Transform transform)>();

            foreach (var renderer in _meshRenderers)
            {
                if (renderer is null || !renderer.Enabled || renderer.Model is null || renderer.GameObject is null)
                { if (renderer is null || renderer.GameObject is null) _needsCleanup = true; continue; }

                var transform = renderer.GameObject.GetComponent<Transform>();
                if (transform is null) continue;

                ValidateAndFixMaterials(renderer);

                if (renderer.Model.SubMeshCount > 1)
                { multiMat.Add((renderer, transform)); continue; }

                var mat = GetFirstValidMaterial(renderer);
                if (mat is null) continue;

                var key = (renderer.Model, mat);
                if (!_meshInstanceGroups.ContainsKey(key)) _meshInstanceGroups[key] = new List<Matrix4>();
                _meshInstanceGroups[key].Add(transform.GetWorldMatrix());
            }

            foreach (var kvp in _meshInstanceGroups)
            {
                var (model, material) = kvp.Key;
                var matrices = kvp.Value;
                if (matrices.Count == 0) continue;

                if (matrices.Count == 1)
                {
                    SetupMaterial(material, view, projection, cameraPos, instanced: false);
                    material.SetInt("u_UseAnimation", 0);
                    material.SetMatrix4("model", matrices[0]);
                    model.Draw();
                }
                else
                {
                    model.SetupInstancing(matrices.ToArray());
                    SetupMaterial(material, view, projection, cameraPos, instanced: true);
                    material.SetInt("u_UseAnimation", 0);
                    model.DrawInstanced(matrices.Count);
                    model.ClearInstancing();
                }
            }

            foreach (var (renderer, transform) in multiMat)
            {
                if (renderer?.Model is null) continue;
                renderer.Model.ClearInstancing();
                RenderMeshWithMultipleMaterials(renderer, transform.GetWorldMatrix(), view, projection, cameraPos);
            }
        }

        private void RenderMeshWithMultipleMaterials(MeshRenderer renderer, Matrix4 worldMatrix,
            Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (renderer.Model is null) return;

            Material? fallback = null;
            GL.BindVertexArray(renderer.Model.GetVAO());

            for (int i = 0; i < renderer.Model.SubMeshCount; i++)
            {
                var material = renderer.GetMaterial(i)
                    ?? (fallback ??= GraphicsEngine.Instance.Materials.Get("basic"));
                if (material is null) continue;

                SetupMaterial(material, view, projection, cameraPos, instanced: false);
                material.SetMatrix4("model", worldMatrix);
                material.SetInt("u_UseAnimation", 0);
                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                GL.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    renderer.Model.GetSubmeshIndexCount(i),
                    DrawElementsType.UnsignedInt,
                    (IntPtr)(renderer.Model.GetSubmeshBaseIndex(i) * sizeof(uint)),
                    renderer.Model.GetSubmeshBaseVertex(i));
            }

            GL.BindVertexArray(0);
        }

        private void RenderAnimatedMeshRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_animatedMeshRenderers.Count == 0) return;

            foreach (var renderer in _animatedMeshRenderers)
            {
                if (renderer is null || !renderer.Enabled || renderer.AnimatedModel is null || renderer.GameObject is null)
                { if (renderer is null || renderer.GameObject is null) _needsCleanup = true; continue; }

                var transform = renderer.GameObject.GetComponent<Transform>();
                if (transform is null) continue;

                var animator = renderer.GetAnimator();
                var model = renderer.AnimatedModel;
                Matrix4 worldMatrix = transform.GetWorldMatrix();

                for (int i = 0; i < model.SubMeshCount; i++)
                {
                    var material = renderer.GetMaterial(i) ?? GraphicsEngine.Instance.Materials.Get("basic");
                    if (material is null) continue;

                    SetupMaterial(material, view, projection, cameraPos, instanced: false);
                    material.SetMatrix4("model", worldMatrix);

                    if (animator != null && animator.IsPlaying)
                        animator.UploadBoneMatrices(material.Shader.ProgramID);
                    else
                        Animator.DisableAnimation(material.Shader.ProgramID);

                    _lightManager.ApplyLightsToShader(material.Shader.ProgramID);
                    model.SubMeshes[i].Mesh.Draw();
                }
            }
        }

        private void RenderSpriteRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_spriteRenderers.Count == 0) return;

            foreach (var renderer in _spriteRenderers)
            {
                if (renderer is null || !renderer.Enabled || renderer.GameObject is null)
                { if (renderer is null || renderer.GameObject is null) _needsCleanup = true; continue; }

                var transform = renderer.GameObject.GetComponent<Transform>();
                if (transform is null) continue;

                if (renderer.Material is null)
                {
                    var basic = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basic is not null) { basic.SetVector3Cached("u_Color", Vector3.One); renderer.SetMaterial(basic); }
                }
                if (renderer.QuadModel is null || renderer.Material is null) continue;

                var key = (renderer.QuadModel, renderer.Material);
                if (!_spriteInstanceGroups.ContainsKey(key)) _spriteInstanceGroups[key] = new List<Matrix4>();
                _spriteInstanceGroups[key].Add(transform.GetWorldMatrix());
            }

            foreach (var kvp in _spriteInstanceGroups)
            {
                var (model, material) = kvp.Key;
                var matrices = kvp.Value;
                if (matrices.Count == 0) continue;

                model.SetupInstancing(matrices.ToArray());
                SetupMaterial(material, view, projection, cameraPos, instanced: true);
                material.SetInt("u_UseAnimation", 0);
                model.DrawInstanced(matrices.Count);
            }
        }

        private void RenderTileRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_tileRenderers.Count == 0) return;

            foreach (var renderer in _tileRenderers)
            {
                if (renderer is null || !renderer.Enabled)
                { if (renderer is null || renderer.GameObject is null) _needsCleanup = true; continue; }

                if (renderer.MaterialCount == 0)
                {
                    var basic = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basic is not null) { basic.SetVector3Cached("u_Color", Vector3.One); renderer.AddMaterial(basic); }
                }

                if (renderer.ModelCount == 0 || renderer.MaterialCount == 0 || renderer.TileCount == 0) continue;

                renderer.UpdateInstanceData();

                foreach (var kvp in renderer.InstanceGroups)
                {
                    if (kvp.Value.Count == 0) continue;
                    var model = renderer.GetModel(kvp.Key.modelIndex);
                    var material = renderer.GetMaterial(kvp.Key.materialIndex);
                    if (model is null || material is null) continue;

                    SetupMaterial(material, view, projection, cameraPos, instanced: true);
                    material.SetInt("u_UseAnimation", 0);
                    model.DrawInstanced(kvp.Value.Count);
                }
            }
        }

        private void SetupMaterial(Material material, Matrix4 view, Matrix4 projection,
            Vector3 cameraPos, bool instanced)
        {
            material.SetPBRProperties();
            material.Use();
            material.SetInt("u_UseInstancing", instanced ? 1 : 0);
            material.SetMatrix4("view", view);
            material.SetMatrix4("projection", projection);
            material.SetVector3("u_CameraPos", cameraPos);
            material.SetMatrix4("u_View", view);
            _lightManager.ApplyLightsToShader(material.Shader.ProgramID);
        }

        private void ValidateAndFixMaterials(MeshRenderer renderer)
        {
            if (renderer.Model is null || HasAnyValidMaterial(renderer)) return;
            var basic = GraphicsEngine.Instance.Materials.Get("basic");
            if (basic is null) return;
            basic.SetVector3Cached("u_Color", Vector3.One);
            var paths = Enumerable.Repeat(basic.Name, renderer.Model.SubMeshCount).ToArray();
            renderer.SaveMaterialPaths(paths);
        }

        private void ClearInstanceGroups()
        {
            foreach (var kvp in _meshInstanceGroups) kvp.Key.model.ClearInstancing();
            _meshInstanceGroups.Clear();
            foreach (var kvp in _spriteInstanceGroups) kvp.Key.model.ClearInstancing();
            _spriteInstanceGroups.Clear();
        }

        private bool HasAnyValidMaterial(MeshRenderer renderer)
            => Enumerable.Range(0, renderer.MaterialCount).Any(i => renderer.GetMaterial(i) is not null);

        private Material? GetFirstValidMaterial(MeshRenderer renderer)
            => Enumerable.Range(0, renderer.MaterialCount)
                .Select(i => renderer.GetMaterial(i))
                .FirstOrDefault(m => m is not null);
    }
}