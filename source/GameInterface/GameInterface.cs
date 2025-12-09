using GameInterface.AutoSync;
using HarmonyLib;
using System;
using Common.Logging;

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
    private static readonly Serilog.ILogger Logger = LogManager.GetLogger<GameInterface>();

    public GameInterface(Harmony harmony, IAutoSyncPatchCollector patchCollector)
    {
        this.harmony = harmony;
        this.patchCollector = patchCollector;
    }

    public void Dispose()
    {
    }

    public void PatchAll()
    {
        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(GameInterfaceModule.HarmonyId))
        {
            Logger.Information("Harmony patches already present for {HarmonyId}", GameInterfaceModule.HarmonyId);
            return;
        }

        var assembly = typeof(GameInterface).Assembly;

        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);

        Logger.Information("Harmony patched GameInterface assembly categories and uncategorized patches");
        patchCollector.PatchAll();
        Logger.Information("AutoSyncPatchCollector patched dynamic sync patches");
    }

    public void UnpatchAll()
    {
        harmony.UnpatchAll();
    }
}
