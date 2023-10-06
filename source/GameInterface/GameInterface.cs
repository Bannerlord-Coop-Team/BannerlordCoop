using HarmonyLib;
using System;

namespace GameInterface;

public interface IGameInterface : IDisposable
{
}

public class GameInterface : IGameInterface
{
    private const string HarmonyId = "com.TaleWorlds.MountAndBlade.Bannerlord.Coop";
    private readonly Harmony harmony = new Harmony(HarmonyId);
    public GameInterface()
    {
        harmony = new Harmony(HarmonyId);
        harmony.PatchAll(typeof(GameInterface).Assembly);
    }

    public void Dispose()
    {
        harmony.UnpatchAll(HarmonyId);
    }
}
