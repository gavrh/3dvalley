using StardewModdingAPI;

namespace ThreeDValley;

public sealed class ModEntry : Mod {
    public override void Entry(IModHelper helper) {
        helper.Events.GameLoop.GameLaunched += (_, _) => {
            Monitor.Log("3D Valley loaded.", LogLevel.Info);
        };
    }
}
