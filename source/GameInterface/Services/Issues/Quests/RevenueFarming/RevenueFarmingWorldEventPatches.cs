using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class RevenueFarmingWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix()
    {
        Run(quest =>
        {
            if (!ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) || sessions.Get(quest) is not { } session ||
                !ContainerProvider.TryResolve<IRevenueFarmingExecutionScope>(out var execution)) return;
            if (quest.QuestDueTime.IsPast)
            {
                using (execution.Enter(session)) quest.CompleteQuestWithTimeOut();
            }
            if (quest.IsOngoing && !session.AwaitingPayment) quest.HourlyTick();
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => Run(quest => quest.OnVillageRaid(village));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void BattleEndedPostfix(MapEvent mapEvent) => Run(quest => quest.OnMapEventEnded(mapEvent));

    private static void Run(Action<Quest> action)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(action);
    }
}
