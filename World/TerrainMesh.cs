using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace ThreeDValley;

internal sealed class TerrainMesh : IDisposable {
    internal const float WaterHeight = -0.5f;
    internal const float GradeHeight = 0f;
    internal const float RaisedHeight = 0.75f;
    internal const float BorderBaseHeight = -1f;

    private enum Surface { Grass, Dirt, Path, Water, Cliff }

    private static readonly Color GrassColor = new(92, 148, 72);
    private static readonly Color DirtColor = new(158, 120, 74);
    private static readonly Color PathColor = new(150, 148, 140);
    private static readonly Color WaterColor = new(62, 106, 176);
    private static readonly Color CliffColor = new(120, 106, 92);

    private readonly TilesheetGeometry[] tilesheets;
    private bool disposed;

    private TerrainMesh(VertexPositionColor[] coloredVertices, int[] coloredIndices, TilesheetGeometry[] tilesheets, int tilesWide, int tilesHigh) {
        this.ColoredVertices = coloredVertices;
        this.ColoredIndices = coloredIndices;
        this.tilesheets = tilesheets;
        this.TilesWide = tilesWide;
        this.TilesHigh = tilesHigh;
    }

    internal VertexPositionColor[] ColoredVertices { get; }

    internal int[] ColoredIndices { get; }

    internal IReadOnlyList<TilesheetGeometry> Tilesheets => this.tilesheets;

    internal int TilesWide { get; }

    internal int TilesHigh { get; }

    internal int TileCount => this.TilesWide * this.TilesHigh;

    internal int TilesheetCount => this.tilesheets.Length;

    internal int VertexCount => this.ColoredVertices.Length + this.tilesheets.Sum(geometry => geometry.Vertices.Length);

    internal int TriangleCount => this.ColoredIndices.Length / 3 + this.tilesheets.Sum(geometry => geometry.TriangleCount);

    internal static TerrainMesh Build(GameLocation location, Func<string, Texture2D> loadTexture) {
        Layer? back = location.Map?.GetLayer("Back") ?? location.Map?.Layers.FirstOrDefault();
        if (back is null)
            return new TerrainMesh(Array.Empty<VertexPositionColor>(), Array.Empty<int>(), Array.Empty<TilesheetGeometry>(), 0, 0);

        Layer? buildings = location.Map?.GetLayer("Buildings");
        int width = back.LayerWidth;
        int height = back.LayerHeight;

        var heights = new float[width * height];
        var surfaces = new Surface[width * height];
        var tileTextures = new TileTexture?[width * height];

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = y * width + x;
                bool hasBuilding = buildings is not null && buildings.Tiles[x, y] is not null;
                bool impassable = location.doesTileHaveProperty(x, y, "Passable", "Back", false) == "F"
                    || location.doesTileHaveProperty(x, y, "Passable", "Buildings", false) == "F";
                bool water = location.isWaterTile(x, y);

                if (water && !hasBuilding) {
                    heights[index] = WaterHeight;
                    surfaces[index] = Surface.Water;
                }
                else if (hasBuilding || impassable) {
                    heights[index] = RaisedHeight;
                    surfaces[index] = Surface.Cliff;
                }
                else {
                    heights[index] = GradeHeight;
                    surfaces[index] = ParseSurface(location.doesTileHaveProperty(x, y, "Type", "Back", false));
                }

                tileTextures[index] = ResolveTileTexture(back.Tiles[x, y]);
            }
        }

        Dictionary<TileSheet, Texture2D?> textures = LoadTextures(tileTextures, loadTexture);

        var coloredVertices = new List<VertexPositionColor>(width * height * 4);
        var coloredIndices = new List<int>(width * height * 6);
        var groups = new Dictionary<TileSheet, TexturedBuilder>();
        int unresolvedTiles = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = y * width + x;
                float top = heights[index];
                Color topColor = ColorFor(surfaces[index]);
                Color sideColor = Darken(topColor);

                TileTexture? tileTexture = tileTextures[index];
                Texture2D? texture = null;
                if (tileTexture is TileTexture resolved)
                    textures.TryGetValue(resolved.Sheet, out texture);
                else
                    unresolvedTiles++;

                if (texture is not null && tileTexture is TileTexture tile) {
                    if (!groups.TryGetValue(tile.Sheet, out TexturedBuilder? builder)) {
                        builder = new TexturedBuilder(tile.Sheet.ImageSource);
                        groups[tile.Sheet] = builder;
                    }

                    AddTexturedQuad(builder.Vertices, builder.Indices,
                        new Vector3(x, top, y),
                        new Vector3(x + 1, top, y),
                        new Vector3(x + 1, top, y + 1),
                        new Vector3(x, top, y + 1),
                        tile.Bounds, texture);
                }
                else {
                    AddQuad(coloredVertices, coloredIndices,
                        new Vector3(x, top, y),
                        new Vector3(x + 1, top, y),
                        new Vector3(x + 1, top, y + 1),
                        new Vector3(x, top, y + 1),
                        topColor);
                }

                AddSkirt(coloredVertices, coloredIndices, heights, width, height, x, y, top, sideColor, 1, 0);
                AddSkirt(coloredVertices, coloredIndices, heights, width, height, x, y, top, sideColor, -1, 0);
                AddSkirt(coloredVertices, coloredIndices, heights, width, height, x, y, top, sideColor, 0, 1);
                AddSkirt(coloredVertices, coloredIndices, heights, width, height, x, y, top, sideColor, 0, -1);
            }
        }

        if (unresolvedTiles > 0)
            Log.Warn($"{unresolvedTiles} terrain tiles have no resolvable tilesheet; using flat surface colour.");

        var tilesheets = new List<TilesheetGeometry>(groups.Count);
        foreach ((TileSheet sheet, TexturedBuilder builder) in groups) {
            if (builder.Vertices.Count == 0)
                continue;
            if (!textures.TryGetValue(sheet, out Texture2D? texture) || texture is null)
                continue;

            tilesheets.Add(new TilesheetGeometry(sheet.ImageSource, texture, builder.Vertices.ToArray(), builder.Indices.ToArray()));
        }

        return new TerrainMesh(coloredVertices.ToArray(), coloredIndices.ToArray(), tilesheets.ToArray(), width, height);
    }

    private static Dictionary<TileSheet, Texture2D?> LoadTextures(TileTexture?[] tileTextures, Func<string, Texture2D> loadTexture) {
        var textures = new Dictionary<TileSheet, Texture2D?>();
        var failures = new List<string>();

        foreach (TileTexture? candidate in tileTextures) {
            if (candidate is not TileTexture tileTexture || textures.ContainsKey(tileTexture.Sheet))
                continue;

            string imageSource = tileTexture.Sheet.ImageSource;
            Texture2D? texture = null;
            try {
                texture = loadTexture(imageSource);
                if (texture is null)
                    failures.Add($"{imageSource}: the loader returned no texture");
            }
            catch (Exception ex) {
                failures.Add($"{imageSource}: {ex.Message}");
            }

            textures[tileTexture.Sheet] = texture;
        }

        foreach (string failure in failures)
            Log.Warn($"Tilesheet failed to load; using flat surface colour. {failure}");

        return textures;
    }

    private static TileTexture? ResolveTileTexture(Tile? tile) {
        StaticTile? staticTile = tile switch {
            AnimatedTile animated when animated.TileFrames.Length > 0 => animated.TileFrames[0],
            StaticTile value => value,
            _ => null,
        };

        if (staticTile?.TileSheet is not TileSheet sheet)
            return null;
        if (sheet.SheetWidth <= 0 || sheet.TileWidth <= 0 || sheet.TileHeight <= 0)
            return null;

        int column = staticTile.TileIndex % sheet.SheetWidth;
        int row = staticTile.TileIndex / sheet.SheetWidth;
        int pixelX = sheet.MarginWidth + column * (sheet.TileWidth + sheet.SpacingWidth);
        int pixelY = sheet.MarginHeight + row * (sheet.TileHeight + sheet.SpacingHeight);
        return new TileTexture(sheet, new Rectangle(pixelX, pixelY, sheet.TileWidth, sheet.TileHeight));
    }

    private static void AddSkirt(List<VertexPositionColor> vertices, List<int> indices, float[] heights, int width, int height, int x, int y, float top, Color color, int dx, int dy) {
        int nx = x + dx;
        int ny = y + dy;

        float neighbour = nx < 0 || ny < 0 || nx >= width || ny >= height
            ? BorderBaseHeight
            : heights[ny * width + nx];

        if (neighbour >= top)
            return;

        if (dx > 0)
            AddQuad(vertices, indices, new(x + 1, top, y), new(x + 1, top, y + 1), new(x + 1, neighbour, y + 1), new(x + 1, neighbour, y), color);
        else if (dx < 0)
            AddQuad(vertices, indices, new(x, top, y + 1), new(x, top, y), new(x, neighbour, y), new(x, neighbour, y + 1), color);
        else if (dy > 0)
            AddQuad(vertices, indices, new(x + 1, top, y + 1), new(x, top, y + 1), new(x, neighbour, y + 1), new(x + 1, neighbour, y + 1), color);
        else
            AddQuad(vertices, indices, new(x, top, y), new(x + 1, top, y), new(x + 1, neighbour, y), new(x, neighbour, y), color);
    }

    private static void AddQuad(List<VertexPositionColor> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) {
        int first = vertices.Count;
        vertices.Add(new VertexPositionColor(a, color));
        vertices.Add(new VertexPositionColor(b, color));
        vertices.Add(new VertexPositionColor(c, color));
        vertices.Add(new VertexPositionColor(d, color));
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
        indices.Add(first);
        indices.Add(first + 2);
        indices.Add(first + 3);
    }

    private static void AddTexturedQuad(List<VertexPositionColorTexture> vertices, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rectangle bounds, Texture2D texture) {
        float u0 = bounds.Left / (float)texture.Width;
        float v0 = bounds.Top / (float)texture.Height;
        float u1 = bounds.Right / (float)texture.Width;
        float v1 = bounds.Bottom / (float)texture.Height;

        int first = vertices.Count;
        vertices.Add(new VertexPositionColorTexture(a, Color.White, new Vector2(u0, v0)));
        vertices.Add(new VertexPositionColorTexture(b, Color.White, new Vector2(u1, v0)));
        vertices.Add(new VertexPositionColorTexture(c, Color.White, new Vector2(u1, v1)));
        vertices.Add(new VertexPositionColorTexture(d, Color.White, new Vector2(u0, v1)));
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
        indices.Add(first);
        indices.Add(first + 2);
        indices.Add(first + 3);
    }

    private static Surface ParseSurface(string? type) {
        return type switch {
            "Dirt" => Surface.Dirt,
            "Stone" => Surface.Path,
            "Wood" => Surface.Path,
            _ => Surface.Grass,
        };
    }

    private static Color ColorFor(Surface surface) {
        return surface switch {
            Surface.Dirt => DirtColor,
            Surface.Path => PathColor,
            Surface.Water => WaterColor,
            Surface.Cliff => CliffColor,
            _ => GrassColor,
        };
    }

    private static Color Darken(Color color) {
        const float factor = 0.6f;
        return new Color((int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor));
    }

    public void Dispose() {
        if (this.disposed)
            return;
        this.disposed = true;

        foreach (TilesheetGeometry geometry in this.tilesheets)
            geometry.Texture.Dispose();
    }

    internal sealed class TilesheetGeometry {
        internal TilesheetGeometry(string imageSource, Texture2D texture, VertexPositionColorTexture[] vertices, int[] indices) {
            this.ImageSource = imageSource;
            this.Texture = texture;
            this.Vertices = vertices;
            this.Indices = indices;
        }

        internal string ImageSource { get; }

        internal Texture2D Texture { get; }

        internal VertexPositionColorTexture[] Vertices { get; }

        internal int[] Indices { get; }

        internal int TileCount => this.Vertices.Length / 4;

        internal int TriangleCount => this.Indices.Length / 3;
    }

    private readonly struct TileTexture {
        internal TileTexture(TileSheet sheet, Rectangle bounds) {
            this.Sheet = sheet;
            this.Bounds = bounds;
        }

        internal TileSheet Sheet { get; }

        internal Rectangle Bounds { get; }
    }

    private sealed class TexturedBuilder {
        internal TexturedBuilder(string imageSource) {
            this.ImageSource = imageSource;
        }

        internal string ImageSource { get; }

        internal List<VertexPositionColorTexture> Vertices { get; } = new();

        internal List<int> Indices { get; } = new();
    }
}
