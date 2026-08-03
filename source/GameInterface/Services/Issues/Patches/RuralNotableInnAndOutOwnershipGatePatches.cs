using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation), for the first SandBox.dll (not TaleWorlds.CampaignSystem.dll) issue type this project syncs -
/// see <see cref="DisableAllIssueBehaviorsExceptAllowlist"/>'s doc comment confirming the allowlist/dispatch
/// scan already covers the SandBox assembly unmodified, and <see cref="RuralNotableInnAndOutIssueBehavior"/>'s
/// own Publicize entry in GameInterface.csproj (already present, covering all of SandBox.dll) confirming private
/// members here are just as directly accessible as any TaleWorlds.CampaignSystem type.
///
/// <c>RuralNotableInnAndOutIssueQuest</c>'s turn-in is a board-game minigame reached via the (shared, mirrored)
/// tavern's Game Host NPC - a local Mission interaction ANY peer can walk up to and play, exactly the same
/// "anyone can turn in someone else's accepted quest" shape as every other type here. Gates the three real
/// outcome consequence methods (win the board game, lose it after however many tries, or pay 1000 denars
/// outright) to the recorded owner only, so a non-owner peer who separately visits the same tavern and plays
/// (or pays) can never collect/fail this issue's reward/relationship/loyalty consequences on someone else's
/// behalf. <c>OnTimedOut</c> (ambient, campaign-time-driven, symmetric across peers, no reward beyond a
/// relation/loyalty penalty applied once per genuine trigger the same way every other type's timeout is left
/// unguarded) is deliberately NOT gated here, matching precedent.
/// </summary>
[HarmonyPatch(typeof(RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest))]
internal class RuralNotableInnAndOutOwnershipGatePatches
{
    [HarmonyPatch("PlayerWonTheBoardGame")]
    [HarmonyPrefix]
    private static bool PlayerWonTheBoardGamePrefix(
        RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerFailAfterBoardGame")]
    [HarmonyPrefix]
    private static bool PlayerFailAfterBoardGamePrefix(
        RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerPaid1000QuestSuccess")]
    [HarmonyPrefix]
    private static bool PlayerPaid1000QuestSuccessPrefix(
        RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
