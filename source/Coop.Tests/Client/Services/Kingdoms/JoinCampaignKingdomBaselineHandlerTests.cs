using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Services.Kingdoms;
using Coop.Core.Client.Services.Kingdoms.Handlers;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Moq;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.Services.Kingdoms;

public class JoinCampaignKingdomBaselineHandlerTests
{
    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly Mock<IAllianceOfferPendingApplier> allianceOfferPendingApplier =
        new Mock<IAllianceOfferPendingApplier>();
    private readonly Mock<IPeaceOfferPendingApplier> peaceOfferPendingApplier =
        new Mock<IPeaceOfferPendingApplier>();
    private readonly JoinCampaignKingdomBaselineHandler handler;

    public JoinCampaignKingdomBaselineHandlerTests(ITestOutputHelper output)
    {
        _ = output;
        handler = new JoinCampaignKingdomBaselineHandler(
            messageBroker,
            allianceOfferPendingApplier.Object,
            peaceOfferPendingApplier.Object);
    }

    [Fact]
    public void ReceivedBaseline_AppliesPendingOffersFromPayload()
    {
        var allianceOffers = new[]
        {
            new PendingAllianceOfferBaseline("kingdom_a", "kingdom_b"),
            new PendingAllianceOfferBaseline("kingdom_c", "kingdom_d"),
        };

        var peaceOffers = new[]
        {
            new PendingPeaceOfferBaseline("kingdom_a", "kingdom_b"),
            new PendingPeaceOfferBaseline("kingdom_c", "kingdom_d"),
        };
        Apply(new NetworkJoinCampaignKingdomBaseline(allianceOffers, peaceOffers));

        allianceOfferPendingApplier.Verify(applier => applier.Apply(allianceOffers), Times.Once);
        peaceOfferPendingApplier.Verify(applier => applier.Apply(peaceOffers), Times.Once);
    }

    [Fact]
    public void EmptyBaseline_AppliesEmptyOfferSet()
    {
        var allianceOffers = Array.Empty<PendingAllianceOfferBaseline>();
        var peaceOffers = Array.Empty<PendingPeaceOfferBaseline>();

        Apply(new NetworkJoinCampaignKingdomBaseline(allianceOffers, peaceOffers));

        allianceOfferPendingApplier.Verify(applier => applier.Apply(allianceOffers), Times.Once);
        peaceOfferPendingApplier.Verify(applier => applier.Apply(peaceOffers), Times.Once);
    }

    [Fact]
    public void MultipleReceivedBaselines_AppliesEachIndependently()
    {
        var first = new[] { new PendingAllianceOfferBaseline("kingdom_a", "kingdom_b") };
        var second = new[] { new PendingAllianceOfferBaseline("kingdom_e", "kingdom_f") };
        var third = new[] { new PendingPeaceOfferBaseline("kingdom_a", "kingdom_b") };
        var fourth = new[] { new PendingPeaceOfferBaseline("kingdom_e", "kingdom_f") };

        Apply(new NetworkJoinCampaignKingdomBaseline(first, third));
        Apply(new NetworkJoinCampaignKingdomBaseline(second, fourth));
        

        allianceOfferPendingApplier.Verify(applier => applier.Apply(first), Times.Once);
        peaceOfferPendingApplier.Verify(applier => applier.Apply(third), Times.Once);
        allianceOfferPendingApplier.Verify(applier => applier.Apply(second), Times.Once);
        peaceOfferPendingApplier.Verify(applier => applier.Apply(fourth), Times.Once);
    }

    private void Apply(NetworkJoinCampaignKingdomBaseline baseline)
    {
        messageBroker.Publish(this, baseline);
        GameThread.Run(() => { }, blocking: true);
    }
}