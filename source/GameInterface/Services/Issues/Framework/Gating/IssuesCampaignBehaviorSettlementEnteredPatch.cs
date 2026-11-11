using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Framework.Gating;

// Stops a player from ending issues as an AI lord
[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnSettlementEntered))]
internal class IssuesCampaignBehaviorSettlementEnteredPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(MobileParty party)
    {
        if (ModInformation.IsClient)
        {
            return false;
        }

        return party == null || !party.IsPlayerParty();
    }
}
