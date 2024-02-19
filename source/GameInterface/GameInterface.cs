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
    private const string HarmonyId = "TaleWorlds.MountAndBlade.Bannerlord.Coop";
    private Harmony harmony;

    public void Dispose()
    {
        UnpatchAll();
    }

    public void PatchAll()
    {
        // NOTE: Patching in constructor causes issues with tests and CI
        if (Harmony.HasAnyPatches(HarmonyId)) return;

        var assembly = typeof(GameInterface).Assembly;

        harmony = new Harmony(HarmonyId);
        harmony.PatchCategory(assembly, HARMONY_STATIC_FIXES_CATEGORY);
        harmony.PatchAllUncategorized(assembly);
    }

    public void UnpatchAll()
    {
        harmony?.UnpatchAll(HarmonyId);
    }
}
