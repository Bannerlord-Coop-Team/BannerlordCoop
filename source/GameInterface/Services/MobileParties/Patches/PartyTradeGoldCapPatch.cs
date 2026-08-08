using Common;
using GameInterface.Configuration;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class PartyTradeGoldCapPatch
{
    private static readonly int PartyTradeGoldIncomeThreshold = 10000;

    [HarmonyPatch(nameof(MobileParty.PartyTradeGold), MethodType.Setter)]
    [HarmonyPostfix]
    public static void PartyTradeGoldSetterPostfix(MobileParty __instance)
    {
        if (__instance.PartyTradeGold <= PartyTradeGoldIncomeThreshold) return;

        if (ModInformation.IsClient) return;

        // Cap party trade gold for disconnected players based on config
        // When capped at PartyTradeGoldIncomeThreshold (10000), rejoining players won't see a spike in caravan/party profits
        if (ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers) return;

        if (ContainerProvider.TryResolve<IPlayerManager>(out IPlayerManager playerManager) == false) return;

        var owner = __instance.Owner;
        if (owner == null || !owner.IsPlayerHero()) return;

        if (playerManager.IsOwnerOfHeroDisconnected(owner))
        {
            __instance.PartyTradeGold = PartyTradeGoldIncomeThreshold;
        }
    }
}
