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
    private static bool Prefix(PlayerManager __instance, Player player, ref bool __result, out bool __state)
    {
        __state = ModInformation.IsServer && GameThread.Instance.IsGameThread;
        if (ModInformation.IsClient || __state) return true;

        var removed = false;
        GameThread.Run(() => removed = __instance.RemovePlayer(player), blocking: true);
        __result = removed;
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(Player player, bool __result, bool __state)
    {
        // Only the successful inner game-thread call may finalize the removed player's commitments.
        if (__state && __result && ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority))
            authority.OnPlayerRemoved(player);
    }
}
