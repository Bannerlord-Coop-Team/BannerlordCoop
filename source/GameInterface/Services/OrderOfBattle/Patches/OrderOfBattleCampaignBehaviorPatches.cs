using Common.Messaging;
using GameInterface.Services.OrderOfBattle.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.OrderOfBattleCampaignBehavior;

namespace GameInterface.Services.OrderOfBattle.Patches;

[HarmonyPatch(typeof(OrderOfBattleCampaignBehavior))]
internal class OrderOfBattleCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(OrderOfBattleCampaignBehavior.SetFormationInfos))]
    [HarmonyPostfix]
    private static void SetFormationInfosPostfix(List<OrderOfBattleFormationData> formationInfos, bool isSiegeBattle, bool isInArmy)
    {
        // Send the player's order of battle to the server so it's kept in the CoopSession for rejoining
        var message = new OrderOfBattleSaved(Hero.MainHero, formationInfos, isSiegeBattle, isInArmy);
        MessageBroker.Instance.Publish(null, message);
    }
}
