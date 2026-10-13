using StardewValley;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace ThreeDValley;

internal static class MapInspector {
    private static readonly string[] SampledProperties = { "Water", "Passable", "Diggable", "Type", "Action" };
    private const int MaxTilesPerLayer = 4096;

    internal static void LogCurrentLocation(string reason) {
        GameLocation? location = Game1.currentLocation;
        if (location is null) {
            Log.Debug($"Map report ({reason}): no current location (title screen); skipped.");
            return;
        }

        Log.Info($"Map report ({reason}): {location.NameOrUniqueName}, outdoors: {location.IsOutdoors}.");

        Map? map = location.Map;
        if (map is null) {
            Log.Warn($"Map report ({reason}): location '{location.NameOrUniqueName}' has no map.");
            return;
        }

        int tilesWide = map.Layers.Count > 0 ? map.Layers[0].LayerWidth : 0;
        int tilesHigh = map.Layers.Count > 0 ? map.Layers[0].LayerHeight : 0;
        Log.Info($"  size: {tilesWide}x{tilesHigh} tiles, {map.DisplayWidth}x{map.DisplayHeight} px; {map.Layers.Count} layers, {map.TileSheets.Count} tilesheets.");

        foreach (Layer layer in map.Layers)
            Log.Info($"  layer '{layer.Id}': {layer.LayerWidth}x{layer.LayerHeight} tiles, {layer.DisplayWidth}x{layer.DisplayHeight} px, visible: {layer.Visible}.");

        if (map.TileSheets.Count == 0) {
            Log.Warn("  no tilesheets on this map.");
            return;
        }

        foreach (TileSheet tileSheet in map.TileSheets)
            Log.Info($"  tilesheet '{tileSheet.Id}': {tileSheet.SheetWidth}x{tileSheet.SheetHeight} tiles, tile {tileSheet.TileWidth}x{tileSheet.TileHeight} px, image '{tileSheet.ImageSource}'.");

        LogTileProperties(location, map);
    }

    private static void LogTileProperties(GameLocation location, Map map) {
        var found = new Dictionary<string, string>();
        bool truncated = false;

        foreach (Layer layer in map.Layers) {
            if (found.Count == SampledProperties.Length)
                break;

            int tileCount = layer.LayerWidth * layer.LayerHeight;
            int scanned = 0;

            for (int y = 0; y < layer.LayerHeight && scanned < MaxTilesPerLayer; y++) {
                for (int x = 0; x < layer.LayerWidth && scanned < MaxTilesPerLayer; x++) {
                    scanned++;

                    foreach (string property in SampledProperties) {
                        if (found.ContainsKey(property))
                            continue;

                        string? value = location.doesTileHaveProperty(x, y, property, layer.Id, false);
                        if (value is not null)
                            found[property] = $"{value} at ({x},{y}) {layer.Id}";
                    }
                }
            }

            if (scanned == MaxTilesPerLayer && tileCount > MaxTilesPerLayer)
                truncated = true;
        }

        Log.Info("  tile properties (first sample per property):");
        foreach (string property in SampledProperties)
            Log.Info(found.TryGetValue(property, out string? sample) ? $"    {property}={sample}" : $"    {property}=<none>");
        if (truncated)
            Log.Info($"    (sampled at most {MaxTilesPerLayer} tiles per layer)");
    }
}
