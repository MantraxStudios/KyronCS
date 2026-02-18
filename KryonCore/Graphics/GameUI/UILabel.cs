using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing;
using System.Drawing.Text;
using SDFont       = System.Drawing.Font;
using SDColor      = System.Drawing.Color;
using SDGraphics   = System.Drawing.Graphics;
using SDPixelFormat= System.Drawing.Imaging.PixelFormat;
using SDImageLock  = System.Drawing.Imaging.ImageLockMode;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Renders a string as a GPU texture quad.
    /// Text is rendered at 2× resolution (supersampling) and stored in a full-res
    /// texture; the shader downsamples it automatically, giving noticeably crisper
    /// glyphs at all sizes without any extra passes.
    ///
    /// The texture is rebuilt automatically when any visual property changes.
    /// </summary>
    public sealed class UILabel : UIElement
    {
        // ── Text ──────────────────────────────────────────────────────────
        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set { if (_text != value) { _text = value ?? string.Empty; _dirty = true; } }
        }

        // ── Font ──────────────────────────────────────────────────────────
        private string _fontName = "Segoe UI";
        public string FontName
        {
            get => _fontName;
            set { if (_fontName != value) { _fontName = value; _dirty = true; } }
        }

        private float _fontSize = 24f;
        public float FontSize
        {
            get => _fontSize;
            set { if (MathF.Abs(_fontSize - value) > 0.01f) { _fontSize = value; _dirty = true; } }
        }

        private FontStyle _fontStyle = FontStyle.Regular;
        public FontStyle FontStyle
        {
            get => _fontStyle;
            set { if (_fontStyle != value) { _fontStyle = value; _dirty = true; } }
        }

        // ── Colors ────────────────────────────────────────────────────────
        private SDColor _textColor = SDColor.White;
        public SDColor TextColor
        {
            get => _textColor;
            set { if (_textColor != value) { _textColor = value; _dirty = true; } }
        }

        /// <summary>
        /// Drop-shadow color. Set Alpha = 0 to disable (default).
        /// A subtle dark shadow (e.g. FromArgb(160, 0, 0, 0)) greatly improves
        /// legibility on variable backgrounds.
        /// </summary>
        public SDColor ShadowColor  { get; set; } = SDColor.FromArgb(0, 0, 0, 0);
        public PointF  ShadowOffset { get; set; } = new PointF(1f, 1f);

        // ── Layout ────────────────────────────────────────────────────────
        /// <summary>
        /// When true (default), Size is automatically set from the measured text extent.
        /// Set false and assign Size manually to clip or scroll content.
        /// </summary>
        public bool  AutoSize { get; set; } = true;
        public float Rotation { get; set; } = 0f;

        // ── Supersampling ─────────────────────────────────────────────────
        /// <summary>
        /// Supersampling factor (1 = off, 2 = 2× — recommended, 3 = 3×).
        /// Higher values sharpen small text at the cost of more VRAM and CPU rebuild time.
        /// </summary>
        public int SuperSample { get; set; } = 2;

        // ── GL ────────────────────────────────────────────────────────────
        private int  _textureId;
        private bool _dirty       = true;
        private bool _initialized;

        // ── Lifecycle ─────────────────────────────────────────────────────

        public override void Initialize()
        {
            _textureId   = GL.GenTexture();
            _initialized = true;
            _dirty       = true;
        }

        public override void Update(float deltaTime)
        {
            if (_dirty && _initialized)
                Rebuild();
        }

        public override void Draw(UIBatch batch)
        {
            if (!Visible) return;
            if (_dirty) Rebuild();
            if (_textureId == 0 || string.IsNullOrEmpty(_text)) return;

            batch.DrawTexture(Position, Size, _textureId, Color, Rotation);
        }

        public override void Dispose()
        {
            if (_textureId != 0) { GL.DeleteTexture(_textureId); _textureId = 0; }
        }

        // ── Private ───────────────────────────────────────────────────────

        private void Rebuild()
        {
            _dirty = false;
            if (!_initialized || string.IsNullOrEmpty(_text)) return;

            int ss = Math.Clamp(SuperSample, 1, 4);

            // Render at ss× the display font size
            float renderFontSize = _fontSize * ss;

            using var font  = new SDFont(_fontName, renderFontSize, _fontStyle, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(_textColor);

            // Measure at render size
            SizeF measured;
            using (var tmp = new Bitmap(1, 1))
            using (var g   = SDGraphics.FromImage(tmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                measured = g.MeasureString(_text, font);
            }

            // Bitmap at render (ss×) resolution with extra padding to avoid clipping
            int bmpW = Math.Max(1, (int)MathF.Ceiling(measured.Width)  + 4 * ss);
            int bmpH = Math.Max(1, (int)MathF.Ceiling(measured.Height) + 4 * ss);

            using var bmp = new Bitmap(bmpW, bmpH, SDPixelFormat.Format32bppArgb);
            using (var g  = SDGraphics.FromImage(bmp))
            {
                g.Clear(SDColor.Transparent);

                // ClearType grid-fit gives sharper horizontally sub-pixeled glyphs;
                // fall back to AntiAlias if ClearType fails (transparent background).
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                // Drop shadow
                if (ShadowColor.A > 0)
                {
                    using var sb = new SolidBrush(ShadowColor);
                    g.DrawString(_text, font, sb,
                                 ShadowOffset.X * ss,
                                 ShadowOffset.Y * ss);
                }

                g.DrawString(_text, font, brush, 0f, 0f);
            }

            // Upload full-resolution bitmap to the GL texture
            var bits = bmp.LockBits(
                new Rectangle(0, 0, bmpW, bmpH),
                SDImageLock.ReadOnly,
                SDPixelFormat.Format32bppArgb);

            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.TexImage2D(
                TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgba,
                bmpW, bmpH, 0,
                OpenTK.Graphics.OpenGL4.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                bits.Scan0);
            bmp.UnlockBits(bits);

            // Trilinear / anisotropic-ready min-filter for the downscaled display size
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D,
                TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // Generate mipmaps so the downsampled display size picks the best mip level
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // Display size = render size ÷ ss  →  crisp supersampled result
            if (AutoSize)
                Size = new Vector2((float)bmpW / ss, (float)bmpH / ss);
        }
    }
}
