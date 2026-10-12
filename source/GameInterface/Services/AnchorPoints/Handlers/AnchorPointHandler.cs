using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.AnchorPoints.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.AnchorPoints.Handlers;

internal class AnchorPointHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public AnchorPointHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<CallFleetRequested>(Handle_CallFleetRequested);
        messageBroker.Subscribe<NetworkCallFleet>(Handle_NetworkCallFleet);

        messageBroker.Subscribe<FleetArrived>(Handle_FleetArrived);
        messageBroker.Subscribe<NetworkNotifyFleetArrived>(Handle_NetworkNotifyFleetArrived);

        messageBroker.Subscribe<EmbarkAtAnchorRequested>(Handle_EmbarkAtAnchorRequested);
        messageBroker.Subscribe<NetworkEmbarkAtAnchor>(Handle_NetworkEmbarkAtAnchor);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CallFleetRequested>(Handle_CallFleetRequested);
        messageBroker.Unsubscribe<NetworkCallFleet>(Handle_NetworkCallFleet);

        messageBroker.Unsubscribe<FleetArrived>(Handle_FleetArrived);
        messageBroker.Unsubscribe<NetworkNotifyFleetArrived>(Handle_NetworkNotifyFleetArrived);

        messageBroker.Unsubscribe<EmbarkAtAnchorRequested>(Handle_EmbarkAtAnchorRequested);
        messageBroker.Unsubscribe<NetworkEmbarkAtAnchor>(Handle_NetworkEmbarkAtAnchor);
    }

    private void Handle_EmbarkAtAnchorRequested(MessagePayload<EmbarkAtAnchorRequested> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new NetworkEmbarkAtAnchor(mobilePartyId));
    }

    private void Handle_NetworkEmbarkAtAnchor(MessagePayload<NetworkEmbarkAtAnchor> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

            var anchor = mobileParty.Anchor;
            if (anchor == null || !anchor.IsValid || mobileParty.IsCurrentlyAtSea) return;

            // Vanilla clears the interaction, targets the anchor and sets sail there
            anchor.OnPartyInteraction(mobileParty);
            mobileParty.SetMoveModeHold();

            // SetSailAtPosition writes the position directly, so push it and the sea state to clients
            messageBroker.Publish(this, new PartyBehaviorChangeAttempted(mobileParty, forcePosition: true, isCurrentlyAtSea: true));
        });
    }

    private void Handle_CallFleetRequested(MessagePayload<CallFleetRequested> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkCallFleet(mobilePartyId, settlementId));
    }

    private void Handle_NetworkCallFleet(MessagePayload<NetworkCallFleet> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            mobileParty.Anchor.CallFleet(settlement);
        });
    }

    private void Handle_FleetArrived(MessagePayload<FleetArrived> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new NetworkNotifyFleetArrived(mobilePartyId));
    }

    private void Handle_NetworkNotifyFleetArrived(MessagePayload<NetworkNotifyFleetArrived> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;
            if (mobileParty != MobileParty.MainParty) return;

            // Same message vanilla AnchorPoint.HourlyTick shows for MobileParty.MainParty
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=qFAhDtrw}Your fleet has arrived at its destination.").ToString(), new Color(0f, 1f, 0f))
            {
                SoundEventPath = "event:/ui/panels/panel_call_fleet"
            });
        });
    }
}
