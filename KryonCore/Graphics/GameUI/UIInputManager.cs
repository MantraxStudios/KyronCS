using KrayonCore.Graphics.GameUI;
using OpenTK.Mathematics;

namespace KrayonCore.UI
{
    /// <summary>
    /// Distribuye el input del mouse a todos los elementos interactivos de un canvas.
    ///
    /// Modo A — coordenadas de ventana directas (juego compilado):
    ///   UIInputManager.Instance.SetMousePos(e.X, e.Y);
    ///   UIInputManager.Instance.SetLeftButton(true/false);
    ///
    /// Modo B — coordenadas relativas al viewport del editor (ImGui):
    ///   UIInputManager.Instance.SetMousePosFromViewport(
    ///       new Vector2(ImGui.GetMousePos().X, ImGui.GetMousePos().Y),
    ///       new Vector2(cursorPos.X,  cursorPos.Y),
    ///       new Vector2(viewportSize.X, viewportSize.Y));
    ///
    /// Cada frame:
    ///   UIInputManager.Instance.UpdateAll(sceneRenderer.UI);
    /// </summary>
    public sealed class UIInputManager
    {
        public static UIInputManager Instance { get; } = new();

        // ── Estado ────────────────────────────────────────────────────────
        public Vector2 MouseScreenPos { get; private set; }
        public bool LeftDown { get; private set; }

        // ── Indica si el mouse está dentro del viewport activo ────────────
        public bool IsInsideViewport { get; private set; } = true;

        // ── Setters modo A (ventana completa) ─────────────────────────────
        public void SetMousePos(float x, float y)
        {
            MouseScreenPos = new Vector2(x, y);
            IsInsideViewport = true;
        }

        public void SetLeftButton(bool down) => LeftDown = down;

        // ── Setter modo B (viewport de editor / ImGui) ────────────────────
        /// <summary>
        /// Calcula la posición del mouse relativa al viewport del framebuffer,
        /// igual que hace el editor para el raycast.
        ///
        /// <param name="globalMousePos">ImGui.GetMousePos()</param>
        /// <param name="viewportOrigin">cursorPos de ImGui (esquina top-left del viewport)</param>
        /// <param name="viewportSize">tamaño del viewport en pantalla</param>
        /// </summary>
        public void SetMousePosFromViewport(
            Vector2 globalMousePos,
            Vector2 viewportOrigin,
            Vector2 viewportSize)
        {
            float relX = globalMousePos.X - viewportOrigin.X;
            float relY = globalMousePos.Y - viewportOrigin.Y;

            // Guardar si está dentro del viewport
            IsInsideViewport = relX >= 0 && relX <= viewportSize.X
                             && relY >= 0 && relY <= viewportSize.Y;

            MouseScreenPos = new Vector2(relX, relY);
        }

        // ── Distribución ──────────────────────────────────────────────────

        /// <summary>
        /// Distribuye el input a todos los UIButton y UISlider del canvas.
        /// Si el mouse está fuera del viewport no envía eventos.
        /// </summary>
        public void Update(UICanvas canvas)
        {
            // Si estamos en modo editor y el mouse salió del viewport,
            // liberar todos los controles sin disparar clicks
            Vector2 refPos = IsInsideViewport
                ? canvas.ScreenToReference(MouseScreenPos)
                : new Vector2(-9999f, -9999f);   // fuera de cualquier elemento

            bool effectiveLeft = IsInsideViewport && LeftDown;

            foreach (var e in canvas.Elements)
            {
                if (!e.Visible || !e.Enabled) continue;

                switch (e)
                {
                    case UIButton btn:
                        btn.UpdateInput(refPos, effectiveLeft);
                        break;
                    case UISlider sld:
                        sld.UpdateInput(refPos, effectiveLeft);
                        break;
                }
            }
        }

        /// <summary>Actualiza todos los canvas del manager.</summary>
        public void UpdateAll(UICanvasManager manager)
        {
            foreach (var canvas in manager.All())
                if (canvas.Visible) Update(canvas);
        }
    }
}