using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEvents.Handlers;

internal class SiegeEventInitializationSnapshotHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEventInitializationSnapshotHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkInitializeSiegeEvent>(HandleInitialize);
    }

    private void HandleInitialize(MessagePayload<NetworkInitializeSiegeEvent> payload)
    {
        if (ModInformation.IsServer) return;
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(message.SiegeEventId, out var siegeEvent)
                || !objectManager.TryGetObjectWithLogging<Settlement>(message.SettlementId, out var settlement)
                || !objectManager.TryGetObjectWithLogging<BesiegerCamp>(message.BesiegerCampId, out var camp)
                || !objectManager.TryGetObjectWithLogging<MobileParty>(message.LeaderPartyId, out var leaderParty)
                || !objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(
                    message.AttackerSiegeEnginesId, out var attackerEngines)
                || !objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(
                    message.DefenderSiegeEnginesId, out var defenderEngines)) return;

            using (new AllowedThread())
            {
                if (siegeEvent.BesiegedSettlement != settlement)
                {
                    ReflectionUtils.SetPrivateField(
                        typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement), siegeEvent, settlement);
                }

                if (siegeEvent.BesiegerCamp != camp)
                {
                    ReflectionUtils.SetPrivateField(
                        typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp), siegeEvent, camp);
                }

                if (settlement.SiegeEvent != siegeEvent) settlement.SiegeEvent = siegeEvent;
                if (camp.SiegeEvent != siegeEvent) camp.SiegeEvent = siegeEvent;
                if (camp._leaderParty != leaderParty) camp._leaderParty = leaderParty;
                if (leaderParty.BesiegerCamp != camp) leaderParty.BesiegerCamp = camp;
                if (camp.SiegeEngines != attackerEngines) camp.SiegeEngines = attackerEngines;
                if (settlement.SiegeEngines != defenderEngines) settlement.SiegeEngines = defenderEngines;

                var siegeEvents = Campaign.Current?.SiegeEventManager?._siegeEvents;
                if (siegeEvents != null && !siegeEvents.Contains(siegeEvent))
                {
                    siegeEvents.Add(siegeEvent);
                }
            }

            settlement.Party?.SetVisualAsDirty();
        }, context: nameof(NetworkInitializeSiegeEvent));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkInitializeSiegeEvent>(HandleInitialize);
    }
}
