using OpenTK.Mathematics;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Base class for all UI elements. Subclass this to create custom widgets.
    /// </summary>
    public abstract class UIElement
    {
        // ── Identity ──────────────────────────────────────────────────────
        public string Name { get; set; } = string.Empty;
        public object? Tag  { get; set; }

        // ── Layout ────────────────────────────────────────────────────────
        /// <summary>Position in screen pixels. Origin = top-left of the canvas.</summary>
        public Vector2 Position { get; set; } = Vector2.Zero;

        /// <summary>Size in pixels. (0,0) = auto-size (element decides).</summary>
        public Vector2 Size { get; set; } = Vector2.Zero;

        // ── Appearance ────────────────────────────────────────────────────
        /// <summary>RGBA tint multiplied on top of the drawn content.</summary>
        public Vector4 Color { get; set; } = Vector4.One;

        // ── State ─────────────────────────────────────────────────────────
        public bool Visible  { get; set; } = true;
        public bool Enabled  { get; set; } = true;

        // ── Sorting ───────────────────────────────────────────────────────
        /// <summary>Higher = drawn on top.</summary>
        public int ZOrder { get; set; } = 0;

        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>Called once when the element is added to a canvas (GL context ready).</summary>
        public virtual void Initialize() { }

        /// <summary>Called every frame before Draw.</summary>
        public virtual void Update(float deltaTime) { }

        /// <summary>Emit draw calls via <paramref name="batch"/>.</summary>
        public abstract void Draw(UIBatch batch);

        /// <summary>Release GPU resources.</summary>
        public virtual void Dispose() { }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>Returns true if the screen-space point is inside this element's bounds.</summary>
        public bool HitTest(Vector2 screenPoint)
            => screenPoint.X >= Position.X && screenPoint.X <= Position.X + Size.X
            && screenPoint.Y >= Position.Y && screenPoint.Y <= Position.Y + Size.Y;
    }
}
