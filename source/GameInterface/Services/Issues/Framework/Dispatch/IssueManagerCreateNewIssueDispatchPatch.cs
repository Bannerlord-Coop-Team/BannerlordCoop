using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.CreationCapture;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CreateNewIssue))]
internal class IssueManagerCreateNewIssueDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        // The server creates issues, clients only build the ones the server announced
        if (ModInformation.IsClient)
        {
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient)
        {
            return;
        }

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return;
        }

        var issue = issueOwner.Issue;
        if (issue == null)
        {
            return;
        }

        MessageBroker.Instance.Publish(issue, new IssueCreated(issue));
    }
}
