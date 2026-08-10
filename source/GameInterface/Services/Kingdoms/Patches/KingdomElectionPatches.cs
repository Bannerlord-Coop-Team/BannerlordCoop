using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch]
internal class KingdomElectionPatches
{
    [HarmonyPatch(typeof(KingdomDecision), nameof(KingdomDecision.NarrowDownCandidates))]
    [HarmonyPrefix]
    private static bool NarrowDownCandidatesPrefix(KingdomDecision __instance, ref MBList<DecisionOutcome> __result)
    {
        if (__instance is not SettlementClaimantDecision claimantDecision) return true;
        if (!ContainerProvider.TryResolve<ISettlementClaimantSnapshotRegistry>(out var snapshotRegistry)) return true;
        if (!snapshotRegistry.TryCreateOutcomes(claimantDecision, out MBList<DecisionOutcome> outcomes)) return true;

        __result = outcomes;
        return false;
    }

    [HarmonyPatch(typeof(KingdomDecision), nameof(KingdomDecision.NarrowDownCandidates))]
    [HarmonyPostfix]
    private static void NarrowDownCandidatesPostfix(KingdomDecision __instance, MBList<DecisionOutcome> __result)
    {
        if (__instance is not SettlementClaimantDecision claimantDecision) return;
        if (!ContainerProvider.TryResolve<ISettlementClaimantSnapshotRegistry>(out var snapshotRegistry)) return;

        snapshotRegistry.Capture(claimantDecision, __result);
    }

    [HarmonyPatch(typeof(KingdomElection), nameof(KingdomElection.OnPlayerSupport))]
    [HarmonyPrefix]
    private static bool Prefix(KingdomElection __instance, DecisionOutcome decisionOutcome, Supporter.SupportWeights supportWeight)
    {
        bool isLocalPlayerChooser = __instance._chooser == Clan.PlayerClan;

        if (!isLocalPlayerChooser)
        {
            foreach (DecisionOutcome outcome in __instance._possibleOutcomes)
            {
                outcome.ResetSupport(__instance.PlayerAsSupporter);
            }
            __instance._hasPlayerVoted = true;
            if (decisionOutcome != null)
            {
                __instance.PlayerAsSupporter.SupportWeight = supportWeight;
                decisionOutcome.AddSupport(__instance.PlayerAsSupporter);
            }
        }
        else
        {
            __instance._chosenOutcome = decisionOutcome;
        }

        return false;
    }
    [HarmonyPatch(typeof(KingdomElection), nameof(KingdomElection.ApplySelection))]
    [HarmonyPrefix]
    private static bool Prefix(KingdomElection __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!KingdomDecisionsVMPatches.TryGetVoteManager(out var voteManager)) return true;

        KingdomDecision decision = __instance?._decision;
        if (decision == null) return true;

        // already handled via the normal voting UI pipeline elsewhere;
        // block the native re-apply
        if (voteManager.HasLocalPlayerSubmittedVote(decision)) return false;

        bool published = voteManager.TryPublishFinalVoteForElection(__instance);
        return !published; // fall back to native if we couldn't route it
    }
}
