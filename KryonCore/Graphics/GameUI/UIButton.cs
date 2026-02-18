using OpenTK.Mathematics;
using SDColor = System.Drawing.Color;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Clickable button with Normal / Hover / Pressed states, smooth color
    /// transitions, rounded corners, gradient fill, and a drop-shadow —
    /// styled after Unreal Engine's compact dark UI.
    ///
    /// Usage:
    ///   canvas.Add(new UIButton
    ///   {
    ///       Text     = "Play",
    ///       Position = new Vector2(860, 480),
    ///       Size     = new Vector2(200, 50),
    ///       OnClick  = () => Console.WriteLine("clicked!"),
    ///   });
    ///
    ///   // Each frame, pass mouse position in reference coords:
    ///   button.UpdateInput(canvas.ScreenToReference(mouseScreenPos), leftButtonDown);
    /// </summary>
    public sealed class UIButton : UIElement
    {
        // ── Content ───────────────────────────────────────────────────────
        public string Text { get; set; } = "Button";
        public float FontSize { get; set; } = 18f;
        public string FontName { get; set; } = "Segoe UI";

        // ── UE-style dark palette ─────────────────────────────────────────

        // ── Backward-compatible solid-color aliases ────────────────────────
        /// <summary>Sets both NormalTop and NormalBottom to the same color (solid fill).</summary>
        public Vector4 NormalColor
        {
            get => NormalTop;
            set { NormalTop = value; NormalBottom = value; }
        }
        /// <summary>Sets both HoverTop and HoverBottom to the same color (solid fill).</summary>
        public Vector4 HoverColor
        {
            get => HoverTop;
            set { HoverTop = value; HoverBottom = value; }
        }
        /// <summary>Sets both PressedTop and PressedBottom to the same color (solid fill).</summary>
        public Vector4 PressedColor
        {
            get => PressedTop;
            set { PressedTop = value; PressedBottom = value; }
        }

        /// <summary>Top gradient stop for Normal state.</summary>
        public Vector4 NormalTop { get; set; } = new(0.220f, 0.220f, 0.220f, 1f);
        /// <summary>Bottom gradient stop for Normal state.</summary>
        public Vector4 NormalBottom { get; set; } = new(0.155f, 0.155f, 0.155f, 1f);

        /// <summary>Top gradient stop for Hover state.</summary>
        public Vector4 HoverTop { get; set; } = new(0.310f, 0.310f, 0.310f, 1f);
        /// <summary>Bottom gradient stop for Hover state.</summary>
        public Vector4 HoverBottom { get; set; } = new(0.230f, 0.230f, 0.230f, 1f);

        /// <summary>Top gradient stop for Pressed state.</summary>
        public Vector4 PressedTop { get; set; } = new(0.110f, 0.110f, 0.110f, 1f);
        /// <summary>Bottom gradient stop for Pressed state.</summary>
        public Vector4 PressedBottom { get; set; } = new(0.090f, 0.090f, 0.090f, 1f);

        /// <summary>Outer border (the thin line enclosing the button).</summary>
        public Vector4 BorderColor { get; set; } = new(0.520f, 0.520f, 0.520f, 1f);

        /// <summary>Top-edge highlight (simulates a subtle bevel). Set alpha=0 to disable.</summary>
        public Vector4 BevelColor { get; set; } = new(0.700f, 0.700f, 0.700f, 0.35f);

        /// <summary>Drop shadow color.</summary>
        public Vector4 ShadowColor { get; set; } = new(0f, 0f, 0f, 0.55f);

        public SDColor TextColor { get; set; } = SDColor.FromArgb(255, 230, 230, 230);

        // ── Geometry ──────────────────────────────────────────────────────
        public float CornerRadius { get; set; } = 4f;
        /// <summary>Border thickness in reference pixels. 0 = no border.</summary>
        public float BorderWidth { get; set; } = 1f;
        /// <summary>Drop-shadow offset in reference pixels.</summary>
        public Vector2 ShadowOffset { get; set; } = new(0f, 2f);
        /// <summary>How many pixels the shadow expands beyond the button edges.</summary>
        public float ShadowSpread { get; set; } = 2f;

        // ── Transition ────────────────────────────────────────────────────
        /// <summary>Higher = snappier color animation. 8 feels natural; 20+ is near-instant.</summary>
        public float TransitionSpeed { get; set; } = 10f;

        // ── Events ────────────────────────────────────────────────────────
        public Action? OnClick { get; set; }
        public Action? OnHoverEnter { get; set; }
        public Action? OnHoverExit { get; set; }

        // ── State ─────────────────────────────────────────────────────────
        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }

        // ── Internals ─────────────────────────────────────────────────────
        private UILabel? _label;
        private Vector4 _animTop;
        private Vector4 _animBot;
        private bool _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void Initialize()
        {
            _animTop = NormalTop;
            _animBot = NormalBottom;
            _initialized = true;

            _label = new UILabel
            {
                FontName = FontName,
                FontSize = FontSize,
                TextColor = TextColor,
                AutoSize = true,
                Color = Vector4.One,
            };
            _label.Initialize();
        }

        public override void Update(float deltaTime)
        {
            if (_label is null) return;

            // Sync label style
            _label.Text = Text;
            _label.FontName = FontName;
            _label.FontSize = FontSize;
            _label.TextColor = TextColor;
            _label.Update(deltaTime);

            // Smooth gradient target
            var (targetTop, targetBot) = IsPressed
                ? (PressedTop, PressedBottom)
                : IsHovered
                    ? (HoverTop, HoverBottom)
                    : (NormalTop, NormalBottom);

            float t = _initialized && deltaTime > 0f
                ? MathF.Min(1f, deltaTime * TransitionSpeed)
                : 1f;

            _animTop = Lerp(_animTop, targetTop, t);
            _animBot = Lerp(_animBot, targetBot, t);
        }

        public override void Draw(UIBatch batch)
        {
            if (!Visible || _label is null) return;

            float r = CornerRadius;
            float sp = ShadowSpread;

            // 1 — Drop shadow (slightly expanded, offset)
            if (ShadowColor.W > 0f)
            {
                batch.DrawRoundedRect(
                    Position - new Vector2(sp) + ShadowOffset,
                    Size + new Vector2(sp * 2f),
                    ShadowColor,
                    r + sp);
            }

            // 2 — Border ring (one pixel wider on all sides)
            if (BorderWidth > 0f)
            {
                batch.DrawRoundedRect(
                    Position - new Vector2(BorderWidth),
                    Size + new Vector2(BorderWidth * 2f),
                    BorderColor,
                    r + BorderWidth);
            }

            // 3 — Gradient fill
            batch.DrawRoundedRectGradient(Position, Size, _animTop, _animBot, r);

            // 4 — Top-edge bevel highlight (1 px tall strip)
            if (BevelColor.W > 0f)
            {
                batch.DrawRoundedRect(
                    Position,
                    new Vector2(Size.X, 1f),
                    BevelColor,
                    r);
            }

            // 5 — Centered label
            _label.Update(0f);
            Vector2 labelSize = _label.Size;
            _label.Position = Position + (Size - labelSize) * 0.5f;
            _label.Draw(batch);
        }

        public override void Dispose()
        {
            _label?.Dispose();
            _label = null;
        }

        // ── Input ─────────────────────────────────────────────────────────

        /// <summary>Call every frame with the mouse in reference coords (canvas.ScreenToReference).</summary>
        public void UpdateInput(Vector2 refMousePos, bool leftDown)
        {
            bool hit = HitTest(refMousePos);

            if (hit && !IsHovered) { IsHovered = true; OnHoverEnter?.Invoke(); }
            if (!hit && IsHovered) { IsHovered = false; OnHoverExit?.Invoke(); }

            if (hit && leftDown) IsPressed = true;

            if (IsPressed && !leftDown)
            {
                IsPressed = false;
                if (hit) OnClick?.Invoke();
            }

            if (!leftDown) IsPressed = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static Vector4 Lerp(Vector4 a, Vector4 b, float t)
            => new(a.X + (b.X - a.X) * t,
                   a.Y + (b.Y - a.Y) * t,
                   a.Z + (b.Z - a.Z) * t,
                   a.W + (b.W - a.W) * t);
    }
}