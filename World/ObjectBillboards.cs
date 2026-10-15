using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using ThreeDValley.Renderer;
using Object = StardewValley.Object;

namespace ThreeDValley;

internal static class ObjectBillboards {
    private const float PixelsPerTile = 64f;

    internal static IReadOnlyList<Billboard> Build(GameLocation location, TerrainMesh terrain, SpriteResolver sprites, out ObjectBillboardStats stats) {
        var billboards = new List<Billboard>(location.Objects.Length + location.furniture.Count);
        int skipped = 0;
        int fences = 0;

        foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
            Object entity = pair.Value;
            if (entity is Fence) {
                fences++;
                continue;
            }

            Billboard? billboard = BuildObject(entity, terrain, sprites);
            if (billboard is null)
                skipped++;
            else
                billboards.Add(billboard);
        }

        foreach (Furniture entity in location.furniture) {
            Billboard? billboard = BuildFurniture(entity, terrain, sprites);
            if (billboard is null)
                skipped++;
            else
                billboards.Add(billboard);
        }

        if (fences > 0) {
            skipped += fences;
            Log.Warn($"{fences} fences skipped: their sprite depends on neighbouring fence pieces (deferred).");
        }

        stats = new ObjectBillboardStats(billboards.Count, skipped);
        return billboards;
    }

    private static Billboard? BuildObject(Object entity, TerrainMesh terrain, SpriteResolver sprites) {
        ParsedItemData data = ItemRegistry.GetDataOrErrorItem(entity.QualifiedItemId);
        Rectangle source = data.GetSourceRect(0, entity.ParentSheetIndex);
        Texture2D? texture = sprites.Resolve(data.TextureName);
        if (texture is null || source.Width <= 0 || source.Height <= 0)
            return null;

        int tileX = (int)entity.TileLocation.X;
        int tileY = (int)entity.TileLocation.Y;
        int left = tileX * 64;
        int top = entity.bigCraftable.Value ? tileY * 64 - 64 : tileY * 64;
        int width = source.Width * 4;
        int height = source.Height * 4;

        var baseCenter = new Vector3(
            (left + width / 2f) / PixelsPerTile,
            terrain.HeightAt(tileX, tileY),
            (top + height) / PixelsPerTile);

        return new Billboard(baseCenter, source, texture);
    }

    private static Billboard? BuildFurniture(Furniture entity, TerrainMesh terrain, SpriteResolver sprites) {
        ParsedItemData data = ItemRegistry.GetDataOrErrorItem(entity.QualifiedItemId);
        Rectangle source = entity.sourceRect.Value;
        Texture2D? texture = sprites.Resolve(data.TextureName);
        if (texture is null || source.Width <= 0 || source.Height <= 0)
            return null;

        Rectangle bounds = entity.boundingBox.Value;
        int tileX = (int)entity.TileLocation.X;
        int tileY = (int)entity.TileLocation.Y;
        var baseCenter = new Vector3(
            (bounds.X + source.Width * 2f) / PixelsPerTile,
            terrain.HeightAt(tileX, tileY),
            bounds.Bottom / PixelsPerTile);

        return new Billboard(baseCenter, source, texture);
    }
}

internal readonly struct ObjectBillboardStats {
    internal ObjectBillboardStats(int objectCount, int skippedCount) {
        this.ObjectCount = objectCount;
        this.SkippedCount = skippedCount;
    }

    internal int ObjectCount { get; }

    internal int SkippedCount { get; }
}
