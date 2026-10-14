using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class RenderView {
    private const float FieldOfView = MathHelper.PiOver4;
    private const float NearPlane = 0.5f;
    private const float FarPlane = 500f;

    internal RenderView(RenderTarget2D renderTarget, Viewport viewport) {
        this.RenderTarget = renderTarget;
        this.Viewport = viewport;
        this.Projection = Matrix.CreatePerspectiveFieldOfView(FieldOfView, (float)viewport.Width / viewport.Height, NearPlane, FarPlane);
    }

    internal RenderTarget2D RenderTarget { get; }

    internal Viewport Viewport { get; }

    internal Matrix View { get; set; } = Matrix.Identity;

    internal Matrix Projection { get; }
}
