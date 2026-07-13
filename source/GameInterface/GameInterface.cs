using GameInterface.AutoSync;
using GameInterface.Utils;
using HarmonyLib;
using System;
using TaleWorlds.Core;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
    void PatchAll();
    void PatchGameReadyPatches();
    void UnpatchAll();
}

public class GameInterface : IGameInterface
{
    public const string HARMONY_STATIC_FIXES_CATEGORY = "HarmonyStaticFixes";

    // These patches reference game types whose static initializers require Game.Current.GameTextManager.
    public const string HARMONY_GAME_READY_CATEGORY = "HarmonyGameReady";

    // Applied at boot by CoopMod, not by PatchAll: the loading-window keepalive must already be live while a host waits on PatchAll itself
    public const string HARMONY_UI_LOADING_CATEGORY = "UILoadingPatches";
    
    private readonly Harmony harmony;
    private readonly IAutoSyncPatchCollector patchCollector;
    private readonly AutoSyncPatcher AutoSyncPatcher;
    private static readonly object gameReadyPatchesLock = new();
    private static bool gameReadyPatchesApplied;

    public GameInterface(Harmony harmony, IAutoSyncPatchCollector patchCollector, AutoSyncPatcher AutoSyncPatcher)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
        this.AutoSyncPatcher = AutoSyncPatcher;
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
            PatchGameReadyPatchesIfGameTextManagerIsReady();
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
        PatchGameReadyPatchesIfGameTextManagerIsReady();
        AutoSyncPatcher.PatchAll();
    }

    public void PatchGameReadyPatches()
    {
        lock (gameReadyPatchesLock)
        {
            if (gameReadyPatchesApplied) return;

            harmony.PatchCategory(typeof(GameInterface).Assembly, HARMONY_GAME_READY_CATEGORY);
            gameReadyPatchesApplied = true;
        }
    }

    private void PatchGameReadyPatchesIfGameTextManagerIsReady()
    {
        if (Game.Current?.GameTextManager != null)
        {
            PatchGameReadyPatches();
        }
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
