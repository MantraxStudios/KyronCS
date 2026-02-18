using KrayonCore.Graphics.GameUI;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Screen-space canvas con preservación de aspect ratio.
    /// Se adapta automáticamente al viewport real en cada frame —
    /// no es necesario llamar a Resize() manualmente (aunque se puede).
    ///
    /// Quick start:
    ///   sceneRenderer.CreateCanvas("hud")
    ///       .Add(new UILabel { Text = "Hello World!", Position = new(20, 20) });
    /// </summary>
    public sealed class UICanvas : IDisposable
    {
        // ── Identity ──────────────────────────────────────────────────────
        public string Name { get; }
        public int SortOrder { get; set; }
        public bool Visible { get; set; } = true;

        // ── Reference resolution ──────────────────────────────────────────
        public float ReferenceWidth { get; private set; } = 1920f;
        public float ReferenceHeight { get; private set; } = 1080f;

        /// <summary>
        /// Fit    = escala uniforme, barras si el aspect difiere (default)
        /// Fill   = escala uniforme, bordes se recortan
        /// Stretch = rellena exacto (puede deformar)
        /// </summary>
        public UIScaleMode ScaleMode { get; set; } = UIScaleMode.Fit;

        // ── Computed (leído desde GL cada frame) ──────────────────────────
        public float Scale { get; private set; } = 1f;
        public float OffsetX { get; private set; } = 0f;
        public float OffsetY { get; private set; } = 0f;
        public float ScaledW { get; private set; }
        public float ScaledH { get; private set; }

        // ── Elements ──────────────────────────────────────────────────────
        private readonly List<UIElement> _elements = new();
        public IReadOnlyList<UIElement> Elements => _elements;

        // ── GL ────────────────────────────────────────────────────────────
        private UIBatch? _batch;
        private bool _batchReady;

        // ── Attachment ────────────────────────────────────────────────────
        private SceneRenderer? _renderer;
        private string _attachKey = string.Empty;

        // ── Ctor ──────────────────────────────────────────────────────────
        public UICanvas(string name = "canvas", int sortOrder = 0)
        {
            Name = name;
            SortOrder = sortOrder;
            ScaledW = ReferenceWidth;
            ScaledH = ReferenceHeight;
        }

        // ── Reference resolution ──────────────────────────────────────────
        public UICanvas SetReferenceResolution(float width, float height)
        {
            ReferenceWidth = width;
            ReferenceHeight = height;
            _batch?.SetReferenceSize(width, height);
            return this;
        }

        // ── Element management ────────────────────────────────────────────
        public UICanvas Add(UIElement element)
        {
            ArgumentNullException.ThrowIfNull(element);
            EnsureBatch();
            element.Initialize();
            _elements.Add(element);
            SortElements();
            return this;
        }

        public bool Remove(UIElement element)
        {
            if (!_elements.Remove(element)) return false;
            element.Dispose();
            return true;
        }

        public UICanvas Clear()
        {
            foreach (var e in _elements) e.Dispose();
            _elements.Clear();
            return this;
        }

        public UIElement? Find(string name) => _elements.Find(e => e.Name == name);
        public T? Find<T>(string n) where T : UIElement
            => _elements.OfType<T>().FirstOrDefault(e => e.Name == n);
        public IEnumerable<T> GetAll<T>() where T : UIElement => _elements.OfType<T>();

        // ── SceneRenderer integration ─────────────────────────────────────
        public UICanvas Attach(SceneRenderer renderer)
        {
            Detach();
            _renderer = renderer;
            _attachKey = $"__UICanvas__{SortOrder:D6}_{Name}";
            renderer.AttachRender(_attachKey, (_, _, _) => RenderInternal());
            return this;
        }

        public UICanvas Detach()
        {
            if (_renderer is not null && !string.IsNullOrEmpty(_attachKey))
                _renderer.DetachRender(_attachKey);
            _renderer = null;
            _attachKey = string.Empty;
            return this;
        }

        // ── Frame ─────────────────────────────────────────────────────────
        public void Update(float deltaTime)
        {
            if (!Visible) return;
            foreach (var e in _elements)
                if (e.Visible) e.Update(deltaTime);
        }

        public void Render()
        {
            if (!Visible) return;
            RenderInternal();
        }

        // ── Coordinate helpers ────────────────────────────────────────────
        public Vector2 ScreenToReference(Vector2 screenPt)
            => new((screenPt.X - OffsetX) / Scale, (screenPt.Y - OffsetY) / Scale);

        public Vector2 ReferenceToScreen(Vector2 refPt)
            => new(refPt.X * Scale + OffsetX, refPt.Y * Scale + OffsetY);

        // ── IDisposable ───────────────────────────────────────────────────
        public void Dispose()
        {
            Detach();
            Clear();
            _batch?.Dispose();
            _batch = null;
        }

        // ── Private ───────────────────────────────────────────────────────

        private void RenderInternal()
        {
            if (!Visible) return;
            EnsureBatch();

            // ── Leer el viewport actual que estableció la cámara ──────────
            // prevVP = [x, y, width, height]  (coordenadas GL, origen bottom-left)
            int[] prevVP = new int[4];
            GL.GetInteger(GetPName.Viewport, prevVP);

            int camX = prevVP[0];
            int camY = prevVP[1];
            int camW = prevVP[2];
            int camH = prevVP[3];

            if (camW <= 0 || camH <= 0) return;

            // ── Calcular escala uniforme dentro del viewport de la cámara ─
            float scaleX = camW / ReferenceWidth;
            float scaleY = camH / ReferenceHeight;

            Scale = ScaleMode switch
            {
                UIScaleMode.Fit => MathF.Min(scaleX, scaleY),
                UIScaleMode.Fill => MathF.Max(scaleX, scaleY),
                UIScaleMode.Stretch => 1f,
                _ => MathF.Min(scaleX, scaleY),
            };

            if (ScaleMode == UIScaleMode.Stretch)
            {
                ScaledW = camW;
                ScaledH = camH;
                OffsetX = 0f;
                OffsetY = 0f;
            }
            else
            {
                ScaledW = ReferenceWidth * Scale;
                ScaledH = ReferenceHeight * Scale;
                // Centrar dentro del viewport de la cámara
                OffsetX = (camW - ScaledW) * 0.5f;
                OffsetY = (camH - ScaledH) * 0.5f;
            }

            // ── Viewport del canvas en coordenadas GL (origen bottom-left) ─
            int vpX = camX + (int)OffsetX;
            int vpY = camY + (int)OffsetY;
            int vpW = (int)ScaledW;
            int vpH = (int)ScaledH;

            // ── Guardar GL state ──────────────────────────────────────────
            bool depthTest = GL.IsEnabled(EnableCap.DepthTest);
            bool cullFace = GL.IsEnabled(EnableCap.CullFace);
            bool blend = GL.IsEnabled(EnableCap.Blend);
            bool scissor = GL.IsEnabled(EnableCap.ScissorTest);

            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.Viewport(vpX, vpY, vpW, vpH);
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(vpX, vpY, vpW, vpH);

            // ── Dibujar elementos ─────────────────────────────────────────
            foreach (var e in _elements)
            {
                if (!e.Visible) continue;
                e.Update(0f);
                e.Draw(_batch!);
            }

            // ── Restaurar GL state ────────────────────────────────────────
            GL.Viewport(camX, camY, camW, camH);
            SetEnabled(EnableCap.ScissorTest, scissor);
            SetEnabled(EnableCap.DepthTest, depthTest);
            SetEnabled(EnableCap.CullFace, cullFace);
            SetEnabled(EnableCap.Blend, blend);
        }

        private void EnsureBatch()
        {
            if (_batchReady) return;
            _batch = new UIBatch();
            _batch.SetReferenceSize(ReferenceWidth, ReferenceHeight);
            _batchReady = true;
        }

        private void SortElements()
            => _elements.Sort((a, b) => a.ZOrder.CompareTo(b.ZOrder));

        private static void SetEnabled(EnableCap cap, bool on)
        {
            if (on) GL.Enable(cap); else GL.Disable(cap);
        }
    }

    public enum UIScaleMode
    {
        /// <summary>Escala uniforme, barras si el aspect difiere (default).</summary>
        Fit,
        /// <summary>Escala uniforme, bordes pueden recortarse.</summary>
        Fill,
        /// <summary>Rellena la pantalla exacto (puede deformar).</summary>
        Stretch,
    }
}