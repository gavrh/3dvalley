using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class SceneRenderer : IDisposable {
    private readonly GraphicsDevice device;

    private VertexBuffer? vertexBuffer;
    private IndexBuffer? indexBuffer;
    private BasicEffect? effect;

    internal SceneRenderer(GraphicsDevice device) {
        this.device = device;
    }

    internal TerrainMesh? Terrain { get; private set; }

    internal void SetTerrain(TerrainMesh? terrain) {
        this.Terrain = terrain;

        this.vertexBuffer?.Dispose();
        this.vertexBuffer = null;
        this.indexBuffer?.Dispose();
        this.indexBuffer = null;

        if (terrain is null || terrain.Vertices.Length == 0 || terrain.Indices.Length == 0)
            return;

        this.vertexBuffer = new VertexBuffer(this.device, VertexPositionColor.VertexDeclaration, terrain.Vertices.Length, BufferUsage.WriteOnly);
        this.vertexBuffer.SetData(terrain.Vertices);
        this.indexBuffer = new IndexBuffer(this.device, IndexElementSize.ThirtyTwoBits, terrain.Indices.Length, BufferUsage.WriteOnly);
        this.indexBuffer.SetData(terrain.Indices);
    }

    internal void Draw(RenderView view) {
        if (view.Viewport.Width <= 0 || view.Viewport.Height <= 0)
            return;
        if (this.vertexBuffer is null || this.indexBuffer is null || this.Terrain is null)
            return;

        this.effect ??= new BasicEffect(this.device) {
            VertexColorEnabled = true,
            LightingEnabled = false,
        };

        Viewport previousViewport = this.device.Viewport;
        DepthStencilState previousDepth = this.device.DepthStencilState;
        RasterizerState previousRasterizer = this.device.RasterizerState;
        BlendState previousBlend = this.device.BlendState;

        try {
            this.device.Viewport = view.Viewport;
            this.effect.World = Matrix.Identity;
            this.effect.View = view.View;
            this.effect.Projection = view.Projection;

            this.device.SetVertexBuffer(this.vertexBuffer);
            this.device.Indices = this.indexBuffer;
            foreach (EffectPass pass in this.effect.CurrentTechnique.Passes) {
                pass.Apply();
                this.device.DepthStencilState = DepthStencilState.Default;
                this.device.RasterizerState = RasterizerState.CullNone;
                this.device.BlendState = BlendState.Opaque;
                this.device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, this.Terrain.TriangleCount);
            }
        }
        finally {
            this.device.SetVertexBuffer(null);
            this.device.Indices = null;
            this.device.Viewport = previousViewport;
            this.device.DepthStencilState = previousDepth;
            this.device.RasterizerState = previousRasterizer;
            this.device.BlendState = previousBlend;
        }
    }

    public void Dispose() {
        this.vertexBuffer?.Dispose();
        this.vertexBuffer = null;
        this.indexBuffer?.Dispose();
        this.indexBuffer = null;
        this.effect?.Dispose();
        this.effect = null;
    }
}
