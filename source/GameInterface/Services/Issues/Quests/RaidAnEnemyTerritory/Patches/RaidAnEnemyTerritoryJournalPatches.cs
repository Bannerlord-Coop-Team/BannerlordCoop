using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.SaveSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

public sealed class RaidAnEnemyTerritoryJournalLog : JournalLogEntry
{
    [SaveableField(100)]
    public readonly string ControllerId;

    public RaidAnEnemyTerritoryJournalLog(IssueBase issue, string controllerId)
        : base(issue.IssueQuest.Title, issue.IssueOwner, null, false, issue, issue.IssueQuest)
    {
        ControllerId = controllerId;
    }
}

public sealed class RaidAnEnemyTerritoryJournalSaveableTypeDefiner : SaveableTypeDefiner
{
    public RaidAnEnemyTerritoryJournalSaveableTypeDefiner() : base(44_378_700)
    {
    }

    public override void DefineClassTypes() => AddClassDefinition(typeof(RaidAnEnemyTerritoryJournalLog), 1);
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), "CreateRelatedLog", new[] { typeof(IssueBase) })]
internal sealed class RaidAnEnemyTerritoryJournalCreationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue, ref JournalLogEntry __result)
    {
        if (issue.IssueQuest is not Quest)
        {
            return true;
        }

        __result = new RaidAnEnemyTerritoryJournalLog(issue, LocalControllerId(issue));
        return false;
    }

    private static string LocalControllerId(IssueBase issue)
    {
        if (ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) &&
            ownerResolver.TryGetOwnerControllerId(issue, out var controllerId))
        {
            return controllerId;
        }

        return ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) ? controller.ControllerId : null;
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
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

[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal sealed class RaidAnEnemyTerritoryCompletedJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        if (__result && ModInformation.IsClient && RaidAnEnemyTerritoryJournalViewPatch.IsBuildingJournal &&
            __instance is RaidAnEnemyTerritoryJournalLog log)
        {
            __result = ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) &&
                !string.IsNullOrEmpty(log.ControllerId) && log.ControllerId == controller.ControllerId;
        }
    }
}
