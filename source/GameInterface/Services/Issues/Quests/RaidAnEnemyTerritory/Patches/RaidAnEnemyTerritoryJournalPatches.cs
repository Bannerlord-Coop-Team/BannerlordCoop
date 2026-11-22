using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.UI.Notifications;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
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

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryJournalViewPatch
{
    [ThreadStatic] internal static bool IsBuildingJournal;

    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = IsBuildingJournal;
        IsBuildingJournal = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => IsBuildingJournal = __state;
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.IsOngoing), MethodType.Getter)]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryActiveJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, ref bool __result)
    {
        if (__result && ModInformation.IsClient && RaidAnEnemyTerritoryJournalViewPatch.IsBuildingJournal && __instance is Quest)
        {
            __result = IsLocalOwner(__instance);
        }
    }

    private static bool IsLocalOwner(QuestBase quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        return issue != null && ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) &&
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) &&
            ownerResolver.TryGetOwnerControllerId(issue, out var ownerController) &&
            ownerController == controller.ControllerId;
    }
}
