using HarmonyLib;
using System;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
}

public class GameInterface : IGameInterface
{
    private const string HarmonyId = "TaleWorlds.MountAndBlade.Bannerlord.Coop";
    private readonly Harmony harmony;
    public GameInterface()
    {
        if (Harmony.HasAnyPatches(HarmonyId)) return;

        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(typeof(GameInterface).Assembly);
    }

    public void Dispose()
    {
        harmony?.UnpatchAll(HarmonyId);
    }
}
