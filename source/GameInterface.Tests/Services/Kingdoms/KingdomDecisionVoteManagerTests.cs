using Common.Messaging;
using Common.Util;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomDecisionVoteManagerTests : IDisposable
{
    private static readonly ICollection<string> NoFinalVotes = new HashSet<string>();

    private readonly Mock<IObjectManager> objectManager = new();
    private readonly KingdomDecisionVoteManager manager;

    public KingdomDecisionVoteManagerTests()
    {
        manager = new KingdomDecisionVoteManager(
            Mock.Of<IPlayerManager>(),
            objectManager.Object,
            Mock.Of<IMessageBroker>(),
            Mock.Of<IKingdomDecisionOutcomeResolver>(),
            Mock.Of<IKingdomDecisionOutcomeOrder>(),
            Mock.Of<IKingdomDecisionRoundPresentation>(),
            Mock.Of<IKingdomVotingRoundClock>());
    }

    public void Dispose()
    {
        manager.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsPendingVoter_HoldsForAVoterWhoseClanStillOwesAVote()
    {
        Player voter = PlayerInClan("voter", "clan_a");

        Assert.True(manager.IsPendingVoter(new[] { "clan_a" }, NoFinalVotes, null, voter));
    }

    [Fact]
    public void IsPendingVoter_DoesNotHoldForAClanThatAlreadyCastItsFinalVote()
    {
        Player voter = PlayerInClan("voter", "clan_a");

        Assert.False(manager.IsPendingVoter(new[] { "clan_a" }, FinalVotesFor("clan_a"), null, voter));
        objectManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void IsPendingVoter_DoesNotHoldOnceEveryEligibleClanHasVoted()
    {
        Player voter = PlayerInClan("voter", "clan_a");

        Assert.False(manager.IsPendingVoter(
            new[] { "clan_a", "clan_b" },
            FinalVotesFor("clan_a", "clan_b"),
            null,
            voter));
        objectManager.VerifyNoOtherCalls();
    }

    [Fact]
    public void IsPendingVoter_StillHoldsForAnEligibleClanThatHasNotVoted()
    {
        string[] eligibleClanIds = { "clan_a", "clan_b" };
        ICollection<string> finalVotes = FinalVotesFor("clan_a");

        Assert.False(manager.IsPendingVoter(eligibleClanIds, finalVotes, null, PlayerInClan("voted", "clan_a")));
        Assert.True(manager.IsPendingVoter(eligibleClanIds, finalVotes, null, PlayerInClan("pending", "clan_b")));
    }

    [Fact]
    public void IsPendingVoter_ResolvesAVoterRegisteredUnderANonCanonicalClanId()
    {
        RegisterClan("registered_clan_a", "clan_a");
        Player voter = PlayerInClan("voter", "registered_clan_a");

        Assert.True(manager.IsPendingVoter(new[] { "clan_a" }, NoFinalVotes, null, voter));
        Assert.False(manager.IsPendingVoter(new[] { "clan_a" }, FinalVotesFor("clan_a"), null, voter));
    }

    [Fact]
    public void IsPendingVoter_DoesNotHoldForAPlayerOutsideTheEligibleClans()
    {
        RegisterClan("clan_b", "clan_b");
        Player outsider = PlayerInClan("outsider", "clan_b");

        Assert.False(manager.IsPendingVoter(new[] { "clan_a" }, NoFinalVotes, null, outsider));
    }

    [Fact]
    public void IsPendingVoter_DoesNotHoldForAPlayerWithNoClan()
    {
        Assert.False(manager.IsPendingVoter(new[] { "clan_a" }, NoFinalVotes, null, PlayerInClan("wanderer", null)));
        Assert.False(manager.IsPendingVoter(new[] { "clan_a" }, NoFinalVotes, null, null));
    }

    private static Player PlayerInClan(string controllerId, string clanId)
    {
        return new Player(controllerId, null, $"{controllerId}-party", clanId, null);
    }

    private static ICollection<string> FinalVotesFor(params string[] clanIds)
    {
        return new HashSet<string>(clanIds);
    }

    private void RegisterClan(string registeredClanId, string canonicalClanId)
    {
        Clan clan = ObjectHelper.SkipConstructor<Clan>();
        objectManager.Setup(objects => objects.TryGetObject(registeredClanId, out clan)).Returns(true);
        objectManager.Setup(objects => objects.TryGetId(clan, out canonicalClanId)).Returns(true);
    }
}
