using GameInterface.AutoSync;
using GameInterface.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
    void PatchAll();
    void PatchGameStarted();
    void UnpatchAll();
}

public class GameInterface : IGameInterface
{
    public const string HARMONY_STATIC_FIXES_CATEGORY = "HarmonyStaticFixes";
    public const string HARMONY_UI_LOADING_CATEGORY = "UILoadingPatches";

    // Applied at boot by CoopMod so it is active before native first-time minor-faction initialization
    public const string HARMONY_CONFIGURED_MINOR_FACTION_CATEGORY = "ConfiguredMinorFactionPatches";

    public const string HARMONY_GAME_STARTED_CATEGORY = "GameStartedPatches";

    private static bool gameStartedPatchesApplied;

    private readonly Harmony harmony;
    private readonly IAutoSyncPatchCollector patchCollector;
    private readonly AutoSyncPatcher AutoSyncPatcher;
    private readonly IEnumerable<HarmonyPatchCategoryRegistration> patchCategories;

    public GameInterface(
        Harmony harmony,
        IAutoSyncPatchCollector patchCollector,
        AutoSyncPatcher AutoSyncPatcher,
        IEnumerable<HarmonyPatchCategoryRegistration> patchCategories)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
        this.AutoSyncPatcher = AutoSyncPatcher;
        this.patchCategories = patchCategories;
    }

    public void Dispose()
    {
    }

    public void PatchAll()
    {
        if (Harmony.HasAnyPatches(harmony.Id))
        {
            // Reconnect skips the install below, so handlers torn down on disconnect must be rebound here.
            AutoSyncPatcher.RebindHandlers();
            return;
        }

        var assembly = typeof(GameInterface).Assembly;

        // Must run before any other detour below, or a fragile no-op method's detour can corrupt its inline
        // x64 unwind info and deadlock the GC.
        FragileDetourGuard.Apply(harmony);

        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);

        Services.Issues.Generic.QuestTypeBootstrap.EnsureAllMigratedTypesRegistered();

        foreach (HarmonyPatchCategoryRegistration patchCategory in patchCategories)
        {
            patchCategory.Apply(harmony);
        }

        AutoSyncPatcher.PatchAll();
    }

    public void PatchGameStarted()
    {
        if (gameStartedPatchesApplied) return;

        harmony.PatchCategory(typeof(GameInterface).Assembly, HARMONY_GAME_STARTED_CATEGORY);
        if (AutoSyncPatcher.Assembly != null)
            harmony.PatchCategory(AutoSyncPatcher.Assembly, HARMONY_GAME_STARTED_CATEGORY);
        gameStartedPatchesApplied = true;
    }

    public void UnpatchAll()
    {
        // Disabled: container disposal relies on patches staying live through teardown.
        return;

        patchCollector.UnpatchAll();
        harmony.UnpatchAll();
    }
}
