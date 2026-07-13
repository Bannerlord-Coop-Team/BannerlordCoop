using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.GameState.Patches;

/// <summary>
/// Defers patches whose target types read <see cref="Game.Current"/> from their static initializers.
/// </summary>
[HarmonyPatch(typeof(GameTextManager), nameof(GameTextManager.LoadGameTexts))]
internal static class GameTextManagerPatches
{
    [HarmonyPostfix]
    private static void LoadGameTextsPostfix(GameTextManager __instance)
    {
        if (Game.Current?.GameTextManager != __instance) return;
        if (!ContainerProvider.TryResolve<IGameInterface>(out var gameInterface)) return;

        gameInterface.PatchGameReadyPatches();
    }
}
