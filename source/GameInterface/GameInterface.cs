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

    // Applied at boot by CoopMod, not by PatchAll: the loading-window keepalive must already be live while a host waits on PatchAll itself
    public const string HARMONY_UI_LOADING_CATEGORY = "UILoadingPatches";

    // Applied from CoopMod.OnGameStart because these patches compile types whose initializers require Game.Current
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
        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(harmony.Id))
        {
            // The patch install below is skipped on reconnect, so rebind the torn-down AutoSync handlers onto
            // the new container here (see RebindHandlers) or every synced update is dropped.
            AutoSyncPatcher.RebindHandlers();
            return;
        }

        var assembly = typeof(GameInterface).Assembly;

        // Install the unwind-corruption guard BEFORE any detour below so it covers every patch we apply
        // (AutoSync, hand-written, and the registry's constructor patches): it skips the 5-byte detour write
        // only for tiny "fragile no-op" methods whose detour would corrupt their inline x64 unwind info and
        // deadlock the GC.
        FragileDetourGuard.Apply(harmony);

        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);

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
        // Unpatching is disabled due to double ctor patch bug. CoopartiveMultiplayerExperience.DestroyContainer
        // relies on patches staying live through container disposal (it clears ContainerProvider before disposing
        // instead of unpatching) - if this is ever re-enabled, revisit that ordering.
        return;

        patchCollector.UnpatchAll();
        harmony.UnpatchAll();
    }
}
