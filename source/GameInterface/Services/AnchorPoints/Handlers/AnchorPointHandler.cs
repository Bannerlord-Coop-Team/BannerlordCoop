using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.AnchorPoints.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.AnchorPoints.Handlers;

internal class AnchorPointHandler : IHandler
{
    // Same slack as SettlementEncounterDistanceValidator, since the client and server each simulate the party's move
    private const float PositionSyncDriftSlack = 0.5f;

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;

    public AnchorPointHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<CallFleetRequested>(Handle_CallFleetRequested);
        messageBroker.Subscribe<NetworkCallFleet>(Handle_NetworkCallFleet);

        messageBroker.Subscribe<EmbarkAtAnchorRequested>(Handle_EmbarkAtAnchorRequested);
        messageBroker.Subscribe<NetworkEmbarkAtAnchor>(Handle_NetworkEmbarkAtAnchor);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CallFleetRequested>(Handle_CallFleetRequested);
        messageBroker.Unsubscribe<NetworkCallFleet>(Handle_NetworkCallFleet);

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
            if (!playerManager.IsSenderControllingParty(obj.Who, data.MobilePartyId)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;

            // The request waited in the queue, so check vanilla AnchorPoint.CanPartyInteract against the party as it is now
            var anchor = mobileParty.Anchor;
            if (anchor == null || !anchor.IsValid || anchor.IsDisabled || mobileParty.IsCurrentlyAtSea) return;
            if (mobileParty.MapEvent != null || mobileParty.CurrentSettlement != null) return;
            float maximumDistance = Campaign.Current.Models.EncounterModel.NeededMaximumLandDistanceForEncounteringMobileParty + PositionSyncDriftSlack;
            if (mobileParty.Position.Distance(anchor.GetInteractionPosition(mobileParty)) >= maximumDistance) return;

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
            if (!playerManager.IsSenderControllingParty(obj.Who, data.MobilePartyId)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            // The request waited in the queue, so check vanilla's call_fleet_condition against the party as it is now
            var anchor = mobileParty.Anchor;
            if (anchor == null || mobileParty.CurrentSettlement != settlement || !settlement.HasPort || !mobileParty.HasNavalNavigationCapability) return;
            if (anchor.IsAtSettlement(settlement) || (anchor.IsMovingToPoint && anchor.IsTargetingSettlement(settlement))) return;

            anchor.CallFleet(settlement);
        });
    }
}
