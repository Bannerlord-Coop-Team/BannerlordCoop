using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches.Disable;

[HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
internal class DisableGarrisonTroopsCampaignBehavior
{
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.OnSettlementEntered))]
    [HarmonyPrefix]
    public static bool OnSettlementEnteredPrefix(MobileParty mobileParty, Settlement settlement)
    {
        if (mobileParty == null)
            return true;

        return !mobileParty.IsPlayerParty() && !settlement.OwnerClan.IsPlayerClan();
    }

    // Vanilla only leaves out MainParty and Clan.PlayerClan, which are never a co-op player's on the server, and its newly conquered exception can stay open after AI captures, so player clan parties are dropped outright
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.CalculateMobilePartiesIdealPartySizes))]
    [HarmonyPostfix]
    private static void CalculateMobilePartiesIdealPartySizesPostfix(List<(MobileParty, int)> __result)
        => __result.RemoveAll(entry => entry.Item1.LeaderHero.Clan.IsPlayerClan());

    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.CollectPartyGarrisonTransferData))]
    [HarmonyPostfix]
    private static void CollectPartyGarrisonTransferDataPostfix(ref GarrisonTroopsCampaignBehavior.PartyGarrisonTransferDataArgs partyGarrisonTransferDataArgs)
    {
        if (partyGarrisonTransferDataArgs.MobileParty.LeaderHero.Clan.IsPlayerClan())
            partyGarrisonTransferDataArgs.IsLeavingTroopsToGarrison = false;
    }
}
