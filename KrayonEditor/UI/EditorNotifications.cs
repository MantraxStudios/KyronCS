using ImGuiNET;
using System.Numerics;

namespace KrayonEditor.UI
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public static class EditorNotifications
    {
        // ── Config ────────────────────────────────────────────────────────────
        private const float DISPLAY_TIME = 10.0f;
        private const float FADE_TIME = 0.4f;
        private const float NOTIFICATION_WIDTH = 420f;
        private const float NOTIFICATION_HEIGHT = 52f;
        private const float NOTIFICATION_SPACING = 8f;
        private const float TOP_MARGIN = 12f;

        // ── Colores por tipo ──────────────────────────────────────────────────
        private static readonly Vector4 ColInfo = new(0.30f, 0.65f, 1.00f, 1.00f);
        private static readonly Vector4 ColSuccess = new(0.25f, 0.85f, 0.45f, 1.00f);
        private static readonly Vector4 ColWarning = new(1.00f, 0.75f, 0.20f, 1.00f);
        private static readonly Vector4 ColError = new(0.90f, 0.30f, 0.30f, 1.00f);
        private static readonly Vector4 ColBg = new(0.10f, 0.11f, 0.14f, 0.95f);
        private static readonly Vector4 ColBgHover = new(0.15f, 0.16f, 0.20f, 0.98f);
        private static readonly Vector4 ColBorder = new(0.22f, 0.24f, 0.30f, 1.00f);

        // ── Iconos por tipo ───────────────────────────────────────────────────
        private static readonly Dictionary<NotificationType, string> Icons = new()
        {
            { NotificationType.Info,    "  " },
            { NotificationType.Success, "  " },
            { NotificationType.Warning, "  " },
            { NotificationType.Error,   "  " }
        };

        // ── Notificacion interna ──────────────────────────────────────────────
        private class Notification
        {
            public string Message { get; set; } = "";
            public NotificationType Type { get; set; }
            public float TimeRemaining { get; set; }
            public float Alpha { get; set; } = 1.0f;
            public bool ShouldRemove { get; set; } = false;
        }

        private static readonly List<Notification> _notifications = new();

        // ══════════════════════════════════════════════════════════════════════
        // ── API PÚBLICA ───────────────────────────────────────────────────────
        // ══════════════════════════════════════════════════════════════════════

        public static void Show(string message, NotificationType type = NotificationType.Info)
        {
            _notifications.Add(new Notification
            {
                Message = message,
                Type = type,
                TimeRemaining = DISPLAY_TIME,
                Alpha = 0.0f  // empieza invisible → fade in
            });
        }

        public static void Info(string message) => Show(message, NotificationType.Info);
        public static void Success(string message) => Show(message, NotificationType.Success);
        public static void Warning(string message) => Show(message, NotificationType.Warning);
        public static void Error(string message) => Show(message, NotificationType.Error);

        // ── Llamar en el loop de render, DESPUÉS de ImGui.NewFrame() ─────────
        public static void Draw(float deltaTime)
        {
            if (_notifications.Count == 0) return;

            var viewport = ImGui.GetMainViewport();
            float centerX = viewport.Pos.X + viewport.Size.X * 0.5f;
            float startY = viewport.Pos.Y + TOP_MARGIN;

            // Actualizar timers y dibujar de arriba hacia abajo
            for (int i = _notifications.Count - 1; i >= 0; i--)
            {
                var n = _notifications[i];
                if (n.ShouldRemove) continue;

                n.TimeRemaining -= deltaTime;

                // Fade in
                if (n.Alpha < 1.0f)
                    n.Alpha = MathF.Min(1.0f, n.Alpha + deltaTime / FADE_TIME);

                // Fade out en los últimos FADE_TIME segundos
                if (n.TimeRemaining <= FADE_TIME)
                    n.Alpha = MathF.Max(0.0f, n.TimeRemaining / FADE_TIME);

                if (n.TimeRemaining <= 0f)
                {
                    n.ShouldRemove = true;
                    continue;
                }
            }

            // Limpiar las que hay que eliminar
            _notifications.RemoveAll(n => n.ShouldRemove);

            // Dibujar
            var drawList = ImGui.GetForegroundDrawList(viewport);
            float currentY = startY;

            for (int i = 0; i < _notifications.Count; i++)
            {
                var n = _notifications[i];
                float alpha = n.Alpha;

                Vector4 accentColor = GetColor(n.Type);
                Vector2 notifPos = new(centerX - NOTIFICATION_WIDTH * 0.5f, currentY);
                Vector2 notifMax = new(notifPos.X + NOTIFICATION_WIDTH, notifPos.Y + NOTIFICATION_HEIGHT);
                Vector2 mousePos = ImGui.GetMousePos();

                bool isHovered = mousePos.X >= notifPos.X && mousePos.X <= notifMax.X &&
                                 mousePos.Y >= notifPos.Y && mousePos.Y <= notifMax.Y;

                // Si hace click, desaparece
                if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    n.ShouldRemove = true;
                    continue;
                }

                Vector4 bgColor = isHovered ? ColBgHover : ColBg;

                // ── Fondo ─────────────────────────────────────────────────────
                drawList.AddRectFilled(
                    notifPos, notifMax,
                    ColorU32(bgColor, alpha),
                    8f
                );

                // ── Borde izquierdo de color (acento) ─────────────────────────
                drawList.AddRectFilled(
                    notifPos,
                    new Vector2(notifPos.X + 4f, notifMax.Y),
                    ColorU32(accentColor, alpha),
                    8f
                );

                // ── Borde exterior ────────────────────────────────────────────
                drawList.AddRect(
                    notifPos, notifMax,
                    ColorU32(ColBorder, alpha * 0.8f),
                    8f, ImDrawFlags.None, 1f
                );

                // ── Icono ─────────────────────────────────────────────────────
                float iconX = notifPos.X + 14f;
                float textY = notifPos.Y + (NOTIFICATION_HEIGHT - ImGui.GetFontSize()) * 0.5f;

                drawList.AddText(
                    new Vector2(iconX, textY),
                    ColorU32(accentColor, alpha),
                    Icons[n.Type]
                );

                // ── Mensaje ───────────────────────────────────────────────────
                float msgX = iconX + 28f;
                float maxTextWidth = NOTIFICATION_WIDTH - msgX + notifPos.X - 16f;

                string displayMsg = TruncateText(n.Message, maxTextWidth);

                drawList.AddText(
                    new Vector2(msgX, textY),
                    ColorU32(new Vector4(0.90f, 0.92f, 0.95f, 1f), alpha),
                    displayMsg
                );

                // ── Barra de progreso (tiempo restante) ───────────────────────
                float progress = Math.Clamp(n.TimeRemaining / DISPLAY_TIME, 0f, 1f);
                float barHeight = 3f;
                float barY = notifMax.Y - barHeight;

                drawList.AddRectFilled(
                    new Vector2(notifPos.X, barY),
                    new Vector2(notifPos.X + NOTIFICATION_WIDTH * progress, notifMax.Y),
                    ColorU32(accentColor, alpha * 0.5f),
                    0f
                );

                // ── X de cierre (hover) ───────────────────────────────────────
                if (isHovered)
                {
                    float closeX = notifMax.X - 20f;
                    float closeY = notifPos.Y + (NOTIFICATION_HEIGHT - ImGui.GetFontSize()) * 0.5f;
                    drawList.AddText(
                        new Vector2(closeX, closeY),
                        ColorU32(new Vector4(0.6f, 0.6f, 0.65f, 1f), alpha),
                        "x"
                    );
                }

                currentY += NOTIFICATION_HEIGHT + NOTIFICATION_SPACING;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static Vector4 GetColor(NotificationType type) => type switch
        {
            NotificationType.Success => ColSuccess,
            NotificationType.Warning => ColWarning,
            NotificationType.Error => ColError,
            _ => ColInfo
        };

        private static uint ColorU32(Vector4 color, float alpha)
        {
            return ImGui.GetColorU32(color with { W = color.W * alpha });
        }

        private static string TruncateText(string text, float maxWidth)
        {
            var textSize = ImGui.CalcTextSize(text);
            if (textSize.X <= maxWidth) return text;

            string truncated = text;
            while (truncated.Length > 0 && ImGui.CalcTextSize(truncated + "...").X > maxWidth)
                truncated = truncated[..^1];

            return truncated + "...";
        }
    }
}