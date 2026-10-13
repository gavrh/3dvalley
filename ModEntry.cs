using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using ThreeDValley.Renderer;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    internal static ModConfig Config { get; private set; } = null!;

    private readonly FreeCamera _camera = new();

    private Texture2D? _pixel;
    private RenderTarget2D? _sceneTarget;
    private SceneRenderer? _sceneRenderer;
    private RenderView? _renderView;
    private int _sceneWidth;
    private int _sceneHeight;
    private bool _compositeFailed;
    private bool _skipNextLook;

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
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

        helper.Events.GameLoop.SaveLoaded += (_, _) => MapInspector.LogCurrentLocation("save loaded");
        helper.Events.Player.Warped += (_, _) => MapInspector.LogCurrentLocation("location changed");
        helper.ConsoleCommands.Add("3dvalley_map", "Log the current location's map, layers, tilesheets, and a tile-property sample.", (_, _) => MapInspector.LogCurrentLocation("command"));
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

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e) {
        if (e.Button != Config.ViewToggleKey)
            return;
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null || Game1.IsChatting)
            return;

        Config.Enabled = !Config.Enabled;
        this.Helper.WriteConfig(Config);
        Log.Info($"3D Valley view {(Config.Enabled ? "enabled" : "disabled")}.");
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e) {
        if (this.IsCameraActive())
            this.UpdateCamera();
    }

    private bool IsCameraActive() {
        return Config.Enabled
            && Context.IsWorldReady
            && Game1.currentLocation is not null
            && Game1.activeClickableMenu is null
            && !Game1.IsChatting;
    }

    private void UpdateCamera() {
        float elapsedSeconds = (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;

        float forward = (this.Helper.Input.IsDown(SButton.W) ? 1f : 0f) - (this.Helper.Input.IsDown(SButton.S) ? 1f : 0f);
        float strafe = (this.Helper.Input.IsDown(SButton.D) ? 1f : 0f) - (this.Helper.Input.IsDown(SButton.A) ? 1f : 0f);
        float vertical = (this.Helper.Input.IsDown(SButton.Space) ? 1f : 0f)
            - ((this.Helper.Input.IsDown(SButton.LeftControl) || this.Helper.Input.IsDown(SButton.RightControl)) ? 1f : 0f);
        this._camera.Move(forward, strafe, vertical, elapsedSeconds);

        Point center = GetMouseCenter();
        if (this._skipNextLook) {
            this._skipNextLook = false;
        }
        else {
            MouseState current = Mouse.GetState();
            this._camera.Look(current.X - center.X, current.Y - center.Y);
        }
        Game1.setMousePositionRaw(center.X, center.Y);
    }

    private static Point GetMouseCenter() {
        Rectangle bounds = Game1.game1.Window.ClientBounds;
        return new Point(bounds.Width / 2, bounds.Height / 2);
    }

    private void DrawComposite(SpriteBatch batch) {
        GraphicsDevice device = Game1.graphics.GraphicsDevice;
        if (!this.EnsureSceneTarget(device))
            return;

        RenderView view = this._renderView!;
        view.View = this._camera.GetView();

        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport previousViewport = device.Viewport;

        try {
            batch.End();
            device.SetRenderTarget(view.RenderTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.CornflowerBlue, 1f, 0);

            this._sceneRenderer ??= new SceneRenderer(device);
            this._sceneRenderer.Draw(view);
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
            this._sceneRenderer?.Dispose();
            this._sceneRenderer = null;
            this._renderView = null;
            this._sceneTarget?.Dispose();
            this._sceneTarget = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.DiscardContents);
            this._renderView = new RenderView(this._sceneTarget, new Viewport(this._sceneTarget.Bounds));
            this._sceneWidth = width;
            this._sceneHeight = height;
            Log.Debug($"Created composite render target {width}x{height}.");
            return true;
        }
        catch (Exception ex) {
            this._sceneTarget = null;
            this._renderView = null;
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
