using Common;
using Common.Tests.Utils;
using Coop.Core.Client.Services.Kingdoms;
using Coop.Core.Client.Services.Kingdoms.Handlers;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
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

    [Fact]
    public void ReceivedBaseline_PublishesEveryActiveDecisionRoundStatus()
    {
        var appliedStatuses = new List<KingdomDecisionRoundStatusData>();
        messageBroker.Subscribe<ApplyKingdomDecisionRoundStatus>(payload => appliedStatuses.Add(payload.What.Status));
        var statuses = new[]
        {
            new KingdomDecisionRoundStatusData("kingdom_a", 0, 100, Array.Empty<KingdomDecisionRoundClanStatusData>()),
            new KingdomDecisionRoundStatusData("kingdom_b", 1, 200, Array.Empty<KingdomDecisionRoundClanStatusData>()),
        };

        Apply(new NetworkJoinCampaignKingdomBaseline(activeDecisionRounds: statuses));

        Assert.Equal(statuses, appliedStatuses);
    }

    [Fact]
    public void ReceivedBaseline_PublishesActiveVotesBeforeRoundStatus()
    {
        var appliedMessages = new List<object>();
        messageBroker.Subscribe<ApplyKingdomDecisionVote>(payload => appliedMessages.Add(payload.What));
        messageBroker.Subscribe<ApplyKingdomDecisionRoundStatus>(payload => appliedMessages.Add(payload.What));
        var voteData = new KingdomDecisionVoteData("kingdom_a", 0, 1, 3, false, true, "outcome-b");
        var status = new KingdomDecisionRoundStatusData(
            "kingdom_a",
            0,
            100,
            Array.Empty<KingdomDecisionRoundClanStatusData>());

        var baseline = new NetworkJoinCampaignKingdomBaseline(
            activeDecisionRounds: new[] { status },
            activeDecisionVotes: new[] { new KingdomDecisionRoundVoteData("clan_a", voteData) });
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, baseline);
        stream.Position = 0;

        Apply(Serializer.Deserialize<NetworkJoinCampaignKingdomBaseline>(stream));

        var appliedVote = Assert.IsType<ApplyKingdomDecisionVote>(appliedMessages[0]);
        Assert.Equal("clan_a", appliedVote.ClanId);
        Assert.Equal(voteData.KingdomId, appliedVote.VoteData.KingdomId);
        Assert.Equal(voteData.DecisionIndex, appliedVote.VoteData.DecisionIndex);
        Assert.Equal(voteData.OutcomeKey, appliedVote.VoteData.OutcomeKey);
        Assert.IsType<ApplyKingdomDecisionRoundStatus>(appliedMessages[1]);
    }

    [Fact]
    public void EmptySerializedBaseline_DoesNotPublishDecisionRoundStatus()
    {
        var appliedStatuses = new List<KingdomDecisionRoundStatusData>();
        messageBroker.Subscribe<ApplyKingdomDecisionRoundStatus>(payload => appliedStatuses.Add(payload.What.Status));
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkJoinCampaignKingdomBaseline());
        stream.Position = 0;
        NetworkJoinCampaignKingdomBaseline baseline =
            Serializer.Deserialize<NetworkJoinCampaignKingdomBaseline>(stream);

        Assert.Null(baseline.ActiveDecisionRounds);
        Assert.Null(baseline.ActiveDecisionVotes);
        Assert.Empty(JoinCampaignKingdomBaselineHandler.GetActiveDecisionRounds(baseline));
        Assert.Empty(JoinCampaignKingdomBaselineHandler.GetActiveDecisionVotes(baseline));
        Apply(baseline);

        Assert.Empty(appliedStatuses);
    }

    private void Apply(NetworkJoinCampaignKingdomBaseline baseline)
    {
        messageBroker.Publish(this, baseline);
        GameThread.Run(() => { }, blocking: true);
    }
}
