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

    public GameInterface(Harmony harmony,IAutoSyncPatchCollector patchCollector)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
    }

    public void Dispose()
    {
        UnpatchAll();
    }

    public void PatchAll()
    {
        // This needs to be here to patch all autosync at the beginning of all tests
        patchCollector.PatchAll();

        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(GameInterfaceModule.HarmonyId)) return;

        var assembly = typeof(GameInterface).Assembly;
        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);
    }

    public void UnpatchAll()
    {
        if (Harmony.HasAnyPatches(GameInterfaceModule.HarmonyId) == false) return;

        foreach (var patch in harmony.GetPatchedMethods())
        {
            harmony.Unpatch(patch, HarmonyPatchType.All, harmony.Id);
        }
    }
}
