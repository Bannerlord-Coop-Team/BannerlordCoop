using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): the vanilla <c>quest_discuss</c> "Yes, I have brought you a few men." player option is gated
/// entirely by <c>CheckIfThereIsSuitablePrisonerInPlayer()</c> - a check against THIS machine's OWN
/// <c>MobileParty.MainParty.PrisonRoster</c>, not any owner-specific state. A non-owning peer who has captured
/// bandit prisoners themselves could walk up to the (mirrored) quest-giver and deliver THEIR OWN prisoners into
/// someone else's issue, advancing <c>_deliveredPrisonerCount</c>/collecting the live
/// <c>PrisonerRansomValue</c>-derived reward on their own copy and eventually calling the real
/// <c>CompleteQuestWithSuccess()</c>. Gating this one check closes the whole delivery-screen/turn-in chain,
/// since every other success path (<c>_playerReachedMaximumAmount</c>, <c>_deliveredPrisonerCount &gt;=
/// _requestedPrisonerCount</c>) is only ever reachable after passing through here first.
/// </summary>
[HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest), "CheckIfThereIsSuitablePrisonerInPlayer")]
internal class LandLordNeedsManualLaborersQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest __instance, ref bool __result)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false; // skip the original - non-owners can never appear to have a suitable prisoner
        }

        return true; // real owner: run the real check unmodified
    }
}

/// <summary>
/// Second dialogue location the survey missed entirely: when <c>IsQuestWithCounterOffer</c> (the quest-giver
/// has negative Mercy and their settlement has a headman), <c>SetDialogs()</c> ALSO wires a
/// <c>QuestCharacterDialogFlow</c> with the HEADMAN Notable (a different, real, walkable Hero than the quest
/// giver) whose player options can call <c>ShareTheProfit()</c> (relation change + <c>_shareProfit</c> flag) or
/// <c>QuestFailPlayerAcceptedCounterOffer()</c> (a real <c>CompleteQuestWithFail()</c> - a genuine, non-replay
/// completion). Since this dialogue flow is built unconditionally in the Quest ctor (present on every peer's
/// mirror, exactly like the main turn-in dialogue above) and its own Condition only checks
/// <c>Hero.OneToOneConversationHero == headman</c> (no ownership check), any peer who can physically reach the
/// same (shared, mirrored) headman Notable could trigger either consequence for an issue they don't own. Both
/// consequences are gated here directly rather than the dialogue's own Condition, since that's the simplest
/// single choke point covering both player-option branches.
/// </summary>
[HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest))]
internal class LandLordNeedsManualLaborersCounterOfferOwnershipGatePatches
{
    [HarmonyPatch("ShareTheProfit")]
    [HarmonyPrefix]
    private static bool ShareTheProfitPrefix(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestFailPlayerAcceptedCounterOffer")]
    [HarmonyPrefix]
    private static bool QuestFailPlayerAcceptedCounterOfferPrefix(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
