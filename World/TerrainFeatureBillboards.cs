using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.WildTrees;
using StardewValley.ItemTypeDefinitions;
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

    private const int TrunkLayer = 0;
    private const int CanopyLayer = 1;
    private const int FruitLayer = 2;

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
        if (tree.growthStage.Value < TreeYoungStage) {
            BuildYoungTree(tree, tile, terrain, sprites, billboards);
            return;
        }

        BuildMatureTree(tree, tile, terrain, sprites, billboards);
    }

    private static void BuildYoungTree(Tree tree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        int growthStage = tree.growthStage.Value;

        Texture2D? texture = sprites.Resolve(TreeTextureName(tree));
        if (texture is null)
            return;

        Rectangle source = growthStage switch {
            0 => new Rectangle(32, 128, 16, 16),
            1 => new Rectangle(0, 128, 16, 16),
            2 => new Rectangle(16, 128, 16, 16),
            _ => new Rectangle(0, 96, 16, 32),
        };

        billboards.Add(new Billboard(TileBaseCenter(tile, terrain), source, texture));
    }

    private static void BuildMatureTree(Tree tree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        if (tree.health.Value <= -99f)
            return;

        Texture2D? texture = sprites.Resolve(TreeTextureName(tree));
        if (texture is null)
            return;

        Vector3 baseCenter = TileBaseCenter(tile, terrain);
        bool moss = tree.hasMoss.Value;

        if (!tree.stump.Value) {
            WildTreeData? data = tree.GetData();
            Rectangle canopy = Tree.treeTopSourceRect;
            if (data is not null && ((data.UseAlternateSpriteWhenSeedReady && tree.hasSeed.Value)
                || (data.UseAlternateSpriteWhenNotShaken && !tree.wasShakenToday.Value)))
                canopy.X = 48;
            if (moss)
                canopy.X = 96;

            billboards.Add(new Billboard(baseCenter, canopy, texture, CanopyLayer));
        }

        Rectangle trunk = Tree.stumpSourceRect;
        if (moss)
            trunk.X += 96;

        billboards.Add(new Billboard(baseCenter, trunk, texture, TrunkLayer));
    }

    private static string? TreeTextureName(Tree tree) {
        _ = tree.texture.Value;
        return tree.TextureName;
    }

    private static void BuildFruitTree(FruitTree fruitTree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        if (fruitTree.growthStage.Value < FruitTreeYoungStage) {
            BuildYoungFruitTree(fruitTree, tile, terrain, sprites, billboards);
            return;
        }

        BuildMatureFruitTree(fruitTree, tile, terrain, sprites, billboards);
    }

    private static void BuildYoungFruitTree(FruitTree fruitTree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        int growthStage = fruitTree.growthStage.Value;

        Texture2D? texture = sprites.Resolve(fruitTree.textureName ?? FruitTreeFallbackTexture);
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

    private static void BuildMatureFruitTree(FruitTree fruitTree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        if (fruitTree.health.Value <= -99f)
            return;

        Texture2D? texture = sprites.Resolve(fruitTree.textureName ?? FruitTreeFallbackTexture);
        if (texture is null)
            return;

        int row = fruitTree.GetSpriteRowNumber();
        int rowTop = row * 5 * 16;
        int season = (int)fruitTree.GetCosmeticSeason();
        Vector3 baseCenter = TileBaseCenter(tile, terrain);

        if (!fruitTree.stump.Value) {
            var canopy = new Rectangle((12 + season * 3) * 16, rowTop, 48, 80);
            billboards.Add(new Billboard(baseCenter, canopy, texture, CanopyLayer));
        }

        var trunk = new Rectangle(384, rowTop + 48, 48, 32);
        billboards.Add(new Billboard(baseCenter, trunk, texture, TrunkLayer));

        AddFruitBillboards(fruitTree, tile, terrain, sprites, billboards);
    }

    private static void AddFruitBillboards(FruitTree fruitTree, Vector2 tile, TerrainMesh terrain, SpriteResolver sprites, List<Billboard> billboards) {
        bool struck = fruitTree.struckByLightningCountdown.Value > 0;
        int count = Math.Min(fruitTree.fruit.Count, 3);

        for (int i = 0; i < count; i++) {
            Item? fruit = fruitTree.fruit[i];
            if (fruit is null)
                continue;

            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(struck ? "(O)382" : fruit.QualifiedItemId);
            Rectangle source = data.GetSourceRect();
            Texture2D? texture = sprites.Resolve(data.TextureName);
            if (texture is null || source.Width <= 0 || source.Height <= 0)
                continue;

            Vector2 position = FruitPosition(tile, i);
            var baseCenter = new Vector3(
                (position.X + source.Width / 2f * SpriteScale) / PixelsPerTile,
                terrain.HeightAt((int)tile.X, (int)tile.Y),
                (position.Y + source.Height * SpriteScale) / PixelsPerTile);

            billboards.Add(new Billboard(baseCenter, source, texture, FruitLayer));
        }
    }

    private static Vector2 FruitPosition(Vector2 tile, int index) {
        return index switch {
            0 => new Vector2(tile.X * 64f - 64f + tile.X * 200f % 64f / 2f, tile.Y * 64f - 192f - tile.X % 64f / 3f),
            1 => new Vector2(tile.X * 64f + 32f, tile.Y * 64f - 256f + tile.X * 232f % 64f / 3f),
            _ => new Vector2(tile.X * 64f + tile.X * 200f % 64f / 3f, tile.Y * 64f - 160f + tile.X * 200f % 64f / 3f),
        };
    }

    private static Vector3 TileBaseCenter(Vector2 tile, TerrainMesh terrain) {
        return new Vector3(
            (tile.X * 64f + 32f) / PixelsPerTile,
            terrain.HeightAt((int)tile.X, (int)tile.Y),
            (tile.Y * 64f + 64f) / PixelsPerTile);
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
