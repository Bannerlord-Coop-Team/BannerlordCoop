using Common;
using HarmonyLib;
using GameInterface.Policies;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Keeps player ownership on the server archive used by OnQuestCompleted and OnIssueUpdated.
/// </summary>
[HarmonyPatch(typeof(JournalLogsCampaignBehavior))]
internal static class ServerQuestJournalPatches
{
    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.GetRelatedLog), typeof(IssueBase))]
    [HarmonyPostfix]
    private static void GetIssueLogPostfix(IssueBase issue, ref JournalLogEntry __result)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
            __result = resolver.RestoreOwner(__result, issue);
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.CreateRelatedLog), typeof(IssueBase))]
    [HarmonyPostfix]
    private static void CreateIssueLogPostfix(IssueBase issue, ref JournalLogEntry __result)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
        {
            __result = issue.IssueQuest == null
                ? resolver.CreateOwnedEntry(__result, issue)
                : resolver.CreateOwnedEntry(__result, issue, issue.IssueQuest);
        }
    }

    [HarmonyPatch(nameof(JournalLogsCampaignBehavior.CreateRelatedLog), typeof(QuestBase))]
    [HarmonyPostfix]
    private static void CreateQuestLogPostfix(QuestBase quest, ref JournalLogEntry __result)
    {
        if (ModInformation.IsServer && __result is not PlayerJournalLogEntry &&
            ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
        {
            __result = resolver.CreateOwnedEntry(__result, quest);
        }
    }
}

/// <summary>
/// The quest screen lists ended journal entries, including those restored from the server's save.
/// </summary>
[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal static class PlayerQuestJournalHistoryPatch
{
    [HarmonyPostfix]
    private static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        if (!__result || ModInformation.IsServer)
            return;

        if (__instance is PlayerJournalLogEntry entry &&
            ContainerProvider.TryResolve<IQuestJournalOwnerResolver>(out var resolver))
        {
            __result = resolver.IsVisible(entry);
        }
        else if (__instance.Title.GetID() == "mvzh0HVk" && !CallOriginalPolicy.IsOriginalAllowed() &&
            ContainerProvider.TryResolve<ITheConquestOfSettlementService>(out var conquest))
        {
            // Older conquest archives already have separately saved ownership.
            __result = conquest.IsVisible(__instance);
        }
    }
}
