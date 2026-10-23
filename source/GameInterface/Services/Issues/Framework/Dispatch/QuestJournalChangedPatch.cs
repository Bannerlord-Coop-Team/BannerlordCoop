using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Mirrors local history while received server snapshots remain one-way.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnQuestLogAdded))]
internal class QuestJournalChangedPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __0)
    {
        if (ModInformation.IsClient && !QuestJournalReplay.IsActive)
            MessageBroker.Instance.Publish(__0, new QuestJournalChanged(__0));
    }
}
