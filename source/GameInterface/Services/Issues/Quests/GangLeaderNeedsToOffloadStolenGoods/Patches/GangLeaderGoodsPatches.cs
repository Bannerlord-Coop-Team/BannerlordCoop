using Common;
using HarmonyLib;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods.Patches;

[HarmonyPatch]
internal class GangLeaderGoodsPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.GetHideoutLeaderDialogFlow))]
    [HarmonyPostfix]
    private static void PurchaseConsequence(Quest __instance, DialogFlow __result)
    {
        if (ModInformation.IsServer) return;
        foreach (var line in __result.Lines)
        {
            var payment = line.Text.GetID() == "21Y4tWE9";
            if (!payment && line.Text.GetID() != "M8j6J8aN") continue;
            line.ConsequenceDelegate = () =>
            {
                if (!ContainerProvider.TryResolve<IGangLeaderGoodsService>(out var service)) return;
                if (payment) service.Purchase(__instance);
                else service.Fight(__instance);
            };
        }
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void BattleStarted(MapEvent mapEvent) => Observe(mapEvent);

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftSettlement(MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IGangLeaderGoodsService>(out var service) &&
            ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.RunAsOwners<Quest>(quest => service.LeaveHideout(quest, party, settlement));
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.OnBattleWon))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void BattleWon(MapEvent __instance) => Observe(__instance);

    private static void Observe(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IGangLeaderGoodsService>(out var service) &&
            ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.RunAsOwners<Quest>(quest => service.ObserveBattle(quest, mapEvent));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPrefix]
    private static void BattleEnded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IGangLeaderGoodsService>(out var service) &&
            ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.RunAsOwners<Quest>(quest => service.BattleEnded(quest, mapEvent));
    }
}
