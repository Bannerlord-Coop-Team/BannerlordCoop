using Common;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.CapturedByBountyHunters;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueOwnershipRegistry), nameof(IssueOwnershipRegistry.SetOwner))]
internal class BountyHuntersRecordJournalOwnerPatch
{
    [HarmonyPostfix]
    private static void Record(Hero issueGiver, string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (issueGiver?.Issue is CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue
            && ContainerProvider.TryResolve<IBountyHuntersJournalOwners>(out var journals))
        {
            journals.Record(issue, controllerId);
        }
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class BountyHuntersJournalOwnershipPersistencePatch
{
    [HarmonyPostfix]
    private static void SyncData(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IBountyHuntersJournalOwners>(out var journals)) journals.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal class BountyHuntersLoadedJournalFilterPatch
{
    [HarmonyPrefix]
    private static void Filter()
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IBountyHuntersJournalOwners>(out var journals))
        {
            journals.FilterLoadedCampaign();
        }
    }
}
