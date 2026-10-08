using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
internal class SmugglersQuestLogPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, JournalLog __result)
    {
        if (ModInformation.IsClient || __instance is not SmugglersIssueBehavior.SmugglersIssueQuest quest) return;
        // Acceptance mirrors the first entry before the server-created party is assigned.
        if (quest._smugglerParty == null) return;
        MessageBroker.Instance.Publish(quest, new SmugglersQuestLogAdded(quest, __result));
    }
}
