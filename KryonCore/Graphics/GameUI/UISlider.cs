using OpenTK.Mathematics;
using SDColor = System.Drawing.Color;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Horizontal slider with a pill-shaped track, smooth fill, and a circular thumb —
    /// styled after Unreal Engine's compact dark UI.
    ///
    /// Usage:
    ///   canvas.Add(new UISlider
    ///   {
    ///       Position = new Vector2(100, 300),
    ///       Size     = new Vector2(300, 6),
    ///       Min      = 0f, Max = 100f, Value = 50f,
    ///       OnChange = v => volume = v,
    ///   });
    ///
    ///   // Each frame:
    ///   slider.UpdateInput(canvas.ScreenToReference(mouseScreenPos), leftButtonDown);
    /// </summary>
    public sealed class UISlider : UIElement
    {
        // ── Range ─────────────────────────────────────────────────────────
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 1f;

        private float _value = 0.5f;
        public float Value
        {
            get => _value;
            set
            {
                float c = Math.Clamp(value, Min, Max);
                if (MathF.Abs(_value - c) > 0.00001f)
                {
                    _value = c;
                    OnChange?.Invoke(_value);
                }
            }
        }

        /// <summary>Normalized value in [0, 1].</summary>
        public float NormalizedValue
        {
            get => (Max - Min) < 0.0001f ? 0f : (_value - Min) / (Max - Min);
            set => Value = Min + Math.Clamp(value, 0f, 1f) * (Max - Min);
        }

        // ── UE-style palette ──────────────────────────────────────────────
        public Vector4 TrackColor      { get; set; } = new(0.14f, 0.14f, 0.14f, 1f);
        public Vector4 TrackBorderColor{ get; set; } = new(0.45f, 0.45f, 0.45f, 1f);
        public Vector4 FillColorA      { get; set; } = new(0.18f, 0.52f, 0.90f, 1f);  // bright blue
        public Vector4 FillColorB      { get; set; } = new(0.12f, 0.40f, 0.75f, 1f);  // slightly darker
        public Vector4 ThumbColor      { get; set; } = new(0.92f, 0.92f, 0.92f, 1f);
        public Vector4 ThumbHoverColor { get; set; } = new(1.00f, 1.00f, 1.00f, 1f);
        public Vector4 ThumbShadow     { get; set; } = new(0.00f, 0.00f, 0.00f, 0.50f);
        public Vector4 ThumbBorder     { get; set; } = new(0.55f, 0.55f, 0.55f, 1f);

        // ── Geometry ──────────────────────────────────────────────────────
        /// <summary>Thumb radius in reference pixels. The thumb renders as a circle.</summary>
        public float ThumbRadius { get; set; } = 9f;
        /// <summary>Border thickness around the track in reference pixels. 0 = no border.</summary>
        public float TrackBorder { get; set; } = 1f;

        // ── Value label ───────────────────────────────────────────────────
        public bool    ShowValue     { get; set; } = true;
        public string  ValueFormat   { get; set; } = "F0";
        public float   LabelFontSize { get; set; } = 14f;
        public string  LabelFontName { get; set; } = "Segoe UI";
        public SDColor LabelColor    { get; set; } = SDColor.FromArgb(255, 200, 200, 200);

        // ── Event ─────────────────────────────────────────────────────────
        public Action<float>? OnChange { get; set; }

        // ── State ─────────────────────────────────────────────────────────
        public bool IsDragging { get; private set; }
        public bool IsHovered  { get; private set; }

        // ── Internals ─────────────────────────────────────────────────────
        private UILabel? _valueLabel;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void Initialize()
        {
            _valueLabel = new UILabel
            {
                FontName  = LabelFontName,
                FontSize  = LabelFontSize,
                TextColor = LabelColor,
                AutoSize  = true,
                Color     = Vector4.One,
            };
            _valueLabel.Initialize();
        }

        public override void Update(float deltaTime)
        {
            if (_valueLabel is null) return;
            _valueLabel.FontName  = LabelFontName;
            _valueLabel.FontSize  = LabelFontSize;
            _valueLabel.TextColor = LabelColor;
            _valueLabel.Text      = Value.ToString(ValueFormat);
            _valueLabel.Update(deltaTime);
        }

        public override void Draw(UIBatch batch)
        {
            if (!Visible) return;

            float trackH  = Size.Y;
            float trackR  = trackH * 0.5f;           // full pill radius
            float norm    = NormalizedValue;
            float fillW   = Size.X * norm;

            // ── Track border ─────────────────────────────────────────────
            if (TrackBorder > 0f)
            {
                batch.DrawRoundedRect(
                    Position - new Vector2(TrackBorder),
                    Size     + new Vector2(TrackBorder * 2f),
                    TrackBorderColor,
                    trackR + TrackBorder);
            }

            // ── Track background (pill shape) ─────────────────────────────
            batch.DrawRoundedRect(Position, Size, TrackColor, trackR);

            // ── Filled portion (gradient) ─────────────────────────────────
            if (fillW > 1f)
            {
                float fillR = MathF.Min(trackR, fillW * 0.5f);
                batch.DrawRoundedRectGradient(
                    Position,
                    new Vector2(fillW, trackH),
                    FillColorA, FillColorB,
                    fillR);
            }

            // ── Thumb ─────────────────────────────────────────────────────
            float thumbX   = Position.X + Size.X * norm;
            float thumbY   = Position.Y + trackH * 0.5f;
            float td       = ThumbRadius * 2f;
            float thumbR   = ThumbRadius;            // circle = radius == half-size
            Vector2 thumbPos = new(thumbX - ThumbRadius, thumbY - ThumbRadius);
            Vector2 thumbSz  = new(td, td);

            // Shadow beneath thumb
            if (ThumbShadow.W > 0f)
            {
                batch.DrawRoundedRect(
                    thumbPos + new Vector2(0f, 1.5f),
                    thumbSz  + new Vector2(2f),
                    ThumbShadow,
                    thumbR + 1f);
            }

            // Thumb border ring
            batch.DrawRoundedRect(
                thumbPos - new Vector2(1f),
                thumbSz  + new Vector2(2f),
                ThumbBorder,
                thumbR + 1f);

            // Thumb fill
            Vector4 thumbCol = (IsHovered || IsDragging) ? ThumbHoverColor : ThumbColor;
            batch.DrawRoundedRect(thumbPos, thumbSz, thumbCol, thumbR);

            // ── Value label (right of slider) ─────────────────────────────
            if (ShowValue && _valueLabel is not null)
            {
                _valueLabel.Update(0f);
                float ly = Position.Y + (trackH - _valueLabel.Size.Y) * 0.5f;
                _valueLabel.Position = new Vector2(
                    Position.X + Size.X + ThumbRadius + 8f, ly);
                _valueLabel.Draw(batch);
            }
        }

        public override void Dispose()
        {
            _valueLabel?.Dispose();
            _valueLabel = null;
        }

        // ── Input ─────────────────────────────────────────────────────────

        /// <summary>Call every frame with the mouse in reference coords (canvas.ScreenToReference).</summary>
        public void UpdateInput(Vector2 refMousePos, bool leftDown)
        {
            // Extended hit area includes the thumb
            float ex = ThumbRadius;
            bool hit = refMousePos.X >= Position.X - ex
                    && refMousePos.X <= Position.X + Size.X + ex
                    && refMousePos.Y >= Position.Y - ex
                    && refMousePos.Y <= Position.Y + Size.Y + ex;

            IsHovered = hit;

            if (hit && leftDown)  IsDragging = true;
            if (!leftDown)        IsDragging = false;

            if (IsDragging)
            {
                float t = (refMousePos.X - Position.X) / MathF.Max(Size.X, 0.001f);
                NormalizedValue = t;
            }
        }
    }
}
