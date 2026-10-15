using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class Billboard {
    internal const float PixelsPerWorldUnit = 16f;

    internal Billboard(Vector3 baseCenter, Rectangle source, Texture2D texture) {
        this.BaseCenter = baseCenter;
        this.Source = source;
        this.Texture = texture;
        this.Width = source.Width / PixelsPerWorldUnit;
        this.Height = source.Height / PixelsPerWorldUnit;
    }

    internal Vector3 BaseCenter { get; }

    internal Rectangle Source { get; }

    internal Texture2D Texture { get; }

    internal float Width { get; }

    internal float Height { get; }
}
