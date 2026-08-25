using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueManager))]
internal class SimpleIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (!SimpleIssueFactoryRegistry.IsRegistered(issueOwner?.Issue)) return;

        MessageBroker.Instance.Publish(issueOwner, new SimpleIssueCreated(issueOwner.Issue));
    }
}
