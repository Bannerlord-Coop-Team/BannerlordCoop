using Common;
using HarmonyLib;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
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
            if (line.Text.GetID() != "21Y4tWE9") continue;
            var original = line.ConsequenceDelegate;
            line.ConsequenceDelegate = () =>
            {
                if (ContainerProvider.TryResolve<IGangLeaderGoodsService>(out var service)) service.Purchase(__instance);
                original();
            };
        }
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
