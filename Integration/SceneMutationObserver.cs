using System.Reflection;
using HarmonyLib;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace ThreeDValley.Integration;

internal static class SceneMutationObserver {
    private const string HarmonyId = "gavrh.3dvalley.scene-mutation-observers";

    private static readonly (Type Type, string Method)[] Targets = {
        (typeof(Crop), nameof(Crop.harvest)),
        (typeof(Grass), nameof(Grass.performToolAction)),
        (typeof(Grass), nameof(Grass.doCollisionAction)),
        (typeof(HoeDirt), nameof(HoeDirt.performToolAction)),
        (typeof(GameLocation), nameof(GameLocation.makeHoeDirt)),
        (typeof(Bush), nameof(Bush.shake)),
        (typeof(Tree), nameof(Tree.shake)),
        (typeof(Tree), nameof(Tree.performToolAction)),
        (typeof(FruitTree), nameof(FruitTree.shake)),
        (typeof(FruitTree), nameof(FruitTree.performToolAction)),
        (typeof(Object), nameof(Object.placementAction)),
        (typeof(Furniture), nameof(Furniture.placementAction)),
        (typeof(Object), nameof(Object.performRemoveAction)),
        (typeof(Furniture), nameof(Furniture.performRemoveAction)),
    };

    private static Action? markDirty;

    internal static bool IsApplied { get; private set; }

    internal static int PatchCount { get; private set; }

    internal static bool TryApply(Action markSceneDirty) {
        SceneMutationObserver.markDirty = markSceneDirty;

        try {
            MethodInfo? postfix = AccessTools.Method(typeof(SceneMutationObserver), nameof(Postfix));
            var harmony = new Harmony(HarmonyId);
            var patched = new List<string>();

            foreach ((Type type, string method) in Targets) {
                MethodInfo? target = AccessTools.Method(type, method);
                if (target is null) {
                    Log.Warn($"Scene mutation observer unavailable: {type.FullName}.{method} was not found; changes from it will only appear on the next day or location rebuild.");
                    continue;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                patched.Add($"{type.Name}.{method}");
            }

            PatchCount = patched.Count;
            IsApplied = PatchCount > 0;

            if (IsApplied)
                Log.Info($"Scene mutation observers applied to {PatchCount} targets: {string.Join(", ", patched)}.");
            else
                Log.Warn("Scene mutation observers unavailable: none of the targets were found; live changes will only appear on the next day or location rebuild.");

            return IsApplied;
        }
        catch (Exception ex) {
            Log.Warn($"Scene mutation observers unavailable: the patches could not be applied; live changes will only appear on the next day or location rebuild. {ex}");
            return false;
        }
    }

    private static void Postfix() {
        markDirty?.Invoke();
    }
}
