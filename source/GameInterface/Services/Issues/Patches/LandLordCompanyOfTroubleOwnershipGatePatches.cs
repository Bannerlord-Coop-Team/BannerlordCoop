using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Independently-found ownership gaps (the task's own instruction to check for one on this quest's completion/
/// turn-in surfaces - confirmed present, matching the shape found in every Group A quest type built so far).
/// Both gaps share the same root cause: <c>_companyTroopCount</c> is re-derived LIVE from
/// <c>MobileParty.MainParty.MemberRoster</c> (via <c>UpdateCompanyTroopCount()</c>) on WHICHEVER machine happens
/// to run the listener/dialogue - and since a non-owner's own mirror of this quest genuinely exists and is
/// <c>IsOngoing</c> (this type rides the fully generic <c>GenericAcceptMirrorIssueTypes</c> accept-mirror, which
/// DOES call the real <c>IssueManager.StartIssueQuest</c>/<c>StartQuest()</c> on every peer - see
/// <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment), its listeners are genuinely wired
/// and DO run - they just always see <c>_companyTroopCount == 0</c> (their own MainParty never received the
/// embedded-troop injection, since <c>QuestAcceptedConsequences()</c> only ever ran on the true accepter's
/// machine). A count of exactly 0 is not "nothing happens" for either of these two methods - it's the SUCCESS
/// condition:
///
/// 1. <c>OnMapEventEnded(MapEvent)</c> - a <c>RegisterEvents()</c>-wired, unconditional listener (Category A, but
///    NOT self-limiting the way this session's usual "state never populated outside the accepter" reasoning
///    predicts - the very thing that reasoning relies on, <c>_companyTroopCount == 0</c> on a non-owner, is
///    EXACTLY this method's own success trigger). Without this gate: the FIRST time ANY connected peer's own
///    local party finishes ANY unrelated map event (<c>mapEvent.IsPlayerMapEvent || mapEvent.IsPlayerSimulation</c>),
///    <c>UpdateCompanyTroopCount()</c> finds 0 embedded troops (true for every non-owner, always), and the
///    method immediately awards the REAL <c>RewardGold</c> to that non-owner's own <c>Hero.MainHero</c> AND
///    genuinely completes (<c>CompleteQuestWithSuccess()</c>) the true owner's already-accepted quest out from
///    under them - reachable with zero deliberate interaction, not even a dialogue click.
///
/// 2. <c>persuasion_complete_with_company_of_trouble_on_consequence()</c> - reached via
///    <c>GetOtherLordsDialogFlow()</c> (registered globally, unconditionally, from <c>SetDialogs()</c>, live on
///    every peer's own mirror - Category B, no ownership check anywhere in the chain up to and including
///    <c>PersuasionDialogForLordGeneralCondition</c>/<c>PersuasionDialogSpecialCondition</c>) - ANY connected peer
///    who talks to ANY OTHER qualifying lord (not the quest giver, not their own clan, not at war, not already
///    having the same issue type) can enter the "sell the mercenaries" persuasion minigame and, on success,
///    <c>_demandGold</c> (computed from THEIR OWN <c>_companyTroopCount</c>, i.e. 1000 + 0*150 = 1000 for a
///    non-owner) is paid to THEM, and the true owner's quest is genuinely completed
///    (<c>CompleteQuestWithSuccess()</c>) - a second, independent, purely-dialogue-driven way to steal someone
///    else's quest reward.
///
/// Both gated with a simple one-liner Prefix, matching this session's own established precedent shape (e.g.
/// <c>MerchantArmyOfPoachersOwnershipGatePatches</c>). Known, accepted, documented trade-off (same shape as every
/// other simple one-liner gate in this family): blocking <c>persuasion_complete_with_company_of_trouble_on_consequence()</c>
/// outright also skips its own first two lines (<c>PlayerEncounter.LeaveEncounter</c>/
/// <c>ConversationManager.EndPersuasion()</c>), so a non-owner who reaches (and wins) this persuasion minigame
/// keeps their own local UI in a lightly stuck state rather than getting a clean exit - a purely local,
/// non-security, minor UX rough edge affecting only the peer attempting to exploit someone else's quest, not the
/// true owner or anyone else; not worth a bespoke partial-body reimplementation for.
/// </summary>
[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest))]
internal class LandLordCompanyOfTroubleOwnershipGatePatches
{
    [HarmonyPatch("OnMapEventEnded")]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix(
        LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __instance, MapEvent mapEvent) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("persuasion_complete_with_company_of_trouble_on_consequence")]
    [HarmonyPrefix]
    private static bool PersuasionCompletePrefix(
        LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
