using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures this accept's authoritative <c>_difficultyMultiplier</c>/<c>RewardGold</c> - see
/// <see cref="ILandlordTrainingForRetainersIssueInterface"/>'s doc comment. Same shape/reasoning as
/// <see cref="VillageNeedsCraftingMaterialsQuestAcceptancePatch"/>, own independent postfix.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LandlordTrainingForRetainersQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue) return;

        if (!ContainerProvider.TryResolve<ILandlordTrainingForRetainersIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var difficultyMultiplier, out var rewardGold)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new LandlordTrainingIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, difficultyMultiplier, rewardGold));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/>'s doc comment for
/// the general derivation, though this is the ambient-tick shape rather than the dialogue-gate shape): gates
/// the QUEST's two private completion-checking methods (<c>CheckFail</c>/<c>CheckSuccess</c>) to the recorded
/// owner only. Both are invoked from SEVERAL different <c>CampaignEvents</c> listeners
/// (<c>HourlyTick</c>/<c>OnPlayerBattleEnd</c>/<c>OnTroopsDeserted</c>/<c>OnPlayerUpgradedTroops</c>/
/// <c>OnSettlementLeft</c>/<c>OnTroopGivenToSettlement</c>) - every one of them reads the LOCAL
/// <c>PartyBase.MainParty</c>'s roster, so a non-owner's own mirrored quest object (which never received the
/// borrowed troops in the first place - <c>QuestAcceptedConsequences</c> only ever runs on the genuine
/// accepter's own machine) would otherwise see 0 of both troop types on its very FIRST tick and immediately
/// (and incorrectly) call <c>Fail()</c> - a real, immediate desync/false-cancel this project cannot ship.
/// Gating these two shared choke points (rather than each of the six listener methods separately) closes this
/// with one patch pair.
/// </summary>
[HarmonyPatch(typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest))]
internal class LandlordTrainingForRetainersOwnershipGatePatches
{
    [HarmonyPatch("CheckFail")]
    [HarmonyPrefix]
    private static bool CheckFailPrefix(
        LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest __instance, ref bool __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch("CheckSuccess")]
    [HarmonyPrefix]
    private static bool CheckSuccessPrefix(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
