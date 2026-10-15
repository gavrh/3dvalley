using Microsoft.Xna.Framework.Graphics;

namespace ThreeDValley;

internal sealed class SpriteResolver : IDisposable {
    private readonly Func<string, Texture2D> loadTexture;
    private readonly Dictionary<string, Texture2D?> textures = new();
    private readonly HashSet<string> loggedFailures = new();

    internal SpriteResolver(Func<string, Texture2D> loadTexture) {
        this.loadTexture = loadTexture;
    }

    internal int TextureCount => this.textures.Values.Count(texture => texture is not null);

    internal Texture2D? Resolve(string? textureName) {
        if (string.IsNullOrWhiteSpace(textureName)) {
            this.LogFailure("no texture name given");
            return null;
        }

        if (this.textures.TryGetValue(textureName, out Texture2D? cached))
            return cached;

        Texture2D? texture = null;
        try {
            texture = this.loadTexture(textureName);
            if (texture is null)
                this.LogFailure($"{textureName}: the loader returned no texture");
        }
        catch (Exception ex) {
            this.LogFailure($"{textureName}: {ex.Message}");
        }

        this.textures[textureName] = texture;
        return texture;
    }

    private void LogFailure(string cause) {
        if (this.loggedFailures.Add(cause))
            Log.Warn($"Sprite unavailable; skipping the entity. {cause}");
    }

    public void Dispose() {
        foreach (Texture2D? texture in this.textures.Values)
            texture?.Dispose();
        this.textures.Clear();
    }
}
