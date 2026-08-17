using Common;
using Common.Messaging;
using Coop.Core.Server.Services.Kingdoms.Messages;

namespace Coop.Core.Client.Services.Kingdoms.Handlers;

internal class JoinCampaignKingdomBaselineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IAllianceOfferPendingApplier allianceOfferPendingApplier;

    public JoinCampaignKingdomBaselineHandler(
        IMessageBroker messageBroker,
        IAllianceOfferPendingApplier allianceOfferPendingApplier)
    {
        this.messageBroker = messageBroker;
        this.allianceOfferPendingApplier = allianceOfferPendingApplier;

        messageBroker.Subscribe<NetworkJoinCampaignKingdomBaseline>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkJoinCampaignKingdomBaseline>(Handle);
    }

    private void Handle(MessagePayload<NetworkJoinCampaignKingdomBaseline> payload)
    {
        var baseline = payload.What;
        GameThread.RunSafe(() =>
        {
            allianceOfferPendingApplier.Apply(baseline.PendingAllianceOffers);
        });
    }
}