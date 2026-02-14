using KrayonCore.Audio;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.Core.Rendering;
using KrayonCore.Graphics.FrameBuffers;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrayonCore.GraphicsData
{
    public sealed class GraphicsEngine
    {
        // ── Singleton ────────────────────────────────────────────────────────
        public static GraphicsEngine? Instance { get; private set; }

        // ── Ventana ──────────────────────────────────────────────────────────
        private GameWindowInternal? _window;

        // ── Rendering ────────────────────────────────────────────────────────
        private readonly SceneRenderer _sceneRenderer;
        public FullscreenQuad? _fullscreenQuad;
        private ScreenQuad? _screenQuad;
        private float _totalTime;

        // ── Subsistemas ──────────────────────────────────────────────────────
        private readonly MaterialManager _materials;
        private InputSystem? _inputSystem;
        public readonly AudioManager Audio = new();

        // ── Nombres de buffers del editor ────────────────────────────────────
        private const string SceneBufferName = "scene";
        private const string PostProcessBufferName = "postProcess";

        // ── Propiedades públicas ─────────────────────────────────────────────
        public MaterialManager Materials => _materials;
        public InputSystem? Input => _inputSystem;
        public PostProcessingSettings? PostProcessing => _fullscreenQuad?.GetSettings();
        public GameWindow? Window => _window;
        public FrameBufferManager Buffers => FrameBufferManager.Instance;

        // ── Eventos públicos ─────────────────────────────────────────────────
        public event Action? OnLoad;
        public event Action<float>? OnUpdate;
        public event Action<float>? OnRender;
        public event Action<int, int>? OnResize;
        public event Action? OnClose;
        public event Action<TextInputEventArgs>? OnTextInput;
        public event Action<string[]>? OnFileDrop;

        // ── Constructor ──────────────────────────────────────────────────────
        public GraphicsEngine()
        {
            Instance = this;
            _materials = new MaterialManager();
            _sceneRenderer = new SceneRenderer();
        }

        // ── API pública ──────────────────────────────────────────────────────
        public void CreateWindow(int width, int height, string title)
        {
            _window = new GameWindowInternal(width, height, title, this);
            _inputSystem = new InputSystem(_window);
        }

        public void Run()
        {
            if (_window is null)
                throw new InvalidOperationException("Llama a CreateWindow() antes de Run().");
            _window.Run();
        }

        public KeyboardState GetKeyboardState() => _window?.KeyboardState ?? default;
        public MouseState GetMouseState() => _window?.MouseState ?? default;
        public SceneRenderer GetSceneRenderer() => _sceneRenderer;

        public FrameBuffer GetSceneFrameBuffer()
        {
            if (_fullscreenQuad?.GetSettings().Enabled == false)
                return Buffers.Get(SceneBufferName);
            return Buffers.Get(PostProcessBufferName);
        }

        public void ResizeFrameBuffer(string name, int width, int height)
            => Buffers.Resize(name, width, height);

        public void ResizeAllFrameBuffers(int width, int height)
            => Buffers.ResizeAll(width, height);

        // ── Callbacks internos ───────────────────────────────────────────────
        internal void InternalTextInput(TextInputEventArgs e)
            => OnTextInput?.Invoke(e);

        internal void InternalFileDrop(FileDropEventArgs e)
        {
            if (e.FileNames is { Length: > 0 })
            {
                Console.WriteLine($"[FileDrop] {string.Join(", ", e.FileNames)}");
                OnFileDrop?.Invoke(e.FileNames);
            }
        }

        internal void InternalLoad()
        {
            AssetManager.Initialize();

            if (SceneManager.ActiveScene is null)
            {
                SceneManager.CreateScene("DefaultScene");
                SceneManager.LoadScene("DefaultScene");
            }

            _materials.LoadMaterialsData();
            CreateDefaultMaterials();

            InitializeFrameBuffers();
            _sceneRenderer.Initialize();

            InitializeFullscreenQuad();
            InitializeScreenQuad();

            ConfigureDefaultPostProcessing();
            PostProcessing?.Load(AssetManager.VFXPath);

            if (AppInfo.IsCompiledGame)
                CSharpScriptManager.Instance.Reload();

            OnLoad?.Invoke();
        }

        internal void InternalUpdate(float deltaTime)
        {
            _totalTime += deltaTime;
            TimerData.DeltaTime = deltaTime;

            SceneManager.Update(deltaTime);
            _sceneRenderer.Update(deltaTime);
            OnUpdate?.Invoke(deltaTime);

            _inputSystem?.ClearFrameData();
        }

        internal void InternalRender(float deltaTime)
        {
            // 1. El SceneRenderer dibuja cada cámara en su buffer de escena
            // (esto ocurre dentro de SceneRenderer.Render → RenderFromCamera)
            _sceneRenderer.Render();

            // 2. Post-proceso para cada cámara con buffer propio
            ApplyPostProcessToAllCameras();

            // 3. Post-proceso del buffer de escena del editor (cámara "main")
            ApplyEditorPostProcess();

            // 4. Composición final al backbuffer
            RenderToScreen();

            SceneManager.Render();
            OnRender?.Invoke(deltaTime);
        }

        internal void InternalResize(int width, int height)
        {
            Buffers.ResizeAll(width, height);
            _sceneRenderer.Resize(width, height);
            OnResize?.Invoke(width, height);
        }

        internal void InternalClose()
        {
            SceneManager.ActiveScene?.OnUnload();
            _fullscreenQuad?.Dispose();
            _screenQuad?.Dispose();
            Buffers.Dispose();
            _sceneRenderer.Shutdown();
            OnClose?.Invoke();
        }

        // ── Post-proceso por cámara ──────────────────────────────────────────

        /// <summary>
        /// Aplica el fullscreen quad de post-proceso al buffer de escena
        /// de cada RenderCamera que tenga buffer propio y PP habilitado.
        /// </summary>
        private void ApplyPostProcessToAllCameras()
        {
            if (_fullscreenQuad is null) return;

            var ppSettings = _fullscreenQuad.GetSettings();
            if (!ppSettings.Enabled) return;

            foreach (var renderCam in CameraManager.Instance.GetRenderOrder())
            {
                if (!renderCam.PostProcessingEnabled) continue;

                var sceneBuffer = renderCam.GetTargetBuffer();
                var ppBuffer = renderCam.GetPostProcessBuffer();

                if (sceneBuffer is null || ppBuffer is null) continue;

                ApplyPostProcess(
                    sceneBuffer, ppBuffer,
                    renderCam.Camera.GetProjectionMatrix(),
                    renderCam.Camera.GetViewMatrix()
                );
            }
        }

        /// <summary>Post-proceso para el buffer del editor (cámara "main").</summary>
        private void ApplyEditorPostProcess()
        {
            if (_fullscreenQuad is null) return;
            if (!_fullscreenQuad.GetSettings().Enabled) return;

            var scene = Buffers.TryGet(SceneBufferName);
            var postProcess = Buffers.TryGet(PostProcessBufferName);

            if (scene is null || postProcess is null) return;

            var camera = _sceneRenderer.GetCamera();
            ApplyPostProcess(
                scene, postProcess,
                camera.GetProjectionMatrix(),
                camera.GetViewMatrix()
            );
        }

        /// <summary>Aplica el fullscreen quad desde un buffer de escena a un buffer de destino.</summary>
        private void ApplyPostProcess(FrameBuffer source, FrameBuffer dest,
            Matrix4 projection, Matrix4 view)
        {
            if (_fullscreenQuad is null) return;

            dest.Bind();
            GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _fullscreenQuad.Render(
                source.ColorTexture,
                source.EmissionTexture,
                source.PositionTexture,
                source.NormalTexture,
                _totalTime,
                source.Width,
                source.Height,
                projection,
                view
            );

            dest.Unbind();
        }

        // ── Composición final ────────────────────────────────────────────────

        /// <summary>
        /// Renderiza al backbuffer usando la cámara "main" del editor.
        /// El game viewport usa directamente la textura del buffer de cada cámara.
        /// </summary>
        private void RenderToScreen()
        {
            if (_window is null || _screenQuad is null) return;

            var ppEnabled = _fullscreenQuad?.GetSettings().Enabled == true;
            var finalTex = ppEnabled
                ? Buffers.TryGet(PostProcessBufferName)?.ColorTexture ?? 0
                : Buffers.TryGet(SceneBufferName)?.ColorTexture ?? 0;

            if (finalTex == 0) return;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
            GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _screenQuad.Render(finalTex);
        }

        // ── Helpers privados ─────────────────────────────────────────────────
        private void InitializeFrameBuffers()
        {
            Buffers.Create(SceneBufferName, 1280, 720, useEmission: true, useGBuffer: true);
            Buffers.Create(PostProcessBufferName, 1280, 720, useEmission: false, useGBuffer: false);
        }

        private void InitializeFullscreenQuad()
        {
            _fullscreenQuad = new FullscreenQuad();
            var material = _materials.Get("fullscreen");
            if (material is not null)
                _fullscreenQuad.SetMaterial(material);
        }

        private void InitializeScreenQuad()
        {
            _screenQuad = new ScreenQuad();
            var material = _materials.Get("screen");
            if (material is not null)
                _screenQuad.SetMaterial(material);
        }

        private void CreateDefaultMaterials()
        {
            CreateMaterial("basic",
                Guid.Parse("9557923a-76ad-432c-abc9-17ed77e82311"),
                Guid.Parse("5ed03f4d-deb0-4de7-a5d6-182e3a60df7f"),
                m => m.SetVector3Cached("u_Color", Vector3.One));

            CreateMaterial("fullscreen",
                Guid.Parse("1e6bf414-5684-458e-8d8b-4e9761dfa320"),
                Guid.Parse("ddcb17f8-2875-4229-a74f-63353d482808"));

            CreateMaterial("screen",
                Guid.Parse("ffbdd214-94e0-467c-b2ec-f220b5788c2b"),
                Guid.Parse("91c14d42-adb9-45c8-a527-ff6335115397"));
        }

        private void CreateMaterial(string name, Guid vert, Guid frag,
            Action<Material>? configure = null)
        {
            var mat = _materials.Create(name, vert, frag);
            if (mat is not null) configure?.Invoke(mat);
        }

        private void ConfigureDefaultPostProcessing()
        {
            if (PostProcessing is null) return;

            PostProcessing.Enabled = true;
            PostProcessing.ColorCorrectionEnabled = true;
            PostProcessing.Brightness = 0.0f;
            PostProcessing.Contrast = 1.05f;
            PostProcessing.Saturation = 1.1f;
            PostProcessing.ColorFilter = Vector3.One;
            PostProcessing.BloomEnabled = true;
            PostProcessing.BloomThreshold = 0.9f;
            PostProcessing.BloomSoftThreshold = 0.5f;
            PostProcessing.BloomIntensity = 0.8f;
            PostProcessing.BloomRadius = 4.0f;
            PostProcessing.GrainEnabled = true;
            PostProcessing.GrainIntensity = 0.03f;
            PostProcessing.GrainSize = 1.2f;
            PostProcessing.SSAOEnabled = false;
            PostProcessing.SSAOKernelSize = 64;
            PostProcessing.SSAORadius = 0.5f;
            PostProcessing.SSAOBias = 0.025f;
            PostProcessing.SSAOPower = 2.0f;

            _fullscreenQuad?.GetSettings().Load($"{AssetManager.BasePath}VFXData.json");
        }
    }
}