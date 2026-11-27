using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Utils;
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

    // Vanilla only leaves MainParty out, so an AI army entering a friendly fortification moved troops out of attached player parties
    [HarmonyPatch(nameof(GarrisonTroopsCampaignBehavior.CalculateMobilePartiesIdealPartySizes))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CalculateMobilePartiesIdealPartySizesTranspiler(IEnumerable<CodeInstruction> instructions)
        => MainPartyComparisonTranspiler.ReplaceMainPartyComparisonsWithIsPlayerParty(instructions);
}
