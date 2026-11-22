using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.UI.Notifications;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

/// <summary>Keeps ownership on raid quest archives created by the accepting client.</summary>
[HarmonyPatch(typeof(JournalLogsCampaignBehavior), nameof(JournalLogsCampaignBehavior.CreateRelatedLog), new[] { typeof(IssueBase) })]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryJournalCreationPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, ref JournalLogEntry __result)
    {
        if (ModInformation.IsClient && issue.IssueQuest is Quest quest &&
            ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
        {
            __result = resolver.CreateOwnedEntry(__result, issue, quest);
        }
    }
}
