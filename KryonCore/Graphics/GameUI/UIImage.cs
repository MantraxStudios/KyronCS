using OpenTK.Mathematics;

namespace KrayonCore.Graphics.GameUI
{
    /// <summary>
    /// Draws a solid-color rectangle or an existing OpenGL texture.
    /// Use as a panel background, image display, or colored overlay.
    /// </summary>
    public sealed class UIImage : UIElement
    {
        /// <summary>
        /// OpenGL texture handle. Set to 0 for a solid <see cref="UIElement.Color"/> rect.
        /// </summary>
        public int TextureId { get; set; } = 0;

        /// <summary>Rotation in radians, around the element center.</summary>
        public float Rotation { get; set; } = 0f;

        public override void Draw(UIBatch batch)
        {
            if (!Visible) return;

            if (TextureId != 0)
                batch.DrawTexture(Position, Size, TextureId, Color, Rotation);
            else
                batch.DrawRect(Position, Size, Color, Rotation);
        }
    }
}
