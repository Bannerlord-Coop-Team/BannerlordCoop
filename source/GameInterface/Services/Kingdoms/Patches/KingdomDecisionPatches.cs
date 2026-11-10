using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using Common;
using System.Linq;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomDecision))]
internal class KingdomDecisionPatches
{
    [HarmonyPatch(nameof(KingdomDecision.NeedsPlayerResolution), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool NeedsPlayerResolutionPrefix(KingdomDecision __instance, ref bool __result)
    {
        if (ModInformation.IsClient
            && ContainerProvider.TryResolve<IKingdomDecisionVoteManager>(out var voteManager)
            && voteManager.IsSubmittedDecision(__instance))
        {
            // MapScreen otherwise reopens an overdue vote while the server waits for other clans.
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(KingdomDecision.ShouldBeCancelled))]
    [HarmonyPrefix]
    private static bool ShouldBeCancelledPrefix(KingdomDecision __instance, ref bool __result)
    {
        if (__instance.Kingdom.IsEliminated)
        {
            __result = true;
            return false;
        }
        if (__instance.ProposerClan.Kingdom != __instance.Kingdom)
        {
            __result = true;
            return false;
        }
        if (!__instance.IsAllowed())
        {
            __result = true;
            return false;
        }
        if (__instance.ShouldBeCancelledInternal())
        {
            __result = true;
            return false;
        }
        if (__instance.ProposerClan.IsPlayerClan())
        {
            __result = false;
            return false;
        }
        MBList<DecisionOutcome> mblist = __instance.NarrowDownCandidates(__instance.DetermineInitialCandidates().ToMBList<DecisionOutcome>(), 3);
        DecisionOutcome queriedDecisionOutcome = __instance.GetQueriedDecisionOutcome(mblist);
        __instance.DetermineSponsors(mblist);
        Supporter.SupportWeights supportWeights;
        DecisionOutcome decisionOutcome = __instance.DetermineSupportOption(new Supporter(__instance.ProposerClan), mblist, out supportWeights, true);
        bool flag = __instance.ProposerClan.Influence < (float)__instance.GetInfluenceCostOfSupport(__instance.ProposerClan, Supporter.SupportWeights.SlightlyFavor) * 1.5f;
        bool flag2 = mblist.Any((DecisionOutcome t) => t.SponsorClan != null && t.SponsorClan.IsEliminated);
        bool flag3 = supportWeights == Supporter.SupportWeights.StayNeutral || decisionOutcome == null;
        bool flag4 = decisionOutcome != queriedDecisionOutcome || (decisionOutcome == queriedDecisionOutcome && flag3);
        __result =  flag2 || (mblist.Any((DecisionOutcome t) => t.SponsorClan == __instance.ProposerClan) && !flag && ((!__instance.CanProposerClanChangeOpinion() && flag4) || (__instance.CanProposerClanChangeOpinion() && flag3)));
        return false;
    }
}
