using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side <c>IssueManager.CreateNewIssue</c> creating an instance of any
/// <see cref="SimpleIssueFactoryRegistry"/>-registered issue type. Deliberately its own, independent
/// postfix-only class (same reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>):
/// <see cref="IssueManagerCreateNewIssuePatches"/>'s client-creation-blocking Prefix is already fully generic
/// (gates ANY <c>IssueManager.CreateNewIssue</c> call regardless of type), so only a second postfix is needed
/// here to also capture/broadcast these types' (payload-less) creation.
/// </summary>
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
