using Common;
using Common.Network;
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
        if (!ContainerProvider.TryResolve<ISiegeEventGraphSynchronizer>(out var graphSynchronizer)
            || !ContainerProvider.TryResolve<INetwork>(out var network)) return;

        var siegeEvent = settlement?.SiegeEvent;
        if (!graphSynchronizer.TryCapture(siegeEvent, out var snapshot, besiegerParty)) return;

        network.SendAll(new NetworkInitializeSiegeEvent(snapshot));
    }
}
