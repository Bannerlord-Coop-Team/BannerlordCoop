using Common.Serialization;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Data;
using TaleWorlds.CampaignSystem.Election;

namespace Coop.IntegrationTests.Kingdoms;

[Collection(KingdomSyncGameThreadCollection.Name)]
public class KingdomNetworkMessageSerializationTest
{
    [Fact]
    public void NetworkRequestCreateKingdom_RoundTrips()
    {
        var original = new NetworkRequestCreateKingdom(
            "Player",
            "Real Kingdom",
            "empire",
            "main_party",
            "town_1");

        var copy = RoundTrip(original);

        Assert.Equal("Player", copy.ControllerId);
        Assert.Equal("Real Kingdom", copy.KingdomName);
        Assert.Equal("empire", copy.CultureId);
        Assert.Equal("main_party", copy.PartyId);
        Assert.Equal("town_1", copy.SettlementId);
    }

    [Fact]
    public void NetworkPlayerKingdomCreated_RoundTrips()
    {
        var original = new NetworkPlayerKingdomCreated(
            "Player",
            "Kingdom_Created_1",
            "Real Kingdom",
            "Clan_Player",
            "main_party",
            "town_1",
            "empire");

        var copy = RoundTrip(original);

        Assert.Equal("Player", copy.ControllerId);
        Assert.Equal("Kingdom_Created_1", copy.KingdomId);
        Assert.Equal("Real Kingdom", copy.KingdomName);
        Assert.Equal("Clan_Player", copy.ClanId);
        Assert.Equal("main_party", copy.PartyId);
        Assert.Equal("town_1", copy.SettlementId);
        Assert.Equal("empire", copy.CultureId);
    }

    [Fact]
    public void NetworkRequestKingdomDecisionVote_RoundTrips()
    {
        var original = new NetworkRequestKingdomDecisionVote(
            "Player",
            new KingdomDecisionVoteData(
                "Kingdom_empire",
                2,
                1,
                (int)Supporter.SupportWeights.FullyPush,
                false,
                true,
                "DeclareWarDecisionOutcome:ShouldWarBeDeclared=False"));

        var copy = RoundTrip(original);

        Assert.Equal("Player", copy.ControllerId);
        AssertVoteData(original.VoteData, copy.VoteData);
    }

    [Fact]
    public void NetworkChangeKingdomDecisionVote_RoundTrips()
    {
        var original = new NetworkChangeKingdomDecisionVote(
            "Clan_realclan",
            new KingdomDecisionVoteData(
                "Kingdom_empire",
                3,
                0,
                (int)Supporter.SupportWeights.StronglyFavor,
                true,
                false,
                null));

        var copy = RoundTrip(original);

        Assert.Equal("Clan_realclan", copy.ClanId);
        AssertVoteData(original.VoteData, copy.VoteData);
    }

    [Fact]
    public void NetworkKingdomDecisionResolved_RoundTrips()
    {
        var original = new NetworkKingdomDecisionResolved(
            "Kingdom_empire",
            4,
            2,
            true,
            "KingSelectionDecisionOutcome:King=hero_1",
            "The council has reached a decision.");

        var copy = RoundTrip(original);

        Assert.Equal("Kingdom_empire", copy.KingdomId);
        Assert.Equal(4, copy.DecisionIndex);
        Assert.Equal(2, copy.OutcomeIndex);
        Assert.True(copy.IsPlayerDecision);
        Assert.Equal("KingSelectionDecisionOutcome:King=hero_1", copy.OutcomeKey);
        Assert.Equal("The council has reached a decision.", copy.NotificationText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void NetworkAddDecision_RoundTripsQueueAnswer(bool? wasQueued)
    {
        var original = new NetworkAddDecision(
            "Kingdom_empire",
            new DeclareWarDecisionData("Clan_realclan", "Kingdom_empire", 5, false, true, false, "Kingdom_vlandia"),
            ignoreInfluenceCost: true,
            randomNumber: 0.25f,
            wasQueued: wasQueued);

        var copy = RoundTrip(original);

        Assert.Equal("Kingdom_empire", copy.KingdomId);
        Assert.True(copy.IgnoreInfluenceCost);
        Assert.Equal(0.25f, copy.RandomNumber);
        Assert.Equal(wasQueued, copy.WasQueued);
        var data = Assert.IsType<DeclareWarDecisionData>(copy.Data);
        Assert.Equal("Clan_realclan", data.ProposerClanId);
        Assert.Equal("Kingdom_vlandia", data.FactionToDeclareWarOnId);
    }

    [Fact]
    public void NetworkRequestChangeKingdomName_RoundTrips()
    {
        var original = new NetworkRequestChangeKingdomName(
            "Kingdom_empire",
            "New Kingdom");
        
        var copy = RoundTrip(original);
        
        Assert.Equal("Kingdom_empire", copy.KingdomId);
        Assert.Equal("New Kingdom", copy.Name);
    }

    [Fact]
    public void NetworkKingdomNameChanged_RoundTrips()
    {
        var original = new NetworkKingdomNameChanged("Kingdom_empire");
        
        var copy = RoundTrip(original);
        
        Assert.Equal("Kingdom_empire", copy.KingdomId);
    }

    /// <summary>
    /// Goes through the production serializer so the round trip exercises the runtime model
    /// <see cref="ProtoBufSerializer.ConfigureRuntimeModel()"/> installs, not protobuf-net's defaults.
    /// </summary>
    private static T RoundTrip<T>(T original)
    {
        ProtoBufSerializer.ConfigureRuntimeModel();
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        return serializer.Deserialize<T>(serializer.Serialize(original!));
    }

    private static void AssertVoteData(KingdomDecisionVoteData expected, KingdomDecisionVoteData actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.KingdomId, actual.KingdomId);
        Assert.Equal(expected.DecisionIndex, actual.DecisionIndex);
        Assert.Equal(expected.OutcomeIndex, actual.OutcomeIndex);
        Assert.Equal(expected.SupportWeight, actual.SupportWeight);
        Assert.Equal(expected.IsAbstain, actual.IsAbstain);
        Assert.Equal(expected.IsFinal, actual.IsFinal);
        Assert.Equal(expected.OutcomeKey, actual.OutcomeKey);
    }
}
