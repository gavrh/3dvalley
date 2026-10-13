using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    private Texture2D? _pixel;

    public override void Entry(IModHelper helper) {
        helper.Events.GameLoop.GameLaunched += (_, _) => {
            Monitor.Log("3D Valley loaded.", LogLevel.Info);
        };
        helper.Events.Display.RenderedWorld += (_, e) => this.DrawMarker(e.SpriteBatch);
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
