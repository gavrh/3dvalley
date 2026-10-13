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
    private readonly Matrix view;

    private VertexBuffer? vertexBuffer;
    private BasicEffect? effect;
    private Matrix projection;
    private int projectionWidth;
    private int projectionHeight;

    internal SceneRenderer(GraphicsDevice device) {
        this.device = device;
        this.view = Matrix.CreateLookAt(new Vector3(0f, 0f, 3f), Vector3.Zero, Vector3.Up);
    }

    internal void Draw(int width, int height) {
        if (width <= 0 || height <= 0)
            return;

        this.EnsureResources();

        if (this.projectionWidth != width || this.projectionHeight != height) {
            this.projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, (float)width / height, 0.1f, 100f);
            this.projectionWidth = width;
            this.projectionHeight = height;
        }

        DepthStencilState previousDepth = this.device.DepthStencilState;
        RasterizerState previousRasterizer = this.device.RasterizerState;
        BlendState previousBlend = this.device.BlendState;

        try {
            this.effect!.World = Matrix.Identity;
            this.effect.View = this.view;
            this.effect.Projection = this.projection;

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
