using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    internal static ModConfig Config { get; private set; } = null!;

    private Texture2D? _pixel;
    private RenderTarget2D? _sceneTarget;
    private int _sceneWidth;
    private int _sceneHeight;
    private bool _compositeFailed;

    public override void Entry(IModHelper helper) {
        Log.Init(this.Monitor);

        Config = helper.ReadConfig<ModConfig>();

        var configPath = Path.Combine(helper.DirectoryPath, "config.json");
        if (!File.Exists(configPath)) {
            helper.WriteConfig(Config);
            Log.Info($"Created default config at {configPath}.");
        }

        Log.Info($"3D Valley {this.ModManifest.Version} loaded.");

        helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e) {
        if (Game1.currentLocation is null)
            return;

        if (Config.Enabled && !this._compositeFailed) {
            try {
                this.DrawComposite(e.SpriteBatch);
            }
            catch (Exception ex) {
                this._compositeFailed = true;
                Log.Error($"Disabling the composite after a draw error: {ex}");
            }
        }

        this.DrawMarker(e.SpriteBatch);
    }

    private void DrawComposite(SpriteBatch batch) {
        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        if (!this.EnsureSceneTarget(device))
            return;

        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport previousViewport = device.Viewport;

        try {
            batch.End();
            device.SetRenderTarget(this._sceneTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.CornflowerBlue, 0f, 0);
        }
        finally {
            this.RestoreRenderTarget(device, previousTargets);
            device.Viewport = previousViewport;
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        Rectangle destination = previousTargets.Length > 0 && previousTargets[0].RenderTarget is RenderTarget2D target
            ? target.Bounds
            : new Rectangle(0, 0, device.PresentationParameters.BackBufferWidth, device.PresentationParameters.BackBufferHeight);
        batch.Draw(this._sceneTarget, destination, Color.White);
    }

    private bool EnsureSceneTarget(GraphicsDevice device) {
        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        if (width <= 0 || height <= 0)
            return false;

        if (this._sceneTarget is not null && !this._sceneTarget.IsDisposed && this._sceneWidth == width && this._sceneHeight == height)
            return true;

        try {
            this._sceneTarget?.Dispose();
            this._sceneTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);
            this._sceneWidth = width;
            this._sceneHeight = height;
            Log.Debug($"Created composite render target {width}x{height}.");
            return true;
        }
        catch (Exception ex) {
            this._sceneTarget = null;
            this._sceneWidth = 0;
            this._sceneHeight = 0;
            Log.Error($"Failed to create the composite render target: {ex}");
            return false;
        }
    }

    private void RestoreRenderTarget(GraphicsDevice device, RenderTargetBinding[] previousTargets) {
        if (previousTargets.Length == 0)
            device.SetRenderTarget(null);
        else
            device.SetRenderTargets(previousTargets);
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
