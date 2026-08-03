using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue"/> - see
/// <see cref="ILordNeedsGarrisonTroopsIssueInterface"/>'s doc comment. Deliberately its own independent postfix
/// (same reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>): the client-creation-blocking
/// Prefix on <see cref="IssueManagerCreateNewIssuePatches"/> is already fully generic.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class LordNeedsGarrisonTroopsIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new LordNeedsGarrisonTroopsIssueCreated(issue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation), targeting a survey-missed SECOND dialogue location: <c>GetGarrisonCommanderDialogFlow()</c> is
/// wired via <c>SetDialogs()</c> (present on every peer's mirror, unconditionally), and its own
/// <c>PlayerGiveTroopsToGarrisonCommanderCondition</c> ClickableCondition checks THIS machine's OWN
/// <c>MobileParty.MainParty.MemberRoster</c> - not any owner-specific state. Reaching this dialogue requires
/// physically travelling to the same (shared, mirrored) <c>_settlement</c> and using the "Talk to the garrison
/// commander" menu option (itself globally registered, not gated behind quest-instance <c>RegisterEvents</c>),
/// so a non-owning peer who has enough of the requested troop type in their own party could otherwise deliver
/// them into someone else's issue and collect the real gold reward. The main quest-giver <c>DiscussDialogFlow</c>
/// has no state-mutating consequence at all (pure flavor branches), so this garrison-commander check is the
/// ONLY gate this issue type needs.
/// </summary>
[HarmonyPatch(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest), "PlayerGiveTroopsToGarrisonCommanderCondition")]
internal class LordNeedsGarrisonTroopsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest __instance, ref bool __result, out TextObject explanation)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            explanation = new TextObject("{=!}You don't have enough men.");
            return false; // skip the original - non-owners never see/can-select the option
        }

        explanation = null; // definite-assignment only - the original computes the real explanation
        return true; // real owner: run the real check unmodified
    }
}
