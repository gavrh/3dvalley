using StardewModdingAPI;

namespace ThreeDValley;

public sealed class ModConfig {
    public bool Enabled { get; set; } = true;

    public SButton ViewToggleKey { get; set; } = SButton.F5;

    public bool ShowDebugOverlay { get; set; } = false;

    public SButton DebugOverlayKey { get; set; } = SButton.F6;
}
