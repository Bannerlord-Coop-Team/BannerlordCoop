using GameInterface.Surrogates;
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
    private readonly ISurrogateCollection surrogateCollection;

    public GameInterface(Harmony harmony, ISurrogateCollection surrogateCollection)
    {
        this.harmony = harmony;
        this.surrogateCollection = surrogateCollection;
    }

    public void Dispose()
    {
        UnpatchAll();
    }

    public void PatchAll()
    {
        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(GameInterfaceModule.HarmonyId)) return;

        var assembly = typeof(GameInterface).Assembly;
        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);
    }

    public void UnpatchAll()
    {
        harmony?.UnpatchAll(GameInterfaceModule.HarmonyId);
    }
}
