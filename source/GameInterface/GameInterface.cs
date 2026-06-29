using GameInterface.AutoSync;
using GameInterface.Utils;
using HarmonyLib;
using System;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
    void PatchAll();
    void UnpatchAll();
}

public class GameInterface : IGameInterface
{
    public const string HARMONY_STATIC_FIXES_CATEGORY = "HarmonyStaticFixes";
    
    private readonly Harmony harmony;
    private readonly IAutoSyncPatchCollector patchCollector;
    private readonly AutoSyncPatcher AutoSyncPatcher;

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
            // Patches persist for the whole process (UnpatchAll is disabled), but the AutoSync apply-handlers
            // are container-scoped and were torn down on disconnect. The patch install below is skipped on a
            // reconnect, so rebind the handlers onto the new container here or every synced update is dropped.
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
        AutoSyncPatcher.PatchAll();
    }

    public void UnpatchAll()
    {
        // Unpatching is disabled due to double ctor patch bug
        return;

        patchCollector.UnpatchAll();
        harmony.UnpatchAll();
    }
}
