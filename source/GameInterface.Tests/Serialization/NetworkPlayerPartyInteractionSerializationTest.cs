using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf.Meta;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkPlayerPartyInteractionSerializationTest
{
    [Fact]
    public void Started_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerPartyInteractionStarted(
            "session-1",
            "initiator-party",
            "responder-party",
            "Initiator",
            "Responder");

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.InitiatorPartyId, result.InitiatorPartyId);
        Assert.Equal(original.ResponderPartyId, result.ResponderPartyId);
        Assert.Equal(original.InitiatorName, result.InitiatorName);
        Assert.Equal(original.ResponderName, result.ResponderName);
    }

    [Fact]
    public void State_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerPartyInteractionState(
            "session-1",
            "party-1",
            "party-2",
            "Other",
            PlayerPartyInteractionPhase.ProposalPending,
            PlayerPartyInteractionProposal.Trade,
            new[] { PlayerPartyInteractionOption.AcceptProposal, PlayerPartyInteractionOption.DeclineProposal },
            isInitiator: false,
            initiatorAcceptedTrade: true,
            responderAcceptedTrade: false,
            partyItems: new[] { new ItemRosterElementData(new ItemObjectData("party-item", null, itemModifierNull: true), 3) },
            otherPartyItems: new[] { new ItemRosterElementData(new ItemObjectData("other-item", null, itemModifierNull: true), 4) });

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.PartyId, result.PartyId);
        Assert.Equal(original.OtherPartyId, result.OtherPartyId);
        Assert.Equal(original.OtherPlayerName, result.OtherPlayerName);
        Assert.Equal(original.Phase, result.Phase);
        Assert.Equal(original.Proposal, result.Proposal);
        Assert.Equal(original.Options, result.Options);
        Assert.Equal(original.IsInitiator, result.IsInitiator);
        Assert.Equal(original.InitiatorAcceptedTrade, result.InitiatorAcceptedTrade);
        Assert.Equal(original.ResponderAcceptedTrade, result.ResponderAcceptedTrade);
        Assert.Single(result.PartyItems);
        Assert.Equal("party-item", result.PartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(3, result.PartyItems[0].Amount);
        Assert.Single(result.OtherPartyItems);
        Assert.Equal("other-item", result.OtherPartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(4, result.OtherPartyItems[0].Amount);
    }

    [Fact]
    public void SubmitOption_RoundTrip_PreservesFields()
    {
        var original = new NetworkSubmitPlayerPartyInteractionOption(
            "session-1",
            PlayerPartyInteractionOption.TradeProposal,
            "party-1");

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.Option, result.Option);
        Assert.Equal(original.PartyId, result.PartyId);
    }

    [Fact]
    public void Ended_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerPartyInteractionEnded(
            "session-1",
            "initiator-party",
            "responder-party",
            PlayerPartyInteractionOutcomeType.TradeAccepted);

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.InitiatorPartyId, result.InitiatorPartyId);
        Assert.Equal(original.ResponderPartyId, result.ResponderPartyId);
        Assert.Equal(original.OutcomeType, result.OutcomeType);
    }

    [Fact]
    public void Denied_RoundTrip_PreservesReason()
    {
        var original = new NetworkPlayerPartyInteractionDenied(PlayerPartyInteractionDeniedReason.Hostile);

        var result = RoundTrip(original);

        Assert.Equal(original.Reason, result.Reason);
    }

    [Fact]
    public void TradeOffer_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerPartyTradeOfferUpdated(
            "session-1",
            "party-1",
            new[] { new ItemRosterElementData(new ItemObjectData("item-1", null, itemModifierNull: true), 2) },
            new[] { new TroopRosterElementData("troop-1", 3, 1, 4, isHero: false) },
            offeredGold: 25,
            offeredFiefs: new[] { "fief-1" },
            offeredPrisoners: new[] { new TroopRosterElementData("prisoner-1", 1, 0, 0, isHero: true) });

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.PartyId, result.PartyId);
        Assert.Single(result.OfferedItems);
        Assert.Equal("item-1", result.OfferedItems[0].ItemObjectData.ItemObjectId);
        Assert.Single(result.OfferedTroops);
        Assert.Equal("troop-1", result.OfferedTroops[0].CharacterId);
        Assert.Equal(25, result.OfferedGold);
        Assert.Single(result.OfferedFiefs);
        Assert.Equal("fief-1", result.OfferedFiefs[0]);
        Assert.Single(result.OfferedPrisoners);
        Assert.Equal("prisoner-1", result.OfferedPrisoners[0].CharacterId);
    }

    [Fact]
    public void TradeAccept_RoundTrip_PreservesFields()
    {
        var original = new NetworkPlayerPartyTradeAcceptChanged("session-1", accepted: true);

        var result = RoundTrip(original);

        Assert.Equal(original.SessionId, result.SessionId);
        Assert.Equal(original.Accepted, result.Accepted);
    }

    private static T RoundTrip<T>(T original)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(ms, original);
            bytes = ms.ToArray();
        }

        Assert.NotEmpty(bytes);

        using (var ms = new MemoryStream(bytes))
        {
            return (T)RuntimeTypeModel.Default.Deserialize(ms, null, typeof(T));
        }
    }
}
