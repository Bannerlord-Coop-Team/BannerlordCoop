using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Independently-found ownership gap (the task's own instruction to check for one on this quest's completion/
/// turn-in path - confirmed present, matching the shape found in every Group A quest type built so far except
/// one): <c>GetPoacherPartyDialogFlow()</c> is registered UNCONDITIONALLY from <c>SetDialogs()</c> (called from
/// the ctor AND <c>InitializeQuestOnGameLoad()</c>) as <c>QuestCharacterDialogFlow</c> - live on EVERY peer's own
/// mirror, not just the accepter's (Category B). Its own top-level condition
/// (<c>SetStartDialogOnCondition()</c>: <c>this._poachersParty != null &amp;&amp; CharacterObject.OneToOneConversationCharacter
/// == ConversationHelper.GetConversationCharacterPartyLeader(this._poachersParty.Party)</c>) is NOT owner-
/// specific - now that <see cref="MerchantArmyOfPoachersPartySpawnGatePatch"/> correctly force-mirrors the real,
/// globally-visible <c>_poachersParty</c> onto every peer, ANY connected player who physically walks up to and
/// talks to that shared party leader can reach this dialogue, exactly the same reachability shape
/// <c>GangLeaderNeedsWeaponsQuestOwnershipGatePatch</c>'s own doc comment found for its guard dialogue.
///
/// Two convergent leaf methods, both reached only via <c>Campaign.Current.ConversationManager.ConversationEndOneShot</c>
/// subscriptions set inside this dialogue's own Consequence delegates - gating them closes both paths at their
/// one real mutation point, matching this session's established "gate the actual mutation, not each
/// registration/condition site" rule:
///
/// - <c>QuestSuccessPlayerComesToAnAgreementWithPoachers()</c> - reached after a successful persuasion minigame
///   (<c>persuasion_complete_with_poachers_on_consequence</c>). Without this gate, ANY non-owner who talks to
///   the shared poachers-party leader and wins the persuasion minigame would give THEMSELVES the real reward
///   gold/renown/relation AND genuinely complete (<c>CompleteQuestWithSuccess()</c>) someone else's
///   already-accepted quest.
/// - <c>QuestFailedAfterTalkingWithPoachers()</c> - reached after picking "Well... you have a point... Go on."
///   Without this gate, ANY non-owner could unilaterally FAIL the true owner's quest
///   (<c>CompleteQuestWithFail()</c>, relation/power/security/prosperity penalties applied against the real
///   quest giver) just by talking to the shared NPC.
///
/// The "give up or die" option only sets the local, harmless <c>_talkedToPoachersBattleWillStart</c> flag - not
/// gated here, since the only thing that flag can ever trigger (<c>StartQuestBattle()</c>) is already gated by
/// <see cref="MerchantArmyOfPoachersBattleStartApprovalPatches"/>, and the flag itself is only ever read by
/// <c>army_of_poachers_village_on_init</c>, which (per design doc §5 Category A) never meaningfully runs on a
/// non-owner's own mirror anyway.
///
/// The battle-outcome paths (<c>QuestSuccessWithPlayerDefeatedPoachers</c>/
/// <c>QuestFailWithPlayerDefeatedAgainstPoachers</c>) are deliberately NOT gated here either - both are reached
/// ONLY from <c>army_of_poachers_village_on_init</c>'s own poll of THIS machine's local
/// <c>PlayerEncounter.Current</c>/<c>PlayerEncounter.Battle</c>, itself only reachable via the same Category-A
/// menu-switch chain - no Category B (dialogue) entry point exists for either, same reasoning already
/// established for <c>CaravanAmbushQuestOwnershipGatePatch</c>'s own non-gated failure paths.
/// </summary>
[HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest))]
internal class MerchantArmyOfPoachersOwnershipGatePatches
{
    [HarmonyPatch("QuestSuccessPlayerComesToAnAgreementWithPoachers")]
    [HarmonyPrefix]
    private static bool QuestSuccessPlayerComesToAnAgreementWithPoachersPrefix(
        MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestFailedAfterTalkingWithPoachers")]
    [HarmonyPrefix]
    private static bool QuestFailedAfterTalkingWithPoachersPrefix(
        MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
