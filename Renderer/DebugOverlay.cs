using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace ThreeDValley.Renderer;

internal sealed class DebugOverlay {
    private const int FrameSampleCount = 60;
    private const double RefreshSeconds = 0.25;

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly float[] frameSeconds = new float[FrameSampleCount];
    private readonly List<string> lines = new();
    private int frameIndex;
    private int frameCount;
    private long lastTicks;
    private bool hasLastTick;
    private double sinceRefresh = RefreshSeconds;

    internal void SampleFrame() {
        long now = this.clock.ElapsedTicks;
        if (!this.hasLastTick) {
            this.hasLastTick = true;
            this.lastTicks = now;
            return;
        }

        float seconds = (now - this.lastTicks) / (float)Stopwatch.Frequency;
        this.lastTicks = now;
        if (seconds <= 0f)
            return;

        this.frameSeconds[this.frameIndex] = seconds;
        this.frameIndex = (this.frameIndex + 1) % FrameSampleCount;
        if (this.frameCount < FrameSampleCount)
            this.frameCount++;

        this.sinceRefresh += seconds;
    }

    internal void Tick(FreeCamera camera, TerrainMesh? terrain, SceneRenderer? renderer, double buildMs, int objectCount, int skippedCount, bool worldSuppressed, bool suppressionAvailable, double vanillaWorldMs) {
        if (this.sinceRefresh < RefreshSeconds && this.lines.Count > 0)
            return;

        this.sinceRefresh = 0;
        this.Build(camera, terrain, renderer, buildMs, objectCount, skippedCount, worldSuppressed, suppressionAvailable, vanillaWorldMs);
    }

    internal IReadOnlyList<string> Build(FreeCamera camera, TerrainMesh? terrain, SceneRenderer? renderer, double buildMs, int objectCount, int skippedCount, bool worldSuppressed, bool suppressionAvailable, double vanillaWorldMs) {
        float average = this.AverageFrameSeconds();
        float fps = average > 0f ? 1f / average : 0f;
        Vector3 position = camera.Position;

        this.lines.Clear();
        this.lines.Add($"FPS {fps:F1} ({average * 1000f:F2} ms)");
        this.lines.Add($"camera ({position.X:F1}, {position.Y:F1}, {position.Z:F1}) yaw {MathHelper.ToDegrees(camera.Yaw):F1} pitch {MathHelper.ToDegrees(camera.Pitch):F1}");
        if (terrain is null) {
            this.lines.Add("terrain none");
        }
        else {
            this.lines.Add($"terrain {terrain.TileCount} tiles, {terrain.VertexCount} verts, {terrain.TriangleCount} tris");
            this.lines.Add($"tilesheets {terrain.TilesheetCount}, billboards {(renderer is null ? "-" : renderer.BillboardCount.ToString())}, objects {objectCount}, sprites skipped {skippedCount}, draw calls {(renderer is null ? "-" : renderer.DrawCallCount.ToString())}, build {buildMs:F1} ms");
        }

        if (!suppressionAvailable)
            this.lines.Add("world vanilla (suppression unavailable)");
        else if (worldSuppressed)
            this.lines.Add($"world suppressed, vanilla world ~{vanillaWorldMs:F1} ms saved");
        else
            this.lines.Add($"world vanilla (suppression off), draw {vanillaWorldMs:F1} ms");

        return this.lines;
    }

    internal void Draw(SpriteBatch batch, Texture2D pixel) {
        if (this.lines.Count == 0)
            return;

        const int padding = 4;
        var position = new Vector2(8f, 8f);

        float width = 0f;
        foreach (string line in this.lines) {
            float lineWidth = Game1.smallFont.MeasureString(line).X;
            if (lineWidth > width)
                width = lineWidth;
        }

        float lineHeight = Game1.smallFont.LineSpacing;
        batch.Draw(pixel, new Rectangle((int)position.X - padding, (int)position.Y - padding, (int)width + padding * 2, (int)(lineHeight * this.lines.Count) + padding * 2), Color.Black * 0.6f);
        foreach (string line in this.lines) {
            batch.DrawString(Game1.smallFont, line, position, Color.White);
            position.Y += lineHeight;
        }
    }

    private float AverageFrameSeconds() {
        if (this.frameCount == 0)
            return 0f;

        float sum = 0f;
        for (int i = 0; i < this.frameCount; i++)
            sum += this.frameSeconds[i];

        return sum / this.frameCount;
    }
}
