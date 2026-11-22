using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Mirrors a native log removal after the running journal has changed.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.RemoveLog))]
internal class QuestJournalRemovedPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance)
    {
        if (ModInformation.IsClient && !QuestJournalReplay.IsActive)
            MessageBroker.Instance.Publish(__instance, new QuestJournalChanged(__instance));
    }
}
