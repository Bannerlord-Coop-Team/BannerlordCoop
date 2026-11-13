using Common.Messaging;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.ObjectManager;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyTradeContextTests
{
    private readonly PlayerPartyTradeContext trade = new PlayerPartyTradeContext(Mock.Of<IMessageBroker>(), Mock.Of<IObjectManager>());

    [Theory]
    [InlineData(PlayerPartyInteractionOutcomeType.TradeAccepted, "Barter offer accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.TradeDeclined, "Trade proposal declined.")]
    [InlineData(PlayerPartyInteractionOutcomeType.ClanJoinAccepted, "Clan service proposal accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.ClanJoinDeclined, "Clan service proposal declined.")]
    [InlineData(PlayerPartyInteractionOutcomeType.VassalAccepted, "Vassalage offer accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.VassalDeclined, "Vassalage offer declined.")]
    [InlineData(PlayerPartyInteractionOutcomeType.MercenaryAccepted, "Mercenary offer accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.MercenaryDeclined, "Mercenary offer declined.")]
    public void OutcomeMessages_UsePlayerPartyInteractionResult(PlayerPartyInteractionOutcomeType outcomeType, string expectedMessage)
    {
        Assert.Equal(expectedMessage, PlayerPartyTradeContext.GetOutcomeMessage(outcomeType));
    }

    [Fact]
    public void CanAccept_BlocksRepeatAcceptAfterLocalAccept()
    {
        trade.Begin("session-1", localParty: null, localPartyId: null);

        Assert.True(trade.CanAccept());

        trade.UpdateAcceptance(localAccepted: true, remoteAccepted: false);

        Assert.False(trade.CanAccept());
        Assert.False(trade.CanCancel());
    }

    [Fact]
    public void CanReset_BlocksResetWhileActive()
    {
        Assert.True(trade.CanReset());

        trade.Begin("session-1", localParty: null, localPartyId: null);

        Assert.False(trade.CanReset());
    }

    [Fact]
    public void CanOffer_BlocksUnknownBarterableWhenActive()
    {
        Assert.True(trade.CanOffer(null));

        trade.Begin("session-1", localParty: null, localPartyId: null);

        Assert.False(trade.CanOffer(null));
    }
}
