using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

[HarmonyPatch]
internal class RevenueFarmingWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix()
    {
        Run(quest =>
        {
            if (!ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) || sessions.Get(quest) is not { } session) return;
            if (quest.QuestDueTime.IsPast)
            {
                using (new RevenueFarmingExecutionScope(session)) quest.CompleteQuestWithTimeOut();
            }
            if (quest.IsOngoing && !session.AwaitingPayment) quest.HourlyTick();
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => Run(quest => quest.OnVillageRaid(village));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementOwnerChanged))]
    [HarmonyPostfix]
    private static void OwnerPostfix(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
        Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        => Run(quest => quest.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void BattleStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        => Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void BattleEndedPostfix(MapEvent mapEvent) => Run(quest => quest.OnMapEventEnded(mapEvent));

    private static void Run(Action<Quest> action)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(action);
    }
}
