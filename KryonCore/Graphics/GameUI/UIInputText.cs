using OpenTK.Mathematics;
using System.Drawing;
using System.Drawing.Text;
using SDFont = System.Drawing.Font;
using SDColor = System.Drawing.Color;
using SDGraphics = System.Drawing.Graphics;

namespace KrayonCore.Graphics.GameUI
{
    public sealed class UIInputText : UIElement
    {
        // ── Content ───────────────────────────────────────────────────────
        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set
            {
                string clamped = MaxLength > 0 && value.Length > MaxLength
                    ? value[..MaxLength] : value;
                if (_text == clamped) return;
                _text = clamped;
                _cursorPos = Math.Clamp(_cursorPos, 0, _text.Length);
                _labelDirty = true;
                OnChange?.Invoke(_text);
            }
        }

        public string Placeholder { get; set; } = string.Empty;
        public int MaxLength { get; set; } = 0;
        public bool IsPassword { get; set; } = false;
        public bool IsNumericOnly { get; set; } = false;

        // ── Font ──────────────────────────────────────────────────────────
        public float FontSize { get; set; } = 18f;
        public string FontName { get; set; } = "Segoe UI";

        // ── Palette ───────────────────────────────────────────────────────
        public Vector4 NormalColor { get; set; } = new(0.14f, 0.14f, 0.14f, 1f);
        public Vector4 HoverColor { get; set; } = new(0.20f, 0.20f, 0.20f, 1f);
        public Vector4 FocusedColor { get; set; } = new(0.12f, 0.12f, 0.12f, 1f);
        public Vector4 BorderNormal { get; set; } = new(0.45f, 0.45f, 0.45f, 1f);
        public Vector4 BorderFocused { get; set; } = new(0.18f, 0.52f, 0.90f, 1f);
        public Vector4 CursorColor { get; set; } = new(0.90f, 0.90f, 0.90f, 1f);
        public SDColor TextColor { get; set; } = SDColor.FromArgb(255, 230, 230, 230);
        public SDColor PlaceholderColor { get; set; } = SDColor.FromArgb(160, 160, 160, 160);

        // ── Geometry ──────────────────────────────────────────────────────
        public float CornerRadius { get; set; } = 4f;
        public float BorderWidth { get; set; } = 1.5f;
        public float Padding { get; set; } = 8f;

        // ── Cursor ────────────────────────────────────────────────────────
        public float CursorBlinkRate { get; set; } = 0.53f;

        // ── Events ────────────────────────────────────────────────────────
        public Action<string>? OnChange { get; set; }
        public Action<string>? OnSubmit { get; set; }

        // ── State ─────────────────────────────────────────────────────────
        public bool IsFocused { get; private set; }
        public bool IsHovered { get; private set; }

        // ── Internals ─────────────────────────────────────────────────────
        private UILabel? _label;
        private int _cursorPos = 0;
        private float _scrollOffset = 0f;
        private float _blinkTimer = 0f;
        private bool _cursorVisible = true;
        private bool _labelDirty = true;
        private string _displayText = string.Empty;

        // Caché de mediciones GDI+ — se invalida cuando cambia fuente o texto
        private float[]? _charWidths;   // ancho acumulado hasta cada índice
        private string _measuredFor = string.Empty;
        private float _measuredSize = 0f;
        private string _measuredFont = string.Empty;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void Initialize()
        {
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

            // Blink
            if (IsFocused)
            {
                _blinkTimer += deltaTime;
                if (_blinkTimer >= CursorBlinkRate)
                {
                    _blinkTimer = 0f;
                    _cursorVisible = !_cursorVisible;
                }
            }
            else
            {
                _cursorVisible = false;
                _blinkTimer = 0f;
            }

            if (_labelDirty)
            {
                bool showPlaceholder = string.IsNullOrEmpty(_text) && !IsFocused;
                _label.TextColor = showPlaceholder ? PlaceholderColor : TextColor;
                _label.FontName = FontName;
                _label.FontSize = FontSize;
                _displayText = showPlaceholder
                    ? Placeholder
                    : (IsPassword ? new string('•', _text.Length) : _text);
                _label.Text = _displayText;
                _labelDirty = false;
                _charWidths = null;   // invalidar caché de medición
            }

            _label.Update(deltaTime);

            if (IsFocused) AdjustScroll();
        }

        public override void Draw(UIBatch batch)
        {
            if (!Visible || _label is null) return;

            // 1 — Fondo
            Vector4 bgColor = IsFocused ? FocusedColor : (IsHovered ? HoverColor : NormalColor);
            batch.DrawRoundedRect(Position, Size, bgColor, CornerRadius);

            // 2 — Borde
            if (BorderWidth > 0f)
            {
                Vector4 borderColor = IsFocused ? BorderFocused : BorderNormal;
                batch.DrawRoundedRect(
                    Position - new Vector2(BorderWidth),
                    Size + new Vector2(BorderWidth * 2f),
                    borderColor,
                    CornerRadius + BorderWidth);
                // redibujar fondo encima para que el borde quede solo en los bordes
                batch.DrawRoundedRect(Position, Size, bgColor, CornerRadius);
            }

            // 3 — Texto: el label tiene AutoSize=true, su Size = tamaño real del texto.
            //     Lo posicionamos verticalmente centrado y con scroll horizontal.
            if (!string.IsNullOrEmpty(_label.Text))
            {
                float textH = _label.Size.Y;
                float labelY = Position.Y + (Size.Y - textH) * 0.5f;
                _label.Position = new Vector2(Position.X + Padding - _scrollOffset, labelY);
                _label.Draw(batch);
            }

            // 4 — Cursor
            if (IsFocused && _cursorVisible)
            {
                float cx = Position.X + Padding + MeasureWidth(_cursorPos) - _scrollOffset;
                cx = Math.Clamp(cx, Position.X + Padding, Position.X + Size.X - Padding);
                float cy = Position.Y + Padding * 0.5f;
                float ch = Size.Y - Padding;
                batch.DrawRect(new Vector2(cx, cy), new Vector2(1.5f, ch), CursorColor);
            }
        }

        public override void Dispose()
        {
            _label?.Dispose();
            _label = null;
        }

        // ── Input ─────────────────────────────────────────────────────────

        public void UpdateInput(Vector2 refMousePos, bool leftDown, bool leftJustDown)
        {
            IsHovered = HitTest(refMousePos);

            if (leftJustDown)
            {
                bool wasFocused = IsFocused;
                IsFocused = IsHovered;

                if (IsFocused && !wasFocused)
                {
                    _cursorPos = _text.Length;
                    _cursorVisible = true;
                    _blinkTimer = 0f;
                    _labelDirty = true;
                }
                else if (!IsFocused && wasFocused)
                {
                    _scrollOffset = 0f;
                    _labelDirty = true;
                }

                if (IsFocused && IsHovered)
                {
                    float localX = refMousePos.X - (Position.X + Padding) + _scrollOffset;
                    _cursorPos = GetCharIndexAtX(localX);
                    _cursorVisible = true;
                    _blinkTimer = 0f;
                }
            }
        }

        public void ProcessChar(char c)
        {
            if (!IsFocused) return;
            if (IsNumericOnly && !char.IsDigit(c) && c != '.' && c != '-') return;
            if (MaxLength > 0 && _text.Length >= MaxLength) return;
            _text = _text.Insert(_cursorPos, c.ToString());
            _cursorPos = Math.Clamp(_cursorPos + 1, 0, _text.Length);
            _labelDirty = true;
            OnChange?.Invoke(_text);
        }

        public void ProcessBackspace()
        {
            if (!IsFocused || _cursorPos == 0) return;
            _text = _text.Remove(_cursorPos - 1, 1);
            _cursorPos = Math.Clamp(_cursorPos - 1, 0, _text.Length);
            _labelDirty = true;
            OnChange?.Invoke(_text);
        }

        public void ProcessDelete()
        {
            if (!IsFocused || _cursorPos >= _text.Length) return;
            _text = _text.Remove(_cursorPos, 1);
            _labelDirty = true;
            OnChange?.Invoke(_text);
        }

        public void ProcessEnter()
        {
            if (!IsFocused) return;
            OnSubmit?.Invoke(_text);
            IsFocused = false;
            _scrollOffset = 0f;
            _labelDirty = true;
        }

        public void ProcessHome()
        {
            if (!IsFocused) return;
            _cursorPos = 0; _cursorVisible = true; _blinkTimer = 0f;
        }

        public void ProcessEnd()
        {
            if (!IsFocused) return;
            _cursorPos = _text.Length; _cursorVisible = true; _blinkTimer = 0f;
        }

        public void ProcessLeft()
        {
            if (!IsFocused || _cursorPos == 0) return;
            _cursorPos--; _cursorVisible = true; _blinkTimer = 0f;
        }

        public void ProcessRight()
        {
            if (!IsFocused || _cursorPos >= _text.Length) return;
            _cursorPos++; _cursorVisible = true; _blinkTimer = 0f;
        }

        public void Unfocus()
        {
            IsFocused = false; _scrollOffset = 0f; _labelDirty = true;
        }

        // ── Medición GDI+ ─────────────────────────────────────────────────

        /// <summary>
        /// Devuelve el ancho en píxeles de referencia del texto de display
        /// desde el inicio hasta <paramref name="charIndex"/>.
        /// Usa GDI+ MeasureString para precisión proporcional real.
        /// El resultado se cachea y solo se recalcula cuando cambia el texto o la fuente.
        /// </summary>
        private float MeasureWidth(int charIndex)
        {
            EnsureCharWidths();
            if (_charWidths is null || charIndex <= 0) return 0f;
            charIndex = Math.Clamp(charIndex, 0, _charWidths.Length - 1);
            return _charWidths[charIndex];
        }

        private void EnsureCharWidths()
        {
            // Ya calculado para este texto+fuente
            if (_charWidths is not null &&
                _measuredFor == _displayText &&
                _measuredSize == FontSize &&
                _measuredFont == FontName)
                return;

            _measuredFor = _displayText;
            _measuredSize = FontSize;
            _measuredFont = FontName;

            int len = _displayText.Length;
            _charWidths = new float[len + 1];
            _charWidths[0] = 0f;

            if (len == 0) return;

            // Medir acumulado carácter a carácter con GDI+
            // Usamos StringFormat.GenericTypographic para resultados sin padding extra
            using var fmt = StringFormat.GenericTypographic;
            using var font = new SDFont(FontName, FontSize, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);
            using var bmp = new System.Drawing.Bitmap(1, 1);
            using var g = SDGraphics.FromImage(bmp);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            for (int i = 1; i <= len; i++)
            {
                string sub = _displayText[..i];
                SizeF sz = g.MeasureString(sub, font, PointF.Empty, fmt);
                _charWidths[i] = sz.Width;
            }
        }

        private void AdjustScroll()
        {
            float innerW = Size.X - Padding * 2f;
            float cursorW = MeasureWidth(_cursorPos);

            if (cursorW - _scrollOffset > innerW)
                _scrollOffset = cursorW - innerW;

            if (cursorW - _scrollOffset < 0f)
                _scrollOffset = cursorW;

            _scrollOffset = MathF.Max(0f, _scrollOffset);
        }

        private int GetCharIndexAtX(float localX)
        {
            EnsureCharWidths();
            if (_charWidths is null) return 0;

            for (int i = 1; i < _charWidths.Length; i++)
            {
                float mid = (_charWidths[i - 1] + _charWidths[i]) * 0.5f;
                if (localX < mid) return i - 1;
            }
            return _text.Length;
        }
    }
}