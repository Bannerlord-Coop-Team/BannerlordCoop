using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.ObjectSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal static class HeadmanHerdJournalVisibilityPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var quests = AccessTools.PropertyGetter(typeof(QuestManager), nameof(QuestManager.Quests));
        var issues = AccessTools.Field(typeof(IssueManager), nameof(IssueManager.Issues));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            string filter = null;
            if (instruction.Calls(quests)) filter = nameof(FilterQuests);
            else if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, issues)) filter = nameof(FilterIssues);
            else if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(LogEntryHistory)
                && method.Name == nameof(LogEntryHistory.GetGameActionLogs) && method.IsGenericMethod
                && method.GetGenericArguments().SequenceEqual(new[] { typeof(JournalLogEntry) })) filter = nameof(FilterHistory);
            if (filter == null) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(HeadmanHerdJournalVisibilityPatch), filter));
        }
        if (matches != 3)
            throw new InvalidOperationException("Quest journal sources changed; expected direct, alternative and history enumerations");
    }

    private static bool UsePersonalView(out IHeadmanHerdPersonalOwnership ownership)
    {
        ownership = null;
        return ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
            && ContainerProvider.TryResolve(out ownership);
    }

    internal static IEnumerable<QuestBase> FilterQuests(IEnumerable<QuestBase> quests)
        => UsePersonalView(out var ownership) ? quests.Where(quest => ownership.IsVisible(quest)) : quests;

    internal static IEnumerable<KeyValuePair<Hero, IssueBase>> FilterIssues(IEnumerable<KeyValuePair<Hero, IssueBase>> issues)
        => UsePersonalView(out var ownership) ? issues.Where(entry => ownership.IsVisible(entry.Value)) : issues;

    internal static IEnumerable<JournalLogEntry> FilterHistory(IEnumerable<JournalLogEntry> history)
        => UsePersonalView(out var ownership) ? history.Where(entry => ownership.IsVisible(entry)) : history;

    internal static MBReadOnlyList<QuestBase> ProjectQuests(MBReadOnlyList<QuestBase> quests)
        => UsePersonalView(out var ownership) ? new MBList<QuestBase>(quests.Where(quest => ownership.IsVisible(quest))) : quests;

}

[HarmonyPatch(typeof(Helpers.MenuHelper), nameof(Helpers.MenuHelper.SetIssueAndQuestDataForHero))]
internal static class HeadmanHerdHeroMenuPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var tracked = AccessTools.PropertyGetter(typeof(QuestBase), nameof(QuestBase.IsTrackEnabled));
        var issue = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Issue));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(tracked))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.DeclaredMethod(typeof(HeadmanHerdHeroMenuPatch), nameof(IsPersonallyTracked));
                matches++;
            }
            yield return instruction;
            if (instruction.Calls(issue))
                yield return CodeInstruction.Call(typeof(HeadmanHerdMenuIssuePatch), nameof(HeadmanHerdMenuIssuePatch.VisibleIssue));
        }
        if (matches != 3) throw new InvalidOperationException("Hero menu tracking conditions changed");
    }

    private static bool IsPersonallyTracked(QuestBase quest)
        => quest.IsTrackEnabled && (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
            || !ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership) || ownership.IsVisible(quest));
}

[HarmonyPatch]
internal static class HeadmanHerdMenuQuestPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(QuestManager), nameof(QuestManager.IsQuestGiver));
        yield return AccessTools.DeclaredMethod(typeof(QuestManager), nameof(QuestManager.CheckQuestForMenuLocations));
        yield return AccessTools.DeclaredMethod(typeof(QuestManager), nameof(QuestManager.IsLocationsTracked));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(QuestManager), nameof(QuestManager.Quests));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(getter)) continue;
            matches++;
            yield return CodeInstruction.Call(typeof(HeadmanHerdJournalVisibilityPatch), nameof(HeadmanHerdJournalVisibilityPatch.ProjectQuests));
        }
        if (matches != 1) throw new InvalidOperationException("Quest menu enumeration changed");
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.GetQuestGiverQuests))]
internal static class HeadmanHerdQuestGiverListPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<QuestBase> __result)
        => __result = HeadmanHerdJournalVisibilityPatch.FilterQuests(__result);
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.CheckIssueForMenuLocations))]
internal static class HeadmanHerdMenuIssuePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.Issue));
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(getter)) continue;
            matches++;
            yield return CodeInstruction.Call(typeof(HeadmanHerdMenuIssuePatch), nameof(VisibleIssue));
        }
        if (matches == 0) throw new InvalidOperationException("Issue menu lookup changed");
    }

    internal static IssueBase VisibleIssue(IssueBase issue)
    {
        if (issue is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue
            || issue.IsOngoingWithoutQuest || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return issue;
        return ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership) && ownership.IsVisible(issue) ? issue : null;
    }
}

[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.UpdateJournalLogEntries))]
internal static class HeadmanHerdUnreadHistoryPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is not MethodInfo method || method.DeclaringType != typeof(LogEntryHistory)
                || method.Name != nameof(LogEntryHistory.GetGameActionLogs) || !method.IsGenericMethod
                || !method.GetGenericArguments().SequenceEqual(new[] { typeof(JournalLogEntry) })) continue;
            matches++;
            yield return CodeInstruction.Call(typeof(HeadmanHerdJournalVisibilityPatch), nameof(HeadmanHerdJournalVisibilityPatch.FilterHistory));
        }
        if (matches != 1) throw new InvalidOperationException("Unread journal history enumeration changed");
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddTrackedObject))]
internal static class HeadmanHerdVisualTrackingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
    {
        if (__instance is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        Campaign.Current.QuestManager.AddTrackedObjectForQuest(trackedObject, __instance);
        return false;
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.AddTrackedObjectForQuest))]
internal static class HeadmanHerdTrackingReferencePatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestManager __instance, ITrackableCampaignObject trackedObject, QuestBase relatedQuest, out bool __state)
    {
        __state = relatedQuest is HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest
            && ModInformation.IsClient && relatedQuest.IsTrackEnabled && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
            && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership) && ownership.IsVisible(relatedQuest)
            && (!__instance._trackedObjects.TryGetValue(trackedObject, out var quests) || !quests.Contains(relatedQuest));
    }

    [HarmonyPostfix]
    private static void Postfix(ITrackableCampaignObject trackedObject, bool __state)
    {
        if (__state) Campaign.Current.VisualTrackerManager.RegisterObject(trackedObject);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.ToggleTrackedObjects))]
internal static class HeadmanHerdTrackingTogglePatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance)
    {
        if (__instance is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)
            && ownership.IsVisible(__instance);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal static class HeadmanHerdLoadedTrackingPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestManager __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
            || !ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)) return;
        foreach (var quest in __instance.Quests)
        {
            if (quest is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest
                || !quest.IsTrackEnabled || !ownership.IsVisible(quest)) continue;
            foreach (var tracked in __instance.GetAllTrackedObjectsOfAQuest(quest))
                Campaign.Current.VisualTrackerManager.RegisterObject(tracked);
        }
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.RemoveTrackedObjectForQuest))]
internal static class HeadmanHerdRemoveTrackingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestManager __instance, ITrackableCampaignObject trackedObject, QuestBase relatedQuest)
    {
        if (relatedQuest is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest quest
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!__instance._trackedObjects.TryGetValue(trackedObject, out var quests) || !quests.Remove(quest)) return true;
        if (quests.Count == 0) __instance._trackedObjects.Remove(trackedObject);

        // Shared quest references do not own the local player's visual marker.
        if (ModInformation.IsClient && quest.IsTrackEnabled
            && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership) && ownership.IsVisible(quest))
            Campaign.Current.VisualTrackerManager.RemoveTrackedObject(trackedObject);
        return false;
    }
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), nameof(JournalLogsCampaignBehavior.OnIssueUpdated))]
internal static class HeadmanHerdIssueHistoryPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue, Hero issueSolver)
        => issue is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue
            || (issue.IsTriedToSolveBefore && issueSolver != null);
}

[HarmonyPatch]
internal static class HeadmanHerdNotificationPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnQuestStarted));
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnQuestLogAdded));
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnQuestCompleted));
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), nameof(DefaultNotificationsCampaignBehavior.OnIssueUpdated));
        yield return AccessTools.DeclaredMethod(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.OnQuestLogAdded));
        yield return AccessTools.DeclaredMethod(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.OnIssueLogAdded));
    }

    [HarmonyPrefix]
    internal static bool Prefix(MBObjectBase __0)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (__0 is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue
            && __0 is not HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)
            && ownership.IsVisible(__0);
    }
}
