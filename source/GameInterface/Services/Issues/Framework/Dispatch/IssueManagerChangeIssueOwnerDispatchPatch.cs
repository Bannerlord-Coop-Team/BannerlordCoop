using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.OwnerChange;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.ChangeIssueOwner))]
internal class IssueManagerChangeIssueOwnerDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue, out Hero __state)
    {
        __state = issue.IssueOwner;

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        // The server moves issues, clients only move the ones the server announced
        return ModInformation.IsServer;
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, Hero __state)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed())
        {
            return;
        }

        MessageBroker.Instance.Publish(issue, new IssueOwnerChanged(issue, __state));
    }
}
