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
        if (__instance.Owner == null || !__instance.Owner.IsPlayerHero()) return;

        ContainerProvider.TryResolve<IPlayerManager>(out var playerManager);

        // Cap party trade gold for disconnected players based on config
        // When capped at PartyTradeGoldIncomeThreshold (10000), rejoining players won't see a spike in caravan/party profits
        if (ModInformation.IsServer
            && !ModConfigProvider.ModOptions.GoldFoodInfluenceChangeForDisconnectedPlayers
            && playerManager.IsOwnerOfHeroDisconnected(__instance.Owner)
            && __instance.PartyTradeGold > PartyTradeGoldIncomeThreshold)
        {
            __instance.PartyTradeGold = PartyTradeGoldIncomeThreshold;
        }
    }
}
