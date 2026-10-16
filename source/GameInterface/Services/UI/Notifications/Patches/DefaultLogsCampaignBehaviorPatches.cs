using Common;
using Common.Messaging;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Notifications.Patches;

/// <summary>
/// Relays log entries whose campaign events never fire on clients.
/// Logs for events already relayed by <see cref="DefaultNotificationsCampaignBehaviorPatches"/> are added by its client handlers.
/// </summary>
[HarmonyPatch(typeof(DefaultLogsCampaignBehavior))]
internal class DefaultLogsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(DefaultLogsCampaignBehavior.OnArmyGathered))]
    [HarmonyPostfix]
    public static void OnArmyGatheredPostfix(ref DefaultLogsCampaignBehavior __instance, Army army, IMapPoint gatheringPoint)
    {
        if (ModInformation.IsClient) return;

        var message = new LogArmyGathered(army, gatheringPoint);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(DefaultLogsCampaignBehavior.OnSiegeAftermathApplied))]
    [HarmonyPostfix]
    public static void OnSiegeAftermathAppliedPostfix(ref DefaultLogsCampaignBehavior __instance, MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType, Clan previousSettlementOwner, Dictionary<MobileParty, float> partyContributions)
    {
        if (ModInformation.IsClient) return;

        var message = new LogSiegeAftermathApplied(attackerParty, settlement, aftermathType, previousSettlementOwner, partyContributions);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
