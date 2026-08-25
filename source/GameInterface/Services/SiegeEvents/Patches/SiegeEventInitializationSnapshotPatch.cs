using Common;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

[HarmonyPatch(typeof(SiegeEventManager), nameof(SiegeEventManager.StartSiegeEvent))]
internal static class SiegeEventInitializationSnapshotPatch
{
    [HarmonyPostfix]
    private static void Postfix(Settlement settlement, MobileParty besiegerParty)
    {
        if (ModInformation.IsClient) return;

        PublishSnapshot(settlement, besiegerParty);
    }

    private static void PublishSnapshot(Settlement settlement, MobileParty besiegerParty)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<INetwork>(out var network)) return;

        var siegeEvent = settlement?.SiegeEvent;
        var camp = siegeEvent?.BesiegerCamp;
        var attackerEngines = camp?.SiegeEngines;
        var defenderEngines = settlement?.SiegeEngines;
        var leaderParty = camp?.LeaderParty ?? besiegerParty;

        if (!objectManager.TryGetIdWithLogging(siegeEvent, out var siegeEventId)
            || !objectManager.TryGetIdWithLogging(settlement, out var settlementId)
            || !objectManager.TryGetIdWithLogging(camp, out var campId)
            || !objectManager.TryGetIdWithLogging(leaderParty, out var leaderPartyId)
            || !objectManager.TryGetIdWithLogging(attackerEngines, out var attackerEnginesId)
            || !objectManager.TryGetIdWithLogging(defenderEngines, out var defenderEnginesId)) return;

        network.SendAll(new NetworkInitializeSiegeEvent(
            siegeEventId,
            settlementId,
            campId,
            leaderPartyId,
            attackerEnginesId,
            defenderEnginesId));
    }
}
