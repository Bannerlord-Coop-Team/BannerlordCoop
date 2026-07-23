using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkBarterSerializationTest
{
    [Fact]
    public void BanditBarterRequest_RoundTrip_PreservesCorrelationAndPayment()
    {
        var original = new NetworkRequestBanditBarter(
            "bandit-party",
            250,
            System.Array.Empty<ItemRosterElementData>(),
            System.Array.Empty<TroopRosterElementData>(),
            "bandit-request");

        var result = RoundTrip(original);

        Assert.Equal("bandit-party", result.BanditPartyId);
        Assert.Equal(250, result.PlayerGold);
        Assert.Equal("bandit-request", result.RequestId);
        Assert.Empty(result.PlayerItems ?? System.Array.Empty<ItemRosterElementData>());
        Assert.Empty(result.PlayerPrisoners ?? System.Array.Empty<TroopRosterElementData>());
    }

    [Fact]
    public void PeaceBarterRequest_RoundTrip_PreservesContextAndTerms()
    {
        var original = new NetworkRequestPeaceBarter(
            "target-hero",
            PeaceConversationContext.Location,
            "location-id",
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Item,
                    "owner-hero",
                    "item-id",
                    "modifier-id",
                    false,
                    3),
            },
            "peace-request");

        var result = RoundTrip(original);

        Assert.Equal(original.TargetHeroId, result.TargetHeroId);
        Assert.Equal((int)PeaceConversationContext.Location, result.Context);
        Assert.Equal("location-id", result.ContextId);
        Assert.Equal("peace-request", result.RequestId);
        var term = Assert.Single(result.Terms);
        Assert.Equal((int)PeaceBarterTermType.Item, term.Type);
        Assert.Equal("owner-hero", term.OwnerHeroId);
        Assert.Equal("item-id", term.ObjectId);
        Assert.Equal("modifier-id", term.ItemModifierId);
        Assert.False(term.ItemModifierNull);
        Assert.Equal(3, term.Amount);
    }

    [Fact]
    public void PeaceBarterResult_RoundTrip_PreservesAuthoritativeGold()
    {
        var original = new NetworkPeaceBarterResult(
            "target-party",
            true,
            725,
            "accepted",
            "peace-request");

        var result = RoundTrip(original);

        Assert.Equal(original.ContextId, result.ContextId);
        Assert.Equal("peace-request", result.RequestId);
        Assert.True(result.Accepted);
        Assert.Equal(725, result.PlayerGold);
        Assert.Equal("accepted", result.Reason);
    }

    [Fact]
    public void LordBarterRequest_RoundTrip_PreservesSafePassageContextAndTerms()
    {
        var original = new NetworkRequestLordBarter(
            "target-lord",
            PeaceConversationContext.MapParty,
            "target-party",
            LordBarterKind.SafePassage,
            new[]
            {
                new PeaceBarterTerm(PeaceBarterTermType.Gold, "player-hero", null, null, true, 500),
            },
            "lord-request");

        var result = RoundTrip(original);

        Assert.Equal("target-lord", result.TargetHeroId);
        Assert.Equal((int)PeaceConversationContext.MapParty, result.Context);
        Assert.Equal((int)LordBarterKind.SafePassage, result.Kind);
        Assert.Equal("target-party", result.ContextId);
        Assert.Equal("lord-request", result.RequestId);
        Assert.Equal(500, Assert.Single(result.Terms).Amount);
    }

    [Fact]
    public void LordBarterResult_RoundTrip_PreservesAuthoritativeGold()
    {
        var result = RoundTrip(new NetworkLordBarterResult("target-party", true, 350, null, "lord-request"));

        Assert.True(result.Accepted);
        Assert.Equal(350, result.PlayerGold);
        Assert.Equal("lord-request", result.RequestId);
    }

    [Fact]
    public void LordBarterAuthorization_RoundTrip_PreservesExactContextAndKind()
    {
        var original = new NetworkAuthorizeLordBarter(
            "lord-request",
            "target-lord",
            PeaceConversationContext.MapParty,
            "target-party",
            LordBarterKind.JoinKingdomAsClan);

        var result = RoundTrip(original);

        Assert.Equal("lord-request", result.RequestId);
        Assert.Equal("target-lord", result.TargetHeroId);
        Assert.Equal((int)PeaceConversationContext.MapParty, result.Context);
        Assert.Equal("target-party", result.ContextId);
        Assert.Equal((int)LordBarterKind.JoinKingdomAsClan, result.Kind);
    }

    [Fact]
    public void LordBarterAuthorizationCancellation_RoundTrip_PreservesCorrelation()
    {
        var result = RoundTrip(new NetworkCancelLordBarterAuthorization("lord-request"));

        Assert.Equal("lord-request", result.RequestId);
    }

    [Fact]
    public void MarriageBarterRequest_RoundTrip_PreservesParticipantsContextAndTerms()
    {
        var original = new NetworkRequestMarriageBarter(
            "counterparty-hero",
            MarriageConversationContext.Location,
            "location-id",
            "hero-being-proposed-to",
            "proposing-hero",
            new[]
            {
                new MarriageBarterTerm(
                    MarriageBarterTermType.Gold,
                    "owner-hero",
                    null,
                    null,
                    true,
                    360),
            },
            "marriage-request");

        var result = RoundTrip(original);

        Assert.Equal("counterparty-hero", result.CounterpartyHeroId);
        Assert.Equal((int)MarriageConversationContext.Location, result.Context);
        Assert.Equal("location-id", result.ContextId);
        Assert.Equal("hero-being-proposed-to", result.HeroBeingProposedToId);
        Assert.Equal("proposing-hero", result.ProposingHeroId);
        Assert.Equal("marriage-request", result.RequestId);
        var term = Assert.Single(result.Terms);
        Assert.Equal((int)MarriageBarterTermType.Gold, term.Type);
        Assert.Equal("owner-hero", term.OwnerHeroId);
        Assert.Equal(360, term.Amount);
    }

    [Fact]
    public void MarriageBarterAuthorization_RoundTrip_PreservesExactContext()
    {
        var original = new NetworkAuthorizeMarriageBarter(
            "marriage-request",
            "counterparty-hero",
            MarriageConversationContext.MapParty,
            "party-id",
            "hero-being-proposed-to",
            "proposing-hero");

        var result = RoundTrip(original);

        Assert.Equal("marriage-request", result.RequestId);
        Assert.Equal("counterparty-hero", result.CounterpartyHeroId);
        Assert.Equal((int)MarriageConversationContext.MapParty, result.Context);
        Assert.Equal("party-id", result.ContextId);
        Assert.Equal("hero-being-proposed-to", result.HeroBeingProposedToId);
        Assert.Equal("proposing-hero", result.ProposingHeroId);
    }

    [Fact]
    public void MarriageBarterAccepted_RoundTrip_PreservesCorrelationAndAuthoritativeGold()
    {
        var original = new NetworkMarriageBarterResult(
            "counterparty-hero",
            "hero-being-proposed-to",
            "proposing-hero",
            true,
            640,
            "accepted",
            "marriage-request");

        var result = RoundTrip(original);

        Assert.Equal("counterparty-hero", result.CounterpartyHeroId);
        Assert.Equal("hero-being-proposed-to", result.HeroBeingProposedToId);
        Assert.Equal("proposing-hero", result.ProposingHeroId);
        Assert.Equal("marriage-request", result.RequestId);
        Assert.True(result.Accepted);
        Assert.Equal(640, result.PlayerGold);
        Assert.Equal("accepted", result.Reason);
    }

    private static T RoundTrip<T>(T original)
    {
        using var stream = new MemoryStream();
        RuntimeTypeModel.Default.Serialize(stream, original);
        Assert.NotEmpty(stream.ToArray());

        stream.Position = 0;
        return (T)RuntimeTypeModel.Default.Deserialize(stream, null, typeof(T));
    }
}
