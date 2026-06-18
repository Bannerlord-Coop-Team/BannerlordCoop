using GameInterface.AutoSync;
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
        if (Harmony.HasAnyPatches(harmony.Id)) return;

        var assembly = typeof(GameInterface).Assembly;

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
