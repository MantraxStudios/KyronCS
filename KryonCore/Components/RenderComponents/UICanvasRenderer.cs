using KrayonCore.Core;
using KrayonCore.Core.Attributes;
using KrayonCore.Graphics.GameUI;
using KrayonCore.GraphicsData;
using KrayonCore.UI;

namespace KrayonCore.Components.RenderComponents
{
    public class UICanvasRenderer : Component
    {
        private string _canvasPath = string.Empty;

        [ToStorage]
        public string CanvasPath
        {
            get => _canvasPath;
            set
            {
                if (_canvasPath == value) return;
                _canvasPath = value;
                if (_isInitialized) Reload();
            }
        }

        [ToStorage] public bool Visible { get; set; } = true;
        [ToStorage] public int SortOrder { get; set; } = 0;
        [ToStorage] public float ReferenceWidth { get; set; } = 1920f;
        [ToStorage] public float ReferenceHeight { get; set; } = 1080f;

        [NoSerializeToInspector] public UICanvas? Canvas { get; private set; }

        private bool _isInitialized = false;
        private bool _pendingLoad = false;

        public override void Awake()
        {
            _isInitialized = false;
            Canvas = null;
            if (!string.IsNullOrEmpty(_canvasPath))
                _pendingLoad = true;
        }

        public override void Start()
        {
            _isInitialized = true;
            if (_pendingLoad) { _pendingLoad = false; Reload(); }
        }

        public override void OnWillRenderObject()
        {
            if (Canvas is not null)
                Canvas.Visible = Visible && Enabled;
        }

        public override void OnDestroy() => UnloadCanvas();

        public void Reload()
        {
            UnloadCanvas();
            if (string.IsNullOrEmpty(_canvasPath)) return;

            var sceneRenderer = GraphicsEngine.Instance?.GetSceneRenderer();
            if (sceneRenderer is null)
            {
                Console.WriteLine("[UICanvasRenderer] SceneRenderer no disponible todavia.");
                _pendingLoad = true;
                return;
            }

            try
            {
                // Igual que MeshRenderer: _canvasPath almacena el GUID del asset como string
                Canvas = UILoader.Load(Guid.Parse(_canvasPath), sceneRenderer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UICanvasRenderer] Error cargando canvas '{_canvasPath}': {ex.Message}");
                Canvas = null;
                return;
            }

            if (Canvas is null)
            {
                Console.WriteLine($"[UICanvasRenderer] No se pudo cargar '{_canvasPath}'.");
                return;
            }

            Canvas.Visible = Visible && Enabled;
            Canvas.SortOrder = SortOrder;
            Canvas.SetReferenceResolution(ReferenceWidth, ReferenceHeight);
            Console.WriteLine($"[UICanvasRenderer] Canvas '{Canvas.Name}' cargado.");
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (Canvas is not null) Canvas.Visible = visible;
        }

        public T? GetElement<T>(string name) where T : UIElement => Canvas?.Find<T>(name);
        public UIElement? GetElement(string name) => Canvas?.Find(name);
        public UIButton? GetButton(string name) => GetElement<UIButton>(name);
        public UISlider? GetSlider(string name) => GetElement<UISlider>(name);
        public UIInputText? GetInputText(string name) => GetElement<UIInputText>(name);
        public UILabel? GetLabel(string name) => GetElement<UILabel>(name);
        public UIImage? GetImage(string name) => GetElement<UIImage>(name);

        public void UpdateInput()
        {
            if (Canvas is not null && Canvas.Visible)
                UIInputManager.Update(Canvas);
        }

        private void UnloadCanvas()
        {
            if (Canvas is null) return;
            string name = Canvas.Name;
            UICanvasManager.Destroy(name);
            Canvas = null;
            Console.WriteLine($"[UICanvasRenderer] Canvas '{name}' destruido.");
        }
    }
}