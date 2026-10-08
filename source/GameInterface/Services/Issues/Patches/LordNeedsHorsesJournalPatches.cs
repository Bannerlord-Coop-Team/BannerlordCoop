using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Data;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

[HarmonyPatch]
internal class LordNeedsHorsesNotificationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), "OnQuestStarted");
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), "OnQuestLogAdded");
        yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), "OnQuestCompleted");
    }

    [HarmonyPrefix]
    internal static bool QuestNotification(QuestBase quest) => quest is not Quest ||
        (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service) && service.IsLocalJournalOwner(quest, quest.QuestGiver));
}

[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnIssueUpdated")]
internal class LordNeedsHorsesIssueNotificationPatch
{
    [HarmonyPrefix]
    internal static bool IssueNotification(IssueBase issue) => issue is not Issue ||
        (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service) && service.IsLocalJournalOwner(issue, issue.IssueOwner));
}

[HarmonyPatch]
internal class LordNeedsHorsesJournalCreationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(JournalLogsCampaignBehavior), "CreateRelatedLog", new[] { typeof(IssueBase) });
        yield return AccessTools.DeclaredMethod(typeof(JournalLogsCampaignBehavior), "CreateRelatedLog", new[] { typeof(QuestBase) });
    }

    [HarmonyPrefix]
    private static bool Prefix(object __0, ref JournalLogEntry __result)
    {
        var issue = __0 as Issue ?? (__0 as Quest)?.QuestGiver.Issue as Issue;
        if (issue == null || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            throw new InvalidOperationException("Horse quest journal service is unavailable");
        __result = service.CreateJournal(issue);
        return false;
    }
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), "OnIssueUpdated")]
internal class LordNeedsHorsesUnacceptedJournalPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue, Hero issueSolver) => issue is not Issue || issueSolver != null ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();

    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver)
    {
        if (ModInformation.IsServer && issueSolver != null && issue is Issue horse)
            MessageBroker.Instance.Publish(issue, new LordNeedsHorsesJournalChanged(horse, details));
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.GetQuestGiverQuests))]
internal class LordNeedsHorsesDiscussionPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref IEnumerable<QuestBase> __result)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service);
        __result = __result.Where(quest => quest is not Quest || service?.IsLocalOwner(quest.QuestGiver) == true);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.IsQuestGiver))]
internal class LordNeedsHorsesQuestGiverVisibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestManager __instance, Hero offeringHero, ref bool __result)
    {
        if (__result && ModInformation.IsClient)
            __result = __instance.GetQuestGiverQuests(offeringHero).Any();
    }
}

[HarmonyPatch(typeof(QuestManager), "get_TrackedObjects")]
internal class LordNeedsHorsesTrackedQuestVisibilityPatch
{
    [HarmonyPostfix]
    internal static void Postfix(ref MBReadOnlyDictionary<ITrackableCampaignObject, List<QuestBase>> __result)
    {
        // Received quest changes still use the complete tracking registry for creation and cleanup.
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;
        ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service);
        __result = __result.Select(entry => new KeyValuePair<ITrackableCampaignObject, List<QuestBase>>(entry.Key,
                entry.Value.Where(quest => quest is not Quest || service?.IsLocalOwner(quest.QuestGiver) == true).ToList()))
            .Where(entry => entry.Value.Count != 0).ToDictionary(entry => entry.Key, entry => entry.Value).GetReadOnlyDictionary();
    }
}

[HarmonyPatch(typeof(VisualTrackerManager), nameof(VisualTrackerManager.CheckTracked))]
internal class LordNeedsHorsesTrackerVisibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(VisualTrackerManager __instance, ITrackableBase trackableObject, ref bool __result)
    {
        if (!__result || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            trackableObject is not ITrackableCampaignObject campaignObject ||
            !Campaign.Current.QuestManager._trackedObjects.TryGetValue(campaignObject, out var quests)) return;
        ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service);
        int hidden = quests.Count(quest => quest is Quest && quest.IsTrackEnabled && service?.IsLocalOwner(quest.QuestGiver) != true);
        if (hidden != 0 && __instance._trackedObjects.TryGetValue(trackableObject, out var tracked))
            __result = tracked.TrackerCount > hidden;
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, new[] { typeof(Action) })]
internal class LordNeedsHorsesJournalVisibilityPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(AccessTools.Field(typeof(IssueManager), nameof(IssueManager.Issues))))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(LordNeedsHorsesJournalVisibilityPatch), nameof(VisibleIssues));
                replaced++;
                yield return instruction;
                continue;
            }
            if (instruction.operand is MethodInfo method)
            {
                if (method == AccessTools.PropertyGetter(typeof(QuestManager), nameof(QuestManager.Quests)))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(LordNeedsHorsesJournalVisibilityPatch), nameof(VisibleQuests));
                    replaced++;
                }
                else if (method.Name == "GetGameActionLogs" && method.IsGenericMethod &&
                    method.GetGenericArguments().SequenceEqual(new[] { typeof(JournalLogEntry) }))
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(LordNeedsHorsesJournalVisibilityPatch), nameof(VisibleHistory)));
                    replaced++;
                    continue;
                }
            }
            yield return instruction;
        }
        if (replaced != 3) throw new InvalidOperationException("Horse quest journal filtering does not match the installed quest screen");
    }

    private static IEnumerable<QuestBase> VisibleQuests(QuestManager manager)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer) return manager.Quests;
        ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service);
        return manager.Quests.Where(quest => quest is not Quest || service?.IsLocalOwner(quest.QuestGiver) == true);
    }

    private static IEnumerable<KeyValuePair<Hero, IssueBase>> VisibleIssues(IssueManager manager)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer) return manager.Issues;
        ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service);
        return manager.Issues.Where(entry => entry.Value is not Issue || service?.IsLocalOwner(entry.Key) == true);
    }

    private static IEnumerable<JournalLogEntry> VisibleHistory(IEnumerable<JournalLogEntry> entries)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer) return entries;
        ContainerProvider.TryResolve<IControllerIdProvider>(out var controller);
        return entries.Where(entry => entry is not LordNeedsHorsesJournalLogEntry horse ||
            horse.OwnerControllerId == controller?.ControllerId);
    }
}
