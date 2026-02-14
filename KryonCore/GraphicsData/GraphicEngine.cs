using Assimp;
using KrayonCore.Audio;
using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Core.Components;
using KrayonCore.Core.Input;
using KrayonCore.Core.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrayonCore.GraphicsData
{
    public class GraphicsEngine
    {
        private GameWindowInternal? _window;
        private SceneRenderer _sceneRenderer;
        private FrameBuffer? _sceneFrameBuffer;
        private FrameBuffer? _postProcessFrameBuffer;
        public FullscreenQuad? _fullscreenQuad;
        private ScreenQuad? _screenQuad;

        private float _totalTime = 0.0f;

        public event Action? LoadEvent;
        public event Action<float>? UpdateEvent;
        public event Action<float>? RenderEvent;
        public event Action<int, int>? ResizeEvent;
        public event Action? CloseEvent;

        public event Action<TextInputEventArgs>? TextInputEvent;
        public event Action<string[]>? FileDropEvent;

        public static GraphicsEngine? Instance { get; private set; }

        private MaterialManager _materials;
        public MaterialManager Materials => _materials;

        // Puede ser null hasta que se llame CreateWindow()
        private InputSystem? _inputSystem;
        public InputSystem? Input => _inputSystem;

        public PostProcessingSettings? PostProcessing => _fullscreenQuad?.GetSettings();
        public readonly AudioManager _audio = new AudioManager();

        public GraphicsEngine()
        {
            Instance = this;
            _materials = new MaterialManager();
            _sceneRenderer = new SceneRenderer();
        }

        private void CreateDefaultMaterials()
        {
            Guid basicVertGuid = Guid.Parse("9557923a-76ad-432c-abc9-17ed77e82311");
            Guid basicFragGuid = Guid.Parse("5ed03f4d-deb0-4de7-a5d6-182e3a60df7f");
            Guid fullscreenVertGuid = Guid.Parse("1e6bf414-5684-458e-8d8b-4e9761dfa320");
            Guid fullscreenFragGuid = Guid.Parse("ddcb17f8-2875-4229-a74f-63353d482808");
            Guid screenFragGuid = Guid.Parse("91c14d42-adb9-45c8-a527-ff6335115397");
            Guid screenVertGuid = Guid.Parse("ffbdd214-94e0-467c-b2ec-f220b5788c2b");

            var textureMaterial = _materials.Create("basic", basicVertGuid, basicFragGuid);
            textureMaterial?.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));

            _materials.Create("fullscreen", fullscreenVertGuid, fullscreenFragGuid);
            _materials.Create("screen", screenVertGuid, screenFragGuid);
        }

        private void ConfigureDefaultPostProcessing()
        {
            if (PostProcessing != null)
            {
                PostProcessing.Enabled = true;

                PostProcessing.ColorCorrectionEnabled = true;
                PostProcessing.Brightness = 0.0f;
                PostProcessing.Contrast = 1.05f;
                PostProcessing.Saturation = 1.1f;
                PostProcessing.ColorFilter = new Vector3(1.0f, 1.0f, 1.0f);

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
            }

            _fullscreenQuad?.GetSettings().Load($"{AssetManager.BasePath}VFXData.json");

        }

        public void CreateWindow(int width, int height, string title)
        {
            _window = new GameWindowInternal(width, height, title, this);

            _inputSystem = new InputSystem(_window);
        }

        public void Run()
        {
            _window?.Run();
        }

        public KeyboardState GetKeyboardState() => _window?.KeyboardState ?? default;
        public MouseState GetMouseState() => _window?.MouseState ?? default;
        public SceneRenderer GetSceneRenderer() => _sceneRenderer;
        public GameWindow? GetWindow() => _window;

        public FrameBuffer GetSceneFrameBuffer()
        {
            if (_fullscreenQuad?.GetSettings().Enabled == false && _sceneFrameBuffer != null)
                return _sceneFrameBuffer;

            if (_postProcessFrameBuffer == null)
                _postProcessFrameBuffer = new FrameBuffer(1920, 1080, false, false);

            return _postProcessFrameBuffer;
        }

        public void ResizeSceneFrameBuffer(int width, int height)
        {
            _sceneFrameBuffer?.Resize(width, height);
            _postProcessFrameBuffer?.Resize(width, height);
        }

        public void OnTextInput(TextInputEventArgs e) => TextInputEvent?.Invoke(e);

        public void OnFileDrop(FileDropEventArgs e)
        {
            if (e.FileNames != null && e.FileNames.Length > 0)
            {
                Console.WriteLine($"Files dropped: {string.Join(", ", e.FileNames)}");
                FileDropEvent?.Invoke(e.FileNames);
            }
        }

        public void InternalLoad()
        {
            AssetManager.Initialize();

            if (SceneManager.ActiveScene == null)
            {
                SceneManager.CreateScene("DefaultScene");
                SceneManager.LoadScene("DefaultScene");
            }

            _materials.LoadMaterialsData();
            CreateDefaultMaterials();

            _sceneFrameBuffer = new FrameBuffer(1280, 720, true, true);
            _postProcessFrameBuffer = new FrameBuffer(1280, 720, false, false);
            _sceneRenderer.Initialize();

            _fullscreenQuad = new FullscreenQuad();
            var fullscreenMaterial = _materials.Get("fullscreen");
            if (fullscreenMaterial != null)
                _fullscreenQuad.SetMaterial(fullscreenMaterial);

            _screenQuad = new ScreenQuad();
            var screenMaterial = _materials.Get("screen");
            if (screenMaterial != null)
                _screenQuad.SetMaterial(screenMaterial);

            ConfigureDefaultPostProcessing();
            PostProcessing?.Load(AssetManager.VFXPath);

            if (AppInfo.IsCompiledGame)
                CSharpScriptManager.Instance.Reload();

            LoadEvent?.Invoke();
        }

        public void InternalUpdate(float deltaTime)
        {
            _totalTime += deltaTime;

            TimerData.DeltaTime = deltaTime;

            SceneManager.Update(deltaTime);
            _sceneRenderer.Update(deltaTime);
            UpdateEvent?.Invoke(deltaTime);

            _inputSystem?.ClearFrameData();
        }

        public void InternalRender(float deltaTime)
        {
            int finalTexture = 0;

            if (_sceneFrameBuffer != null && _postProcessFrameBuffer != null && _fullscreenQuad != null)
            {
                _sceneFrameBuffer.Bind();
                GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _sceneRenderer.Render();
                _sceneFrameBuffer.Unbind();

                var ppSettings = _fullscreenQuad.GetSettings();

                if (ppSettings.Enabled)
                {
                    _postProcessFrameBuffer.Bind();
                    GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    var camera = _sceneRenderer.GetCamera();
                    var projection = camera != null ? camera.GetProjectionMatrix() : Matrix4.Identity;
                    var view = camera != null ? camera.GetViewMatrix() : Matrix4.Identity;

                    _fullscreenQuad.Render(
                        _sceneFrameBuffer.ColorTexture,
                        _sceneFrameBuffer.EmissionTexture,
                        _sceneFrameBuffer.PositionTexture,
                        _sceneFrameBuffer.NormalTexture,
                        _totalTime,
                        _sceneFrameBuffer.Width,
                        _sceneFrameBuffer.Height,
                        projection,
                        view
                    );
                    _postProcessFrameBuffer.Unbind();

                    finalTexture = _postProcessFrameBuffer.ColorTexture;
                }
                else
                {
                    finalTexture = _sceneFrameBuffer.ColorTexture;
                }
            }

            if (_window != null && _screenQuad != null && finalTexture != 0)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
                GL.ClearColor(0.5f, 0.5f, 0.5f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _screenQuad.Render(finalTexture);
            }

            SceneManager.Render();
            RenderEvent?.Invoke(deltaTime);
        }

        public void InternalResize(int width, int height)
        {
            _sceneRenderer.Resize(width, height);
            ResizeEvent?.Invoke(width, height);
        }

        public void InternalClose()
        {
            SceneManager.ActiveScene?.OnUnload();

            _fullscreenQuad?.Dispose();
            _screenQuad?.Dispose();
            _sceneFrameBuffer?.Dispose();
            _postProcessFrameBuffer?.Dispose();
            _sceneRenderer.Shutdown();
            CloseEvent?.Invoke();
        }
    }
}