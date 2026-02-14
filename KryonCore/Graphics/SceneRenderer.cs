using KrayonCore.Graphics;
using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.GraphicsData;
using LightingSystem;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore
{
    public class SceneRenderer
    {
        // ── Estado ───────────────────────────────────────────────────────────
        private LightManager _lightManager = new();

        private readonly Dictionary<(Model model, Material material), List<Matrix4>>
            _meshInstanceGroups = new(),
            _spriteInstanceGroups = new();

        // ── Render Attachments ───────────────────────────────────────────────
        private readonly Dictionary<string, Action<Matrix4, Matrix4, Vector3>> _renderAttachments = new();

        public bool WireframeMode { get; set; } = false;

        public SceneRenderer()
        {
            CameraManager.Instance.Create(
                "main",
                new Vector3(0, 0, 5),
                WindowConfig.Width / (float)WindowConfig.Height,
                priority: 0
            );
        }

        // ── Inicialización ───────────────────────────────────────────────────
        public void Initialize()
        {
            _lightManager = new LightManager();

            CameraManager.Instance.Create(
                "main",
                new Vector3(0, 0, 5),
                WindowConfig.Width / (float)WindowConfig.Height,
                priority: 0
            );
        }

        // ── API de Render Attachments ────────────────────────────────────────

        /// <summary>
        /// Agrega un callback de renderizado que se ejecuta después de renderizar la escena.
        /// </summary>
        /// <param name="name">Nombre único del attachment</param>
        /// <param name="renderCallback">Callback que recibe (view, projection, cameraPos)</param>
        public void AttachRender(string name, Action<Matrix4, Matrix4, Vector3> renderCallback)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("El nombre no puede estar vacío", nameof(name));

            _renderAttachments[name] = renderCallback;
        }

        /// <summary>
        /// Remueve un render attachment por nombre.
        /// </summary>
        public bool DetachRender(string name)
        {
            return _renderAttachments.Remove(name);
        }

        /// <summary>
        /// Limpia todos los render attachments.
        /// </summary>
        public void ClearRenderAttachments()
        {
            _renderAttachments.Clear();
        }

        /// <summary>
        /// Obtiene un render attachment por nombre.
        /// </summary>
        public Action<Matrix4, Matrix4, Vector3>? GetRenderAttachment(string name)
        {
            return _renderAttachments.TryGetValue(name, out var callback) ? callback : null;
        }

        /// <summary>
        /// Verifica si existe un render attachment con el nombre dado.
        /// </summary>
        public bool HasRenderAttachment(string name)
        {
            return _renderAttachments.ContainsKey(name);
        }

        // ── Render ───────────────────────────────────────────────────────────
        public void Render()
        {
            GL.PolygonMode(MaterialFace.FrontAndBack,
                WireframeMode ? PolygonMode.Line : PolygonMode.Fill);

            foreach (var renderCam in CameraManager.Instance.GetRenderOrder())
                RenderFromCamera(renderCam);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void RenderFromCamera(RenderCamera renderCam)
        {
            // Resolver qué framebuffer usar
            FrameBuffer? target = renderCam.GetTargetBuffer();

            if (target is null)
            {
                // Sin buffer propio → usa el buffer de escena principal
                target = FrameBufferManager.Instance.TryGet("scene");
                if (target is null) return;
            }

            target.Bind();

            // Viewport en píxeles dentro del buffer
            int vpX = (int)(renderCam.ViewportX * target.Width);
            int vpY = (int)(renderCam.ViewportY * target.Height);
            int vpW = (int)(renderCam.ViewportWidth * target.Width);
            int vpH = (int)(renderCam.ViewportHeight * target.Height);
            GL.Viewport(vpX, vpY, vpW, vpH);

            // Clear
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

            // Matrices de esta cámara
            var cam = renderCam.Camera;
            var view = cam.GetViewMatrix();
            var projection = cam.GetProjectionMatrix();
            var cameraPos = cam.Position;

            RenderMeshRenderers(view, projection, cameraPos);
            RenderSpriteRenderers(view, projection, cameraPos);
            RenderTileRenderers(view, projection, cameraPos);

            // Ejecutar render attachments
            RenderAttachments(view, projection, cameraPos);

            ClearInstanceGroups();
            target.Unbind();
        }

        /// <summary>
        /// Ejecuta todos los render attachments registrados con las matrices de la cámara actual.
        /// </summary>
        private void RenderAttachments(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            if (_renderAttachments.Count == 0) return;

            foreach (var kvp in _renderAttachments)
            {
                try
                {
                    kvp.Value?.Invoke(view, projection, cameraPos);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SceneRenderer] Error en render attachment '{kvp.Key}': {ex.Message}");
                }
            }
        }

        // ── Instanciado ──────────────────────────────────────────────────────
        private void ClearInstanceGroups()
        {
            foreach (var kvp in _meshInstanceGroups)
                kvp.Key.model.ClearInstancing();
            _meshInstanceGroups.Clear();

            foreach (var kvp in _spriteInstanceGroups)
                kvp.Key.model.ClearInstancing();
            _spriteInstanceGroups.Clear();
        }

        // ── Mesh renderers ───────────────────────────────────────────────────
        private void RenderMeshRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var meshRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<MeshRenderer>();
            if (meshRenderers is null) return;

            var multiMaterialObjects = new List<GameObject>();

            foreach (var go in meshRenderers)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                var transform = go.GetComponent<Transform>();

                if (renderer is null || !renderer.Enabled || renderer.Model is null || transform is null)
                    continue;

                ValidateAndFixMaterials(renderer);

                int validCount = CountValidMaterials(renderer);
                Matrix4 worldMatrix = transform.GetWorldMatrix();

                if (validCount == 1)
                {
                    var mat = GetFirstValidMaterial(renderer);
                    if (mat is null) continue;

                    var key = (renderer.Model, mat);
                    if (!_meshInstanceGroups.ContainsKey(key))
                        _meshInstanceGroups[key] = new List<Matrix4>();

                    _meshInstanceGroups[key].Add(worldMatrix);
                }
                else if (validCount > 1)
                {
                    multiMaterialObjects.Add(go);
                }
            }

            foreach (var kvp in _meshInstanceGroups)
            {
                var (model, material) = kvp.Key;
                var matrices = kvp.Value;
                if (matrices.Count == 0) continue;

                model.SetupInstancing(matrices.ToArray());
                SetupMaterial(material, view, projection, cameraPos, instanced: true);
                model.DrawInstanced(matrices.Count);
                model.ClearInstancing();
            }

            foreach (var go in multiMaterialObjects)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                var transform = go.GetComponent<Transform>();
                if (renderer?.Model is null || transform is null) continue;

                renderer.Model.ClearInstancing();
                RenderMeshWithMultipleMaterials(
                    renderer, transform.GetWorldMatrix(), view, projection, cameraPos);
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
                var material = (i < renderer.MaterialCount ? renderer.GetMaterial(i) : null)
                            ?? (renderer.MaterialCount > 0 ? renderer.GetMaterial(0) : null);

                if (material is null)
                {
                    fallback ??= GraphicsEngine.Instance.Materials.Get("basic");
                    fallback?.SetVector3Cached("u_Color", new Vector3(1f, 0f, 1f));
                    material = fallback;
                }

                if (material is null) continue;

                SetupMaterial(material, view, projection, cameraPos, instanced: false);
                material.SetMatrix4("model", worldMatrix);
                _lightManager.ApplyLightsToShader(material.Shader.ProgramID);

                GL.DrawElementsBaseVertex(
                    PrimitiveType.Triangles,
                    renderer.Model.GetSubmeshIndexCount(i),
                    DrawElementsType.UnsignedInt,
                    (IntPtr)(renderer.Model.GetSubmeshBaseIndex(i) * sizeof(uint)),
                    renderer.Model.GetSubmeshBaseVertex(i)
                );
            }

            GL.BindVertexArray(0);
        }

        // ── Sprite renderers ─────────────────────────────────────────────────
        private void RenderSpriteRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var spriteRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<SpriteRenderer>();
            if (spriteRenderers is null) return;

            foreach (var go in spriteRenderers)
            {
                var renderer = go.GetComponent<SpriteRenderer>();
                var transform = go.GetComponent<Transform>();

                if (renderer is null || !renderer.Enabled || transform is null) continue;

                if (renderer.Material is null)
                {
                    var basic = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basic is not null)
                    {
                        basic.SetVector3Cached("u_Color", Vector3.One);
                        renderer.SetMaterial(basic);
                    }
                }

                if (renderer.QuadModel is null || renderer.Material is null) continue;

                var key = (renderer.QuadModel, renderer.Material);
                if (!_spriteInstanceGroups.ContainsKey(key))
                    _spriteInstanceGroups[key] = new List<Matrix4>();

                _spriteInstanceGroups[key].Add(transform.GetWorldMatrix());
            }

            foreach (var kvp in _spriteInstanceGroups)
            {
                var (model, material) = kvp.Key;
                var matrices = kvp.Value;
                if (matrices.Count == 0) continue;

                model.SetupInstancing(matrices.ToArray());
                SetupMaterial(material, view, projection, cameraPos, instanced: true);
                model.DrawInstanced(matrices.Count);
            }
        }

        // ── Tile renderers ───────────────────────────────────────────────────
        private void RenderTileRenderers(Matrix4 view, Matrix4 projection, Vector3 cameraPos)
        {
            var tileRenderers = SceneManager.ActiveScene?.FindGameObjectsWithComponent<TileRenderer>();
            if (tileRenderers is null) return;

            foreach (var go in tileRenderers)
            {
                var renderer = go.GetComponent<TileRenderer>();
                if (renderer is null || !renderer.Enabled) continue;

                if (renderer.MaterialCount == 0)
                {
                    var basic = GraphicsEngine.Instance.Materials.Get("basic");
                    if (basic is not null)
                    {
                        basic.SetVector3Cached("u_Color", Vector3.One);
                        renderer.AddMaterial(basic);
                    }
                }

                if (renderer.ModelCount == 0 || renderer.MaterialCount == 0 || renderer.TileCount == 0)
                    continue;

                renderer.UpdateInstanceData();

                foreach (var kvp in renderer.InstanceGroups)
                {
                    int instanceCount = kvp.Value.Count;
                    if (instanceCount == 0) continue;

                    var model = renderer.GetModel(kvp.Key.modelIndex);
                    var material = renderer.GetMaterial(kvp.Key.materialIndex);
                    if (model is null || material is null) continue;

                    SetupMaterial(material, view, projection, cameraPos, instanced: true);
                    model.DrawInstanced(instanceCount);
                }
            }
        }

        // ── Material helper ───────────────────────────────────────────────────
        private void SetupMaterial(Material material, Matrix4 view, Matrix4 projection,
            Vector3 cameraPos, bool instanced)
        {
            material.SetPBRProperties();
            material.Use();
            material.SetInt("u_UseInstancing", instanced ? 1 : 0);
            material.SetMatrix4("view", view);
            material.SetMatrix4("projection", projection);
            material.SetVector3("u_CameraPos", cameraPos);
            _lightManager.ApplyLightsToShader(material.Shader.ProgramID);
        }

        // ── Validación de materiales ─────────────────────────────────────────
        private void ValidateAndFixMaterials(MeshRenderer renderer)
        {
            if (renderer.Model is null) return;

            if (renderer.MaterialCount == 0 || !HasAnyValidMaterial(renderer))
            {
                var basic = GraphicsEngine.Instance.Materials.Get("basic");
                if (basic is null) return;
                basic.SetVector3Cached("u_Color", Vector3.One);
                renderer.ClearMaterials();
                renderer.AddMaterial(basic);
                return;
            }

            bool hasGaps = false;
            int lastValidIdx = -1;

            for (int i = 0; i < renderer.MaterialCount; i++)
            {
                if (renderer.GetMaterial(i) is not null)
                {
                    if (hasGaps) { RebuildMaterialArray(renderer); return; }
                    lastValidIdx = i;
                }
                else if (lastValidIdx >= 0)
                {
                    hasGaps = true;
                }
            }
        }

        private void RebuildMaterialArray(MeshRenderer renderer)
        {
            var valid = Enumerable.Range(0, renderer.MaterialCount)
                .Select(i => renderer.GetMaterial(i))
                .Where(m => m is not null)
                .ToList();

            renderer.ClearMaterials();
            foreach (var m in valid) renderer.AddMaterial(m!);
        }

        private bool HasAnyValidMaterial(MeshRenderer renderer)
            => Enumerable.Range(0, renderer.MaterialCount)
                .Any(i => renderer.GetMaterial(i) is not null);

        private int CountValidMaterials(MeshRenderer renderer)
            => Enumerable.Range(0, renderer.MaterialCount)
                .Count(i => renderer.GetMaterial(i) is not null);

        private Material? GetFirstValidMaterial(MeshRenderer renderer)
            => Enumerable.Range(0, renderer.MaterialCount)
                .Select(i => renderer.GetMaterial(i))
                .FirstOrDefault(m => m is not null);

        // ── API pública ───────────────────────────────────────────────────────
        public void ToggleWireframe() => WireframeMode = !WireframeMode;
        public void SetWireframeMode(bool val) => WireframeMode = val;

        public void Resize(int width, int height)
        {
            CameraManager.Instance.ResizeAll(width, height);
        }

        public void Update(float deltaTime) { }

        public void Shutdown()
        {
            ClearInstanceGroups();
            //ClearRenderAttachments();

            var cameras = CameraManager.Instance.GetRenderOrder().ToList();
            foreach (var cam in cameras)
            {
                if (cam.Name != "main")
                {
                    CameraManager.Instance.Remove(cam.Name);
                }
            }
        }

        /// <summary>Devuelve la cámara principal para uso del editor.</summary>
        public Camera GetCamera()
            => CameraManager.Instance.Main?.Camera
            ?? throw new InvalidOperationException("No hay cámara principal registrada.");

        public LightManager GetLightManager() => _lightManager;
    }
}