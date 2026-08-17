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
    private readonly JoinCampaignKingdomBaselineHandler handler;

    public JoinCampaignKingdomBaselineHandlerTests(ITestOutputHelper output)
    {
        _ = output;
        handler = new JoinCampaignKingdomBaselineHandler(
            messageBroker,
            allianceOfferPendingApplier.Object);
    }

    [Fact]
    public void ReceivedBaseline_AppliesPendingAllianceOffersFromPayload()
    {
        var offers = new[]
        {
            new PendingAllianceOfferBaseline("kingdom_a", "kingdom_b"),
            new PendingAllianceOfferBaseline("kingdom_c", "kingdom_d"),
        };

        Apply(new NetworkJoinCampaignKingdomBaseline(offers));

        allianceOfferPendingApplier.Verify(applier => applier.Apply(offers), Times.Once);
    }

    [Fact]
    public void EmptyBaseline_AppliesEmptyOfferSet()
    {
        var offers = Array.Empty<PendingAllianceOfferBaseline>();

        Apply(new NetworkJoinCampaignKingdomBaseline(offers));

        allianceOfferPendingApplier.Verify(applier => applier.Apply(offers), Times.Once);
    }

    [Fact]
    public void MultipleReceivedBaselines_AppliesEachIndependently()
    {
        var first = new[] { new PendingAllianceOfferBaseline("kingdom_a", "kingdom_b") };
        var second = new[] { new PendingAllianceOfferBaseline("kingdom_e", "kingdom_f") };

        Apply(new NetworkJoinCampaignKingdomBaseline(first));
        Apply(new NetworkJoinCampaignKingdomBaseline(second));

        allianceOfferPendingApplier.Verify(applier => applier.Apply(first), Times.Once);
        allianceOfferPendingApplier.Verify(applier => applier.Apply(second), Times.Once);
    }

    private void Apply(NetworkJoinCampaignKingdomBaseline baseline)
    {
        messageBroker.Publish(this, baseline);
        GameThread.Run(() => { }, blocking: true);
    }
}