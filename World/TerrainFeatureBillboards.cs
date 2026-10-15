using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.TerrainFeatures;
using ThreeDValley.Renderer;

namespace ThreeDValley;

internal static class TerrainFeatureBillboards {
    private const float PixelsPerTile = 64f;
    private const float SpriteScale = 4f;

    private const string GrassTexture = "TerrainFeatures\\grass";
    private const string BushTexture = "TileSheets\\bushes";
    private const string CropFallbackTexture = "TileSheets\\crops";
    private const string FruitTreeFallbackTexture = "TileSheets\\fruitTrees";
    private const string CursorsTexture = "LooseSprites\\Cursors";

    private const int TreeYoungStage = 5;
    private const int FruitTreeYoungStage = 4;

    internal static IReadOnlyList<Billboard> Build(GameLocation location, TerrainMesh terrain, SpriteResolver sprites, out TerrainFeatureBillboardStats stats) {
        var billboards = new List<Billboard>(location.terrainFeatures.Length + location.largeTerrainFeatures.Count);
        int features = 0;
        int skipped = 0;

        foreach (KeyValuePair<Vector2, TerrainFeature> pair in location.terrainFeatures.Pairs) {
            int before = billboards.Count;
            BuildFeature(pair.Value, pair.Key, location, terrain, sprites, billboards);
            if (billboards.Count > before)
                features++;
            else
                skipped++;
        }

        foreach (LargeTerrainFeature feature in location.largeTerrainFeatures) {
            int before = billboards.Count;
            BuildLargeFeature(feature, terrain, sprites, billboards);
            if (billboards.Count > before)
                features++;
            else
                skipped++;
        }

        stats = new TerrainFeatureBillboardStats(features, skipped);
        return billboards;
    }

    private static void BuildFeature(TerrainFeature feature, Vector2 tile, GameLocation location, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        switch (feature) {
            case HoeDirt dirt:
                BuildCrop(dirt, tile, terrain, sprites, billboards);
                break;
            case CosmeticPlant:
                break;
            case Grass grass:
                BuildGrass(grass, tile, location, terrain, sprites, billboards);
                break;
            case FruitTree fruitTree:
                BuildFruitTree(fruitTree, tile, terrain, sprites, billboards);
                break;
            case Tree tree:
                BuildTree(tree, tile, terrain, sprites, billboards);
                break;
        }
    }

    private static void BuildLargeFeature(LargeTerrainFeature feature, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        if (feature is Bush bush)
            BuildBush(bush, terrain, sprites, billboards);
    }

    private static void BuildCrop(HoeDirt dirt, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        Crop? crop = dirt.crop;
        if (crop is null)
            return;

        string textureName = crop.forageCrop.Value
            ? CursorsTexture
            : crop.overrideTexturePath.Value ?? CropFallbackTexture;
        Texture2D? texture = sprites.Resolve(textureName);
        if (texture is null)
            return;

        int tileX = (int)tile.X;
        int tileY = (int)tile.Y;
        Rectangle source = crop.getSourceRect(tileX * 7 + tileY * 11);
        if (source.Width <= 0 || source.Height <= 0)
            return;

        Vector2 drawPosition = crop.drawPosition;
        if (drawPosition == Vector2.Zero)
            drawPosition = new Vector2(tileX * 64f + 32f, tileY * 64f + 32f);

        float originY = crop.forageCrop.Value ? 8f : 24f;
        var baseCenter = new Vector3(
            drawPosition.X / PixelsPerTile,
            terrain.HeightAt(tileX, tileY),
            (drawPosition.Y + (source.Height - originY) * SpriteScale) / PixelsPerTile);

        billboards.Add(new Billboard(baseCenter, source, texture));
    }

    private static void BuildGrass(Grass grass, Vector2 tile, GameLocation location, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        Texture2D? texture = sprites.Resolve(grass.textureName());
        if (texture is null)
            return;

        int tileX = (int)tile.X;
        int tileY = (int)tile.Y;
        int sourceOffset = grass.grassSourceOffset.Value;

        var random = Utility.CreateRandom(Game1.uniqueIDForThisGame, (double)Game1.stats.DaysPlayed / 28.0, (double)tileX * 7.0, (double)tileY * 11.0);
        bool front = location.hasTileAt(tileX, tileY, "Front");

        int bladeCount = grass.numberOfWeeds.Value;
        for (int i = 0; i < bladeCount; i++) {
            int whichWeed = random.Next(3);
            _ = random.Next(-2, 3);
            _ = random.Next(-2, 3);
            int offset3 = random.Next(-2, 3);
            int offset4 = random.Next(-2, 3) + (front ? -7 : 0);

            var source = new Rectangle(whichWeed * 15, sourceOffset, 15, 20);
            var globalPosition = new Vector2(
                tileX * 64f + i % 2 * 32f + offset3 * 4f - 4f + 30f,
                tileY * 64f + i / 2 * 32f + offset4 * 4f + 40f);

            var baseCenter = new Vector3(
                globalPosition.X / PixelsPerTile,
                terrain.HeightAt(tileX, tileY),
                (globalPosition.Y + 10f) / PixelsPerTile);

            billboards.Add(new Billboard(baseCenter, source, texture));
        }
    }

    private static void BuildTree(Tree tree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        int growthStage = tree.growthStage.Value;
        if (growthStage >= TreeYoungStage || tree.stump.Value)
            return;

        string? textureName = tree.TextureName;
        if (textureName is null) {
            _ = tree.texture.Value;
            textureName = tree.TextureName;
        }

        Texture2D? texture = sprites.Resolve(textureName);
        if (texture is null)
            return;

        Rectangle source = growthStage switch {
            0 => new Rectangle(32, 128, 16, 16),
            1 => new Rectangle(0, 128, 16, 16),
            2 => new Rectangle(16, 128, 16, 16),
            _ => new Rectangle(0, 96, 16, 32),
        };

        var baseCenter = new Vector3(
            (tile.X * 64f + 32f) / PixelsPerTile,
            terrain.HeightAt((int)tile.X, (int)tile.Y),
            (tile.Y * 64f + 64f) / PixelsPerTile);

        billboards.Add(new Billboard(baseCenter, source, texture));
    }

    private static void BuildFruitTree(FruitTree fruitTree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        int growthStage = fruitTree.growthStage.Value;
        if (growthStage >= FruitTreeYoungStage)
            return;

        string textureName = fruitTree.textureName ?? FruitTreeFallbackTexture;
        Texture2D? texture = sprites.Resolve(textureName);
        if (texture is null)
            return;

        int row = fruitTree.GetSpriteRowNumber();
        Rectangle source = growthStage switch {
            0 => new Rectangle(0, row * 5 * 16, 48, 80),
            1 => new Rectangle(48, row * 5 * 16, 48, 80),
            2 => new Rectangle(96, row * 5 * 16, 48, 80),
            _ => new Rectangle(144, row * 5 * 16, 48, 80),
        };

        float wobble = Math.Clamp(MathF.Sin(tile.X * 200f / (MathF.PI * 2f)) * -16f, -8f, 64f) / 2f;
        var baseCenter = new Vector3(
            (tile.X * 64f + 32f + wobble) / PixelsPerTile,
            terrain.HeightAt((int)tile.X, (int)tile.Y),
            (tile.Y * 64f + 48f + wobble) / PixelsPerTile);

        billboards.Add(new Billboard(baseCenter, source, texture));
    }

    private static void BuildBush(Bush bush, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        Rectangle source = bush.sourceRect.Value;
        if (source.Width <= 0 || source.Height <= 0)
            return;

        Texture2D? texture = sprites.Resolve(BushTexture);
        if (texture is null)
            return;

        Vector2 tile = bush.Tile;
        int effectiveSize = bush.size.Value switch {
            3 => 0,
            4 => 1,
            int size => size,
        };

        float positionX = tile.X * 64f + (effectiveSize + 1) * 64f / 2f;
        float positionY = (tile.Y + 1f) * 64f - (effectiveSize > 0 && (!bush.townBush.Value || effectiveSize != 1) && bush.size.Value != 4 ? 64f : 0f);
        float originX = (effectiveSize + 1) * 16f / 2f;
        const float originY = 32f;

        var baseCenter = new Vector3(
            (positionX + (source.Width / 2f - originX) * SpriteScale) / PixelsPerTile,
            terrain.HeightAt((int)tile.X, (int)tile.Y),
            (positionY + (source.Height - originY) * SpriteScale) / PixelsPerTile);

        billboards.Add(new Billboard(baseCenter, source, texture));
    }
}

internal readonly struct TerrainFeatureBillboardStats {
    internal TerrainFeatureBillboardStats(int featureCount, int skippedCount) {
        this.FeatureCount = featureCount;
        this.SkippedCount = skippedCount;
    }

    internal int FeatureCount { get; }

    internal int SkippedCount { get; }
}
