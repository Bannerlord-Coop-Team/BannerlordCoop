using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Quests), MethodType.Getter)]
internal class ConquestQuestListPatch
{
    [HarmonyPostfix]
    internal static void Postfix(ref MBReadOnlyList<QuestBase> __result)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!ContainerProvider.TryResolve<IConquestQuest>(out var service)) return;
        if (__result.Any(quest => quest is Quest && !service.IsVisible(quest)))
            __result = __result.Where(quest => quest is not Quest || service.IsVisible(quest)).ToMBList();
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.TrackedObjects), MethodType.Getter)]
internal class ConquestQuestTrackingPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref MBReadOnlyDictionary<ITrackableCampaignObject, List<QuestBase>> __result)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!ContainerProvider.TryResolve<IConquestQuest>(out var service)) return;
        if (!__result.Values.Any(quests => quests.Any(quest => quest is Quest && !service.IsVisible(quest)))) return;
        var visible = new Dictionary<ITrackableCampaignObject, List<QuestBase>>();
        foreach (var entry in __result)
        {
            var quests = entry.Value.Where(quest => quest is not Quest || service.IsVisible(quest)).ToList();
            if (quests.Count != 0) visible.Add(entry.Key, quests);
        }
        __result = visible.GetReadOnlyDictionary();
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.IsTrackEnabled), MethodType.Getter)]
internal class ConquestQuestTrackingEnabledPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, ref bool __result)
    {
        if (__instance is not Quest || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        __result = __result && ContainerProvider.TryResolve<IConquestQuest>(out var service) && service.IsVisible(__instance);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.IsThereDiscussDialogFlow), MethodType.Getter)]
internal class ConquestQuestDiscussionPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, ref bool __result)
    {
        if (__instance is not Quest || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        __result = __result && ContainerProvider.TryResolve<IConquestQuest>(out var service) && service.IsVisible(__instance);
    }
}

[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal class ConquestQuestHistoryPatch
{
    [HarmonyPostfix]
    internal static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        if (!__result || __instance.Title.GetID() != "mvzh0HVk" || ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;
        __result = ContainerProvider.TryResolve<IConquestQuest>(out var service) && service.IsVisible(__instance);
    }
}

[HarmonyPatch]
internal class ConquestQuestNotificationPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "OnQuestStarted", "OnQuestLogAdded", "OnQuestCompleted" })
            yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), name);
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase quest)
    {
        if (quest is not Quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IConquestQuest>(out var service) && service.IsVisible(quest);
    }
}
