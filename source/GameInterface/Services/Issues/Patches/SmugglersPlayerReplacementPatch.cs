using Common;
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

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.RemovePlayer))]
internal class SmugglersPlayerRemovalPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerManager __instance, Player player)
    {
        if (ModInformation.IsServer && player != null
            && __instance.TryGetPlayer(player.ControllerId, out var registered) && ReferenceEquals(registered, player)
            && ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority))
            authority.OnPlayerRemoving(player);
    }
}
