using Assimp;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;
using KrayonCore.Core.Attributes;
using KrayonCore.Core;
using KrayonCore.Core.Rendering;

namespace KrayonCore
{
    public class GraphicsEngine
    {
        private GameWindowInternal? _window;
        private SceneRenderer _sceneRenderer;
        private FrameBuffer? _sceneFrameBuffer;
        private FrameBuffer? _postProcessFrameBuffer;
        public FullscreenQuad? _fullscreenQuad;
        
        // AÑADIR ESTA VARIABLE
        private float _totalTime = 0.0f;

        public event Action? LoadEvent;
        public event Action<float>? UpdateEvent;
        public event Action<float>? RenderEvent;
        public event Action<int, int>? ResizeEvent;
        public event Action? CloseEvent;

        public event Action<TextInputEventArgs>? TextInputEvent;
        public static GraphicsEngine? Instance { get; private set; }

        private MaterialManager _materials;
        public MaterialManager Materials => _materials;

        public PostProcessingSettings? PostProcessing => _fullscreenQuad?.GetSettings();

        public GraphicsEngine()
        {
            Instance = this;

            _materials = new MaterialManager();
            _sceneRenderer = new SceneRenderer();
        }

        private void CreateDefaultMaterials()
        {
            var textureMaterial = _materials.Create("basic", "shaders/basic");
            textureMaterial?.SetVector3Cached("u_Color", new Vector3(1.0f, 1.0f, 1.0f));

            if (textureMaterial != null)
            {
                textureMaterial.LoadMainTexture("textures/sprites/Environment/dirt.png", 
                                               generateMipmaps: true, flip: true);
            }

            _materials.Create("fullscreen", "shaders/fullscreen");
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

            //_fullscreenQuad.GetSettings().Load($"{AssetManager.BasePath}VFXData.json");
        }

        public void CreateWindow(int width, int height, string title)
        {
            _window = new GameWindowInternal(width, height, title, this);
        }

        public void Run()
        {
            _window?.Run();
        }

        public KeyboardState GetKeyboardState() => _window?.KeyboardState ?? default;
        public MouseState GetMouseState() => _window?.MouseState ?? default;
        public SceneRenderer GetSceneRenderer() => _sceneRenderer;

        public GameWindow GetWindow() => _window;

        public FrameBuffer GetSceneFrameBuffer()
        {
            if (_fullscreenQuad?.GetSettings().Enabled == false && _sceneFrameBuffer != null)
            {
                return _sceneFrameBuffer;
            }
            
            if (_postProcessFrameBuffer == null)
            {
                _postProcessFrameBuffer = new FrameBuffer(1280, 720, false, false);
            }
            return _postProcessFrameBuffer;
        }

        public void ResizeSceneFrameBuffer(int width, int height)
        {
            if (_sceneFrameBuffer != null)
            {
                _sceneFrameBuffer.Resize(width, height);
            }
            if (_postProcessFrameBuffer != null)
            {
                _postProcessFrameBuffer.Resize(width, height);
            }
        }

        public void OnTextInput(TextInputEventArgs e)
        {
            TextInputEvent?.Invoke(e);
        }

        public void InternalLoad()
        {
            AssetManager.Initialize();

            if (SceneManager.ActiveScene == null)
            {
                var defaultScene = SceneManager.CreateScene("DefaultScene");
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
            {
                _fullscreenQuad.SetMaterial(fullscreenMaterial);
            }

            ConfigureDefaultPostProcessing();

            PostProcessing.Load($"{AssetManager.BasePath}VFXData.json");

            LoadEvent?.Invoke();
        }

        public void InternalUpdate(float deltaTime)
        {
            // ACUMULAR EL TIEMPO TOTAL
            _totalTime += deltaTime;
            
            SceneManager.Update(deltaTime);
            _sceneRenderer.Update(deltaTime);
            UpdateEvent?.Invoke(deltaTime);
        }

        public void InternalRender(float deltaTime)
        {
            if (_sceneFrameBuffer != null && _postProcessFrameBuffer != null && _fullscreenQuad != null)
            {
                _sceneFrameBuffer.Bind();
                GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                _sceneRenderer.Render();
                _sceneFrameBuffer.Unbind();

                var ppSettings = _fullscreenQuad.GetSettings();
                if (ppSettings.Enabled)
                {
                    _postProcessFrameBuffer.Bind();
                    GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                    
                    var camera = _sceneRenderer.GetCamera();
                    var projection = camera != null ? camera.GetProjectionMatrix() : Matrix4.Identity;
                    
                    // CAMBIAR deltaTime POR _totalTime
                    _fullscreenQuad.Render(
                        _sceneFrameBuffer.ColorTexture, 
                        _sceneFrameBuffer.EmissionTexture,
                        _sceneFrameBuffer.PositionTexture,
                        _sceneFrameBuffer.NormalTexture,
                        _totalTime,  // <-- USAR TIEMPO ACUMULADO
                        _sceneFrameBuffer.Width, 
                        _sceneFrameBuffer.Height,
                        projection
                    );
                    _postProcessFrameBuffer.Unbind();
                }
            }

            if (_window != null)
            {
                GL.Viewport(0, 0, _window.ClientSize.X, _window.ClientSize.Y);
                GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            }

            RenderEvent?.Invoke(deltaTime);
        }

        public void InternalResize(int width, int height)
        {
            _sceneRenderer.Resize(width, height);
            ResizeEvent?.Invoke(width, height);
        }

        public void InternalClose()
        {
            if (SceneManager.ActiveScene != null)
            {
                SceneManager.ActiveScene.OnUnload();
            }

            _fullscreenQuad?.Dispose();
            _sceneFrameBuffer?.Dispose();
            _postProcessFrameBuffer?.Dispose();
            _sceneRenderer.Shutdown();
            CloseEvent?.Invoke();
        }
    }
}