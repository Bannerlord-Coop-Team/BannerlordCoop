using Common;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Patches;

[HarmonyPatch(typeof(Workshop))]
internal class WorkshopCapitalPatch
{
    [HarmonyPatch(nameof(Workshop.ChangeGold))]
    [HarmonyPostfix]
    public static void ChangeGoldPostfix(Workshop __instance)
    {
        if (__instance.Capital <= __instance.InitialCapital) return;

        if (ModInformation.IsClient) return;

        // Cap workshop capital for disconnected players based on config
        // When capped at InitialCapital (10000), rejoining players won't see a spike in workshop profits
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers) return;

        if (ContainerProvider.TryResolve<IPlayerManager>(out IPlayerManager playerManager) == false) return;

        var owner = __instance.Owner;
        if (owner == null || !owner.IsPlayerHero()) return;

        if (playerManager.IsOwnerOfHeroDisconnected(__instance.Owner))
        {
            __instance.Capital = __instance.InitialCapital;
        }
    }
}
