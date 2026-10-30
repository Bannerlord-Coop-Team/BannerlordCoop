using Common.Messaging;
using Coop.Naval.CampaignBehaviors.Messages;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static NavalDLC.CampaignBehaviors.NavalOrderOfBattleCampaignBehavior;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalOrderOfBattleCampaignBehavior))]
internal class NavalOrderOfBattleCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(NavalOrderOfBattleCampaignBehavior.SetFormationInfos))]
    [HarmonyPostfix]
    private static void SetFormationInfosPostfix(List<NavalOrderOfBattleFormationData> formationInfos, bool isInArmy)
    {
        // Send the player's naval order of battle to the server so it's kept in the CoopSession for rejoining
        var message = new NavalOrderOfBattleSaved(Hero.MainHero, formationInfos, isInArmy);
        MessageBroker.Instance.Publish(null, message);
    }
}
