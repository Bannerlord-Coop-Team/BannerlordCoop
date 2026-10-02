using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ReplacePlayer))]
internal class SmugglersPlayerReplacementPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player registeredPlayer, Player replacementPlayer, bool __result)
    {
        if (__result && ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority))
            authority.OnPlayerReplaced(registeredPlayer, replacementPlayer);
    }
}
