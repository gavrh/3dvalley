using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    internal static ModConfig Config { get; private set; } = null!;

    private Texture2D? _pixel;

    public override void Entry(IModHelper helper) {
        Log.Init(this.Monitor);

        Config = helper.ReadConfig<ModConfig>();

        var configPath = Path.Combine(helper.DirectoryPath, "config.json");
        if (!File.Exists(configPath)) {
            helper.WriteConfig(Config);
            Log.Info($"Created default config at {configPath}.");
        }

        Log.Info($"3D Valley {this.ModManifest.Version} loaded.");

        helper.Events.Display.RenderedWorld += (_, e) => this.DrawMarker(e.SpriteBatch);

        helper.Events.GameLoop.SaveLoaded += (_, _) => MapInspector.LogCurrentLocation("save loaded");
        helper.Events.Player.Warped += (_, _) => MapInspector.LogCurrentLocation("location changed");
        helper.ConsoleCommands.Add("3dvalley_map", "Log the current location's map, layers, tilesheets, and a tile-property sample.", (_, _) => MapInspector.LogCurrentLocation("command"));
    }

    private void DrawMarker(SpriteBatch batch) {
        if (Game1.currentLocation is null)
            return;

        this._pixel ??= CreatePixel();

        Vector2 anchor = Game1.player.Position;
        var box = new Rectangle((int)anchor.X - 32, (int)anchor.Y - 32, 64, 64);
        batch.Draw(this._pixel, box, Color.OrangeRed);

        const string label = "3D Valley marker";
        Vector2 textSize = Game1.smallFont.MeasureString(label);
        var textPos = new Vector2(box.Center.X - textSize.X / 2f, box.Bottom + 4);
        batch.Draw(this._pixel, new Rectangle((int)textPos.X - 4, (int)textPos.Y - 2, (int)textSize.X + 8, (int)textSize.Y + 4), Color.Black * 0.6f);
        batch.DrawString(Game1.smallFont, label, textPos, Color.White);
    }

    private static Texture2D CreatePixel() {
        var pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        return pixel;
    }
}
