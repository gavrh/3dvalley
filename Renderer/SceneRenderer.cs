using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class SceneRenderer : IDisposable {
    private static readonly VertexPositionColor[] Geometry = {
        new(new Vector3(-0.6f, 1f, 0f), Color.IndianRed),
        new(new Vector3(-1.6f, -1f, 0f), Color.IndianRed),
        new(new Vector3(0.4f, -1f, 0f), Color.IndianRed),
        new(new Vector3(0.7f, 1f, -2f), Color.MediumSeaGreen),
        new(new Vector3(-0.3f, -1f, -2f), Color.MediumSeaGreen),
        new(new Vector3(1.7f, -1f, -2f), Color.MediumSeaGreen),
    };

    private readonly GraphicsDevice device;

    private VertexBuffer? vertexBuffer;
    private BasicEffect? effect;

    internal SceneRenderer(GraphicsDevice device) {
        this.device = device;
    }

    internal void Draw(RenderView view) {
        if (view.Viewport.Width <= 0 || view.Viewport.Height <= 0)
            return;

        this.EnsureResources();

        Viewport previousViewport = this.device.Viewport;
        DepthStencilState previousDepth = this.device.DepthStencilState;
        RasterizerState previousRasterizer = this.device.RasterizerState;
        BlendState previousBlend = this.device.BlendState;

        try {
            this.device.Viewport = view.Viewport;
            this.effect!.World = Matrix.Identity;
            this.effect.View = view.View;
            this.effect.Projection = view.Projection;

            this.device.SetVertexBuffer(this.vertexBuffer);
            foreach (EffectPass pass in this.effect.CurrentTechnique.Passes) {
                pass.Apply();
                this.device.DepthStencilState = DepthStencilState.Default;
                this.device.RasterizerState = RasterizerState.CullNone;
                this.device.BlendState = BlendState.Opaque;
                this.device.DrawPrimitives(PrimitiveType.TriangleList, 0, Geometry.Length / 3);
            }
        }
        finally {
            this.device.SetVertexBuffer(null);
            this.device.Viewport = previousViewport;
            this.device.DepthStencilState = previousDepth;
            this.device.RasterizerState = previousRasterizer;
            this.device.BlendState = previousBlend;
        }
    }

    public void Dispose() {
        this.vertexBuffer?.Dispose();
        this.vertexBuffer = null;
        this.effect?.Dispose();
        this.effect = null;
    }

    private void EnsureResources() {
        if (this.vertexBuffer is null) {
            this.vertexBuffer = new VertexBuffer(this.device, VertexPositionColor.VertexDeclaration, Geometry.Length, BufferUsage.WriteOnly);
            this.vertexBuffer.SetData(Geometry);
        }

        this.effect ??= new BasicEffect(this.device) {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };
    }
}
