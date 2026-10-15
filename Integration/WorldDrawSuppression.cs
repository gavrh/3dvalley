using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using StardewValley;

namespace ThreeDValley.Integration;

internal static class WorldDrawSuppression {
    private const string HarmonyId = "gavrh.3dvalley.world-draw-suppression";

    private static Func<bool>? drawSuppressedWorld;
    private static bool timingVanilla;
    private static long vanillaStartTimestamp;

    internal static bool IsApplied { get; private set; }

    internal static double LastVanillaDrawMs { get; private set; }

    internal static bool TryApply(Func<bool> drawSuppressedWorld) {
        WorldDrawSuppression.drawSuppressedWorld = drawSuppressedWorld;

        try {
            MethodInfo? target = AccessTools.Method(typeof(Game1), nameof(Game1.DrawWorld));
            if (target is null) {
                Log.Warn("World draw suppression unavailable: Game1.DrawWorld was not found; the vanilla world will keep drawing under the composite.");
                return false;
            }

            var harmony = new Harmony(HarmonyId);
            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(WorldDrawSuppression), nameof(Prefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(WorldDrawSuppression), nameof(Postfix))));

            IsApplied = true;
            Log.Info($"World draw suppression patch applied to {target.DeclaringType?.FullName}.{target.Name}.");
            return true;
        }
        catch (Exception ex) {
            Log.Warn($"World draw suppression unavailable: the patch could not be applied; the vanilla world will keep drawing under the composite. {ex}");
            return false;
        }
    }

    private static bool Prefix() {
        Func<bool>? draw = drawSuppressedWorld;
        if (draw is not null && draw()) {
            timingVanilla = false;
            return false;
        }

        vanillaStartTimestamp = Stopwatch.GetTimestamp();
        timingVanilla = true;
        return true;
    }

    private static void Postfix() {
        if (!timingVanilla)
            return;

        timingVanilla = false;
        LastVanillaDrawMs = (Stopwatch.GetTimestamp() - vanillaStartTimestamp) * 1000.0 / Stopwatch.Frequency;
    }
}
