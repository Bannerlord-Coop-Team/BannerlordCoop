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

        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(typeof(GameInterface).Assembly);
    }

    public void UnpatchAll()
    {
        harmony?.UnpatchAll(HarmonyId);
    }
}
