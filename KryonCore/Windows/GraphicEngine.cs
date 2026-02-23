using KrayonCore.Audio;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.Core.Rendering;
using KrayonCore.Graphics.FrameBuffers;
using KrayonCore.Graphics.GameUI;
using KrayonCore.UI;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrayonCore.GraphicsData
{
    public sealed class GraphicsEngine
    {
        public static GraphicsEngine? Instance { get; private set; }

        private GameWindowInternal? _window;
        public FullscreenQuad? _fullscreenQuad;
        private ScreenQuad? _screenQuad;
        private float _totalTime;

        private readonly MaterialManager _materials;
        private InputSystem? _inputSystem;
        public readonly AudioManager Audio = new();

        private const string SceneBufferName = "scene";
        private const string PostProcessBufferName = "postProcess";

        public MaterialManager Materials => _materials;
        public InputSystem? Input => _inputSystem;
        public PostProcessingSettings? PostProcessing => _fullscreenQuad?.GetSettings();
        public GameWindow? Window => _window;
        public FrameBufferManager Buffers => GetSceneRenderer().Buffers;
        public List<SceneRenderer> SceneRenderers { get; } = new();

        public event Action? OnLoad;
        public event Action<float>? OnUpdate;
        public event Action<float>? OnRender;
        public event Action<int, int>? OnResize;
        public event Action? OnClose;
        public event Action<TextInputEventArgs>? OnTextInput;
        public event Action<string[]>? OnFileDrop;
        public event Action? OnTryClose;

        public GraphicsEngine()
        {
            Instance = this;
            _materials = new MaterialManager();
        }

        public SceneRenderer AddSceneRenderer()
        {
            var renderer = new SceneRenderer();
            SceneRenderers.Add(renderer);
            return renderer;
        }

        public void RemoveSceneRenderer(SceneRenderer renderer)
        {
            if (!SceneRenderers.Contains(renderer)) return;
            SceneRenderers.Remove(renderer);
            renderer.Shutdown();
        }

        public void CreateWindow(int width, int height, string title)
        {
            _window = new GameWindowInternal(width, height, title, this);
            _inputSystem = new InputSystem(_window);
            _window.OnTryClose += () => OnTryClose?.Invoke();
        }

        public void Run()
        {
            if (_window is null)
                throw new InvalidOperationException("Llama a CreateWindow() antes de Run().");
            _window.Run();
        }

        public KeyboardState GetKeyboardState() => _window?.KeyboardState ?? default;
        public MouseState GetMouseState() => _window?.MouseState ?? default;
        public SceneRenderer GetSceneRenderer() => SceneRenderers[0];

        public FrameBuffer GetSceneFrameBuffer() =>
            _fullscreenQuad?.GetSettings().Enabled == false
                ? Buffers.Get(SceneBufferName)
                : Buffers.Get(PostProcessBufferName);

        public void ResizeFrameBuffer(string name, int width, int height) => Buffers.Resize(name, width, height);
        public void ResizeAllFrameBuffers(int width, int height) => Buffers.ResizeAll(width, height);
        public void CanClose() => GameWindowInternal.CanIClose = true;
        public void CantNoClose() => GameWindowInternal.CanIClose = false;

        internal void InternalTextInput(TextInputEventArgs e) => OnTextInput?.Invoke(e);

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
            CSharpScriptManager.Instance.Initialize();

            if (SceneManager.PrimaryScene is null)
            {
                SceneManager.CreateScene("DefaultScene");
                SceneManager.LoadScene("DefaultScene");
            }

            _materials.LoadMaterialsData();
            CreateDefaultMaterials();
            InitializeFrameBuffers();
            InitializeFullscreenQuad();
            InitializeScreenQuad();
            ConfigureDefaultPostProcessing();
            PostProcessing?.Load(AssetManager.VFXPath);

            if (AppInfo.IsCompiledGame)
                CSharpScriptManager.Instance.Reload();

            if (_window is not null)
            {
                _window.TextInput += e => UIInputManager.AddTypedChar(e.AsString);
                _window.KeyDown += e => UIInputManager.AddKeyDown(e.Key);
            }

            OnLoad?.Invoke();
        }

        internal void InternalUpdate(float deltaTime)
        {
            _totalTime += deltaTime;
            TimerData.DeltaTime = deltaTime;

            SceneManager.Update(deltaTime);
            SceneRenderers.ForEach(r => r.Update(deltaTime));
            OnUpdate?.Invoke(deltaTime);
            _inputSystem?.ClearFrameData();
        }

        internal void InternalRender(float deltaTime)
        {
            SceneRenderers.ForEach(r => r.Render());
            ApplyPostProcessToAllCameras();

            if (!AppInfo.IsCompiledGame)
                ApplyEditorPostProcess();

            RenderToScreen();
            SceneManager.Render();
            OnRender?.Invoke(deltaTime);
        }

        internal void InternalResize(int width, int height)
        {
            Buffers.ResizeAll(width, height);
            SceneRenderers.ForEach(r => r.Resize(width, height));
            OnResize?.Invoke(width, height);
        }

        internal void InternalClose()
        {
            SceneManager.PrimaryScene?.OnUnload();
            _fullscreenQuad?.Dispose();
            _screenQuad?.Dispose();
            Buffers.Dispose();
            SceneRenderers.ForEach(r => r.Shutdown());
            Audio.Dispose();
            OnClose?.Invoke();
        }

        private void ApplyPostProcessToAllCameras()
        {
            if (_fullscreenQuad is null || !_fullscreenQuad.GetSettings().Enabled) return;

            foreach (var renderCam in SceneRenderers.SelectMany(r => r.ManagerCams.GetRenderOrder()))
            {
                if (!renderCam.PostProcessingEnabled) continue;

                var sceneBuffer = renderCam.GetTargetBuffer();
                var ppBuffer = renderCam.GetPostProcessBuffer();
                if (sceneBuffer is null || ppBuffer is null) continue;

                ApplyPostProcess(sceneBuffer, ppBuffer,
                    renderCam.Camera.GetProjectionMatrix(),
                    renderCam.Camera.GetViewMatrix());
            }
        }

        private void ApplyEditorPostProcess()
        {
            if (_fullscreenQuad is null || !_fullscreenQuad.GetSettings().Enabled) return;

            var scene = Buffers.TryGet(SceneBufferName);
            var postProcess = Buffers.TryGet(PostProcessBufferName);
            if (scene is null || postProcess is null) return;

            foreach (var renderer in SceneRenderers)
            {
                var camera = renderer.GetCamera();
                ApplyPostProcess(scene, postProcess,
                    camera.GetProjectionMatrix(),
                    camera.GetViewMatrix());
            }
        }

        private void ApplyPostProcess(FrameBuffer source, FrameBuffer dest, Matrix4 projection, Matrix4 view)
        {
            if (_fullscreenQuad is null) return;

            dest.Bind();
            GL.ClearColor(WindowConfig.ColorClear.X, WindowConfig.ColorClear.Y, WindowConfig.ColorClear.Z, WindowConfig.ColorClear.W);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _fullscreenQuad.Render(
                source.ColorTexture, source.EmissionTexture,
                source.PositionTexture, source.NormalTexture,
                _totalTime, source.Width, source.Height,
                projection, view);

            dest.Unbind();
        }

        private void RenderToScreen()
        {
            if (_window is null || _screenQuad is null) return;

            bool ppEnabled = _fullscreenQuad?.GetSettings().Enabled == true;

            int finalTex = AppInfo.IsCompiledGame
                ? GetSceneRenderer().ManagerCams.GetRenderOrder()
                    .FirstOrDefault(rc => rc.Name != "main")
                    ?.GetFinalTextureId(ppEnabled) ?? 0
                : ppEnabled
                    ? Buffers.TryGet(PostProcessBufferName)?.ColorTexture ?? 0
                    : Buffers.TryGet(SceneBufferName)?.ColorTexture ?? 0;

            if (finalTex == 0) return;

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
            GL.ClearColor(WindowConfig.ColorClear.X, WindowConfig.ColorClear.Y, WindowConfig.ColorClear.Z, WindowConfig.ColorClear.W);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _screenQuad.Render(finalTex);
        }

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
                AssetManager.FindByPath("shaders/basic.vert").Guid,
                AssetManager.FindByPath("shaders/basic.frag").Guid,
                m => m.SetVector3Cached("u_Color", Vector3.One));

            CreateMaterial("fullscreen",
                AssetManager.FindByPath("shaders/fullscreen.vert").Guid,
                AssetManager.FindByPath("shaders/fullscreen.frag").Guid);

            CreateMaterial("screen",
                AssetManager.FindByPath("shaders/screen.vert").Guid,
                AssetManager.FindByPath("shaders/screen.frag").Guid);
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