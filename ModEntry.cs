using StardewModdingAPI;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    internal static ModConfig Config { get; private set; } = null!;

    public override void Entry(IModHelper helper) {
        Log.Init(this.Monitor);

        Config = helper.ReadConfig<ModConfig>();

        var configPath = Path.Combine(helper.DirectoryPath, "config.json");
        if (!File.Exists(configPath)) {
            helper.WriteConfig(Config);
            Log.Info($"Created default config at {configPath}.");
        }

        Log.Info($"3D Valley {this.ModManifest.Version} loaded.");
    }
}
