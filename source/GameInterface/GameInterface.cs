using GameInterface.AutoSync.Internal;
using HarmonyLib;
using System;
using System.Linq;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
    void PatchAll();
    void UnpatchAll();
}

public class GameInterface : IGameInterface
{
    public const string HARMONY_STATIC_FIXES_CATEGORY = "HarmonyStaticFixes";
    
    private Harmony harmony;
    private readonly IAutoSyncPatcher autoSyncPatcher;

    public GameInterface(Harmony harmony, IAutoSyncPatcher autoSyncPatcher)
    {
        this.harmony = harmony;
        this.autoSyncPatcher = autoSyncPatcher;
    }

    public void Dispose()
    {
        autoSyncPatcher.UnpatchAll();
        UnpatchAll();
    }

    public void PatchAll()
    {
        autoSyncPatcher.PatchAll();

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
