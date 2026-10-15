using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley.Renderer;

internal sealed class SceneRenderer : IDisposable {
    private static readonly Comparison<BillboardDraw> BackToFront = (a, b) => b.DistanceSquared.CompareTo(a.DistanceSquared);
    private static readonly short[] QuadIndices = { 0, 1, 2, 0, 2, 3 };

    private readonly GraphicsDevice device;
    private readonly IndexBuffer quadIndexBuffer;
    private readonly List<BillboardDraw> billboards = new();

    private VertexBuffer? coloredVertexBuffer;
    private IndexBuffer? coloredIndexBuffer;
    private int coloredTriangleCount;
    private readonly List<TilesheetBuffers> tilesheets = new();
    private BasicEffect? effect;

    internal SceneRenderer(GraphicsDevice device) {
        this.device = device;
        this.quadIndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, QuadIndices.Length, BufferUsage.WriteOnly);
        this.quadIndexBuffer.SetData(QuadIndices);
    }

    internal TerrainMesh? Terrain { get; private set; }

    internal IReadOnlyList<Billboard>? Billboards { get; private set; }

    internal int DrawCallCount => this.tilesheets.Count + (this.coloredVertexBuffer is not null ? 1 : 0) + this.billboards.Count;

    internal int BillboardCount => this.billboards.Count;

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

    internal void SetBillboards(IReadOnlyList<Billboard>? billboards) {
        this.Billboards = billboards;

        foreach (BillboardDraw draw in this.billboards)
            draw.Dispose();
        this.billboards.Clear();

        if (billboards is null)
            return;

        foreach (Billboard billboard in billboards)
            this.billboards.Add(new BillboardDraw(billboard, this.CreateBillboardVertices(billboard)));
    }

    internal void Draw(RenderView view) {
        if (view.Viewport.Width <= 0 || view.Viewport.Height <= 0)
            return;

        bool hasTerrain = this.tilesheets.Count > 0 || this.coloredVertexBuffer is not null;
        if (!hasTerrain && this.billboards.Count == 0)
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

            if (hasTerrain) {
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

            this.DrawBillboards(view);
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

    private void DrawBillboards(RenderView view) {
        if (this.billboards.Count == 0)
            return;

        Vector3 cameraPosition = Matrix.Invert(view.View).Translation;
        foreach (BillboardDraw draw in this.billboards)
            draw.DistanceSquared = Vector3.DistanceSquared(draw.Billboard.BaseCenter, cameraPosition);
        this.billboards.Sort(BackToFront);

        this.effect!.VertexColorEnabled = false;
        this.effect.TextureEnabled = true;

        this.device.DepthStencilState = DepthStencilState.DepthRead;
        this.device.RasterizerState = RasterizerState.CullNone;
        this.device.BlendState = BlendState.AlphaBlend;
        this.device.SamplerStates[0] = SamplerState.PointClamp;
        this.device.Indices = this.quadIndexBuffer;

        foreach (BillboardDraw draw in this.billboards) {
            this.effect.World = BillboardWorld(draw.Billboard, cameraPosition);
            this.effect.Texture = draw.Billboard.Texture;
            this.device.SetVertexBuffer(draw.VertexBuffer);

            foreach (EffectPass pass in this.effect.CurrentTechnique.Passes) {
                pass.Apply();
                this.device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }
    }

    private static Matrix BillboardWorld(Billboard billboard, Vector3 cameraPosition) {
        Vector3 forward = cameraPosition - billboard.BaseCenter;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.000001f)
            forward = Vector3.Backward;
        else
            forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.Up, forward);
        float width = billboard.Width;
        float height = billboard.Height;

        return new Matrix(
            right.X * width, right.Y * width, right.Z * width, 0f,
            Vector3.Up.X * height, Vector3.Up.Y * height, Vector3.Up.Z * height, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            billboard.BaseCenter.X, billboard.BaseCenter.Y, billboard.BaseCenter.Z, 1f);
    }

    private VertexBuffer CreateBillboardVertices(Billboard billboard) {
        float u0 = billboard.Source.Left / (float)billboard.Texture.Width;
        float v0 = billboard.Source.Top / (float)billboard.Texture.Height;
        float u1 = billboard.Source.Right / (float)billboard.Texture.Width;
        float v1 = billboard.Source.Bottom / (float)billboard.Texture.Height;

        var vertices = new[] {
            new VertexPositionTexture(new Vector3(-0.5f, 0f, 0f), new Vector2(u0, v1)),
            new VertexPositionTexture(new Vector3(0.5f, 0f, 0f), new Vector2(u1, v1)),
            new VertexPositionTexture(new Vector3(0.5f, 1f, 0f), new Vector2(u1, v0)),
            new VertexPositionTexture(new Vector3(-0.5f, 1f, 0f), new Vector2(u0, v0)),
        };

        var vertexBuffer = new VertexBuffer(this.device, VertexPositionTexture.VertexDeclaration, vertices.Length, BufferUsage.WriteOnly);
        vertexBuffer.SetData(vertices);
        return vertexBuffer;
    }

    public void Dispose() {
        this.coloredVertexBuffer?.Dispose();
        this.coloredVertexBuffer = null;
        this.coloredIndexBuffer?.Dispose();
        this.coloredIndexBuffer = null;

        foreach (TilesheetBuffers buffers in this.tilesheets)
            buffers.Dispose();
        this.tilesheets.Clear();

        foreach (BillboardDraw draw in this.billboards)
            draw.Dispose();
        this.billboards.Clear();

        this.quadIndexBuffer.Dispose();

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

    private sealed class BillboardDraw : IDisposable {
        internal BillboardDraw(Billboard billboard, VertexBuffer vertexBuffer) {
            this.Billboard = billboard;
            this.VertexBuffer = vertexBuffer;
        }

        internal Billboard Billboard { get; }

        internal VertexBuffer VertexBuffer { get; }

        internal float DistanceSquared { get; set; }

        public void Dispose() {
            this.VertexBuffer.Dispose();
        }
    }
}
