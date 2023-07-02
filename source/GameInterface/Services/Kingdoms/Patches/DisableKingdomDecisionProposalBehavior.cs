using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomDecisionProposalBehavior))]
internal class DisableKingdomDecisionProposalBehavior
{
    [HarmonyPatch(nameof(KingdomDecisionProposalBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
