using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side creation of a
/// <see cref="ProdigalSonIssueBehavior.ProdigalSonIssue"/> - see <see cref="IProdigalSonIssueInterface"/>'s doc
/// comment. Own independent postfix, same reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class ProdigalSonIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not ProdigalSonIssueBehavior.ProdigalSonIssue prodigalSonIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new ProdigalSonIssueCreated(prodigalSonIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): <c>ProdigalSonIssueQuest</c> has FOUR distinct genuine, player-interaction-driven completion
/// paths the survey didn't separately enumerate - a scripted <c>MissionFightHandler.StartCustomFight</c> against
/// the house thugs (<c>FinishQuestSuccess1</c> on a win, <c>FinishQuestFail2</c> on a loss), a persuasion
/// mini-game against the gang leader (<c>FinishQuestSuccess3</c>), and paying the debt outright
/// (<c>FinishQuestSuccess4</c>) - all reachable by ANY peer who walks into the shared/mirrored reserved house
/// location and interacts with the shared/mirrored <c>_targetHero</c>/<c>_prodigalSon</c> NPCs, exactly the same
/// "anyone can turn in someone else's accepted quest" shape as every other type here. Gates all four
/// reward-applying methods to the recorded owner only. <c>FinishQuestFail1</c> (ambient timeout, symmetric
/// across peers, only a relation penalty - same shape as every other type's unguarded <c>OnTimedOut</c>) is
/// deliberately NOT gated, matching precedent.
///
/// Per the survey's "no accept-time RNG" claim: verified true for the QUEST/accept path itself (see
/// <see cref="IProdigalSonIssueInterface"/>'s doc comment for the two CREATION-time rolls the survey actually
/// missed instead). The one per-client-divergent value that DOES flow into the quest at accept time -
/// <c>IssueDifficultyMultiplier</c>, captured into <c>_questDifficulty</c> and used only to pick which of three
/// gangster-tier rosters <c>SpawnThugsInHouse</c> spawns in the mission - is a purely cosmetic divergence
/// (different enemy composition for whichever peer's own local mission actually runs the fight), never read
/// anywhere that affects reward/success/failure determination (that's decided solely by
/// <c>HouseFightFinished(isPlayerSideWon)</c>/persuasion outcome). Accepted as-is, identical reasoning to
/// <see cref="VillageNeedsToolsIssueInterface"/>'s own documented <c>_issueDifficultyMultiplier</c> trade-off.
/// </summary>
[HarmonyPatch(typeof(ProdigalSonIssueBehavior.ProdigalSonIssueQuest))]
internal class ProdigalSonOwnershipGatePatches
{
    [HarmonyPatch("FinishQuestSuccess1")]
    [HarmonyPrefix]
    private static bool FinishQuestSuccess1Prefix(ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FinishQuestSuccess3")]
    [HarmonyPrefix]
    private static bool FinishQuestSuccess3Prefix(ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FinishQuestSuccess4")]
    [HarmonyPrefix]
    private static bool FinishQuestSuccess4Prefix(ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("FinishQuestFail2")]
    [HarmonyPrefix]
    private static bool FinishQuestFail2Prefix(ProdigalSonIssueBehavior.ProdigalSonIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
