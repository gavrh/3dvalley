using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class SceneRenderer : IDisposable {
    private readonly GraphicsDevice device;

    private VertexBuffer? coloredVertexBuffer;
    private IndexBuffer? coloredIndexBuffer;
    private int coloredTriangleCount;
    private readonly List<TilesheetBuffers> tilesheets = new();
    private BasicEffect? effect;

    internal SceneRenderer(GraphicsDevice device) {
        this.device = device;
    }

    internal TerrainMesh? Terrain { get; private set; }

    internal void SetTerrain(TerrainMesh? terrain) {
        this.Terrain = terrain;

        this.coloredVertexBuffer?.Dispose();
        this.coloredVertexBuffer = null;
        this.coloredIndexBuffer?.Dispose();
        this.coloredIndexBuffer = null;
        this.coloredTriangleCount = 0;

        foreach (TilesheetBuffers buffers in this.tilesheets)
            buffers.Dispose();
        this.tilesheets.Clear();

        if (terrain is null)
            return;

        if (terrain.ColoredVertices.Length > 0 && terrain.ColoredIndices.Length > 0) {
            this.coloredVertexBuffer = new VertexBuffer(this.device, VertexPositionColor.VertexDeclaration, terrain.ColoredVertices.Length, BufferUsage.WriteOnly);
            this.coloredVertexBuffer.SetData(terrain.ColoredVertices);
            this.coloredIndexBuffer = new IndexBuffer(this.device, IndexElementSize.ThirtyTwoBits, terrain.ColoredIndices.Length, BufferUsage.WriteOnly);
            this.coloredIndexBuffer.SetData(terrain.ColoredIndices);
            this.coloredTriangleCount = terrain.ColoredIndices.Length / 3;
        }

        foreach (TerrainMesh.TilesheetGeometry geometry in terrain.Tilesheets) {
            if (geometry.Vertices.Length == 0 || geometry.Indices.Length == 0)
                continue;

            var vertexBuffer = new VertexBuffer(this.device, VertexPositionColorTexture.VertexDeclaration, geometry.Vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(geometry.Vertices);
            var indexBuffer = new IndexBuffer(this.device, IndexElementSize.ThirtyTwoBits, geometry.Indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(geometry.Indices);
            this.tilesheets.Add(new TilesheetBuffers(geometry.Texture, vertexBuffer, indexBuffer, geometry.TriangleCount));
        }
    }

    internal void Draw(RenderView view) {
        if (view.Viewport.Width <= 0 || view.Viewport.Height <= 0)
            return;
        if (this.Terrain is null || (this.tilesheets.Count == 0 && this.coloredVertexBuffer is null))
            return;

        this.effect ??= new BasicEffect(this.device) {
            LightingEnabled = false,
        };

        Viewport previousViewport = this.device.Viewport;
        DepthStencilState previousDepth = this.device.DepthStencilState;
        RasterizerState previousRasterizer = this.device.RasterizerState;
        BlendState previousBlend = this.device.BlendState;
        SamplerState previousSampler = this.device.SamplerStates[0];

        try {
            this.device.Viewport = view.Viewport;
            this.effect.World = Matrix.Identity;
            this.effect.View = view.View;
            this.effect.Projection = view.Projection;

            this.effect.VertexColorEnabled = true;
            this.effect.TextureEnabled = true;
            foreach (TilesheetBuffers buffers in this.tilesheets) {
                this.effect.Texture = buffers.Texture;
                this.DrawTerrain(buffers.VertexBuffer, buffers.IndexBuffer, buffers.TriangleCount);
            }

            if (this.coloredVertexBuffer is not null && this.coloredIndexBuffer is not null) {
                this.effect.TextureEnabled = false;
                this.effect.Texture = null;
                this.DrawTerrain(this.coloredVertexBuffer, this.coloredIndexBuffer, this.coloredTriangleCount);
            }
        }
        finally {
            this.device.SetVertexBuffer(null);
            this.device.Indices = null;
            this.device.Viewport = previousViewport;
            this.device.DepthStencilState = previousDepth;
            this.device.RasterizerState = previousRasterizer;
            this.device.BlendState = previousBlend;
            this.device.SamplerStates[0] = previousSampler;
        }
    }

    private void DrawTerrain(VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int triangleCount) {
        this.device.SetVertexBuffer(vertexBuffer);
        this.device.Indices = indexBuffer;

        foreach (EffectPass pass in this.effect!.CurrentTechnique.Passes) {
            pass.Apply();
            this.device.DepthStencilState = DepthStencilState.Default;
            this.device.RasterizerState = RasterizerState.CullNone;
            this.device.BlendState = BlendState.Opaque;
            this.device.SamplerStates[0] = SamplerState.PointClamp;
            this.device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, triangleCount);
        }
    }

    public void Dispose() {
        this.coloredVertexBuffer?.Dispose();
        this.coloredVertexBuffer = null;
        this.coloredIndexBuffer?.Dispose();
        this.coloredIndexBuffer = null;

        foreach (TilesheetBuffers buffers in this.tilesheets)
            buffers.Dispose();
        this.tilesheets.Clear();

        this.effect?.Dispose();
        this.effect = null;
    }

    private sealed class TilesheetBuffers : IDisposable {
        internal TilesheetBuffers(Texture2D texture, VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int triangleCount) {
            this.Texture = texture;
            this.VertexBuffer = vertexBuffer;
            this.IndexBuffer = indexBuffer;
            this.TriangleCount = triangleCount;
        }

        internal Texture2D Texture { get; }

        internal VertexBuffer VertexBuffer { get; }

        internal IndexBuffer IndexBuffer { get; }

        internal int TriangleCount { get; }

        public void Dispose() {
            this.VertexBuffer.Dispose();
            this.IndexBuffer.Dispose();
        }
    }
}
