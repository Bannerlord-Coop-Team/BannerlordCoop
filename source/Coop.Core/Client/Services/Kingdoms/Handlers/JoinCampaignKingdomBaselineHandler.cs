using Common;
using Common.Messaging;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using System;

namespace Coop.Core.Client.Services.Kingdoms.Handlers;

internal class JoinCampaignKingdomBaselineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IAllianceOfferPendingApplier allianceOfferPendingApplier;
    private readonly IPeaceOfferPendingApplier peaceOfferPendingApplier;

    public JoinCampaignKingdomBaselineHandler(
        IMessageBroker messageBroker,
        IAllianceOfferPendingApplier allianceOfferPendingApplier,
        IPeaceOfferPendingApplier peaceOfferPendingApplier)
    {
        this.messageBroker = messageBroker;
        this.allianceOfferPendingApplier = allianceOfferPendingApplier;
        this.peaceOfferPendingApplier = peaceOfferPendingApplier;

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
            peaceOfferPendingApplier.Apply(baseline.PendingPeaceOffers);
            foreach (var roundStatus in GetActiveDecisionRounds(baseline))
            {
                messageBroker.Publish(this, new ApplyKingdomDecisionRoundStatus(roundStatus));
            }
        });
    }

    internal static KingdomDecisionRoundStatusData[] GetActiveDecisionRounds(
        NetworkJoinCampaignKingdomBaseline baseline)
    {
        return baseline.ActiveDecisionRounds ?? Array.Empty<KingdomDecisionRoundStatusData>();
    }
}
