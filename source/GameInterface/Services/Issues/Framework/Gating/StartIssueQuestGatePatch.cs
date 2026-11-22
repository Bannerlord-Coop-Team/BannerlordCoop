using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Gating;

/// <summary>
/// The accept line looks up the giver's issue, which can be gone by the time the player clicks it, for example moved
/// to a relative. Vanilla would throw there and leave the conversation waiting for an answer, so the player is told
/// and the conversation closes.
/// </summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.StartIssueQuest))]
internal class StartIssueQuestGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueManager __instance, Hero issueOwner, ref bool __result)
    {
        if (ModInformation.IsServer) return true;

        if ((issueOwner != null && __instance.Issues.ContainsKey(issueOwner)) ||
            !ContainerProvider.TryResolve<IIssueConversationGuard>(out var conversationGuard))
        {
            return true;
        }

        conversationGuard.EndUnavailableAccept(issueOwner);
        __result = false;
        return false;
    }
}
