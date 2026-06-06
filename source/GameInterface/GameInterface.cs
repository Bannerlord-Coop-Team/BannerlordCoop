using GameInterface.AutoSync;
using GameInterface.DynamicSync;
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
    private readonly DynamicSyncPatcher dynamicSyncPatcher;

    public GameInterface(Harmony harmony, IAutoSyncPatchCollector patchCollector, DynamicSyncPatcher dynamicSyncPatcher)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
        this.dynamicSyncPatcher = dynamicSyncPatcher;
    }

    public void Dispose()
    {
    }

    public void PatchAll()
    {
        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(harmony.Id)) return;

        var assembly = typeof(GameInterface).Assembly;

        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);
        dynamicSyncPatcher.PatchAll();
    }

    public void UnpatchAll()
    {
        patchCollector.UnpatchAll();
        // Only remove Coop's own patches. The parameterless Harmony.UnpatchAll() is a global
        // unpatch that strips every patch in the process — the base game's and other mods'
        // included — which crashes the game when a player leaves a coop session.
        harmony.UnpatchAll(harmony.Id);
    }
}
