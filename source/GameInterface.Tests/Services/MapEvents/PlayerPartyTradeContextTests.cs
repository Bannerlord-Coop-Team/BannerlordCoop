using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyTradeContextTests : IDisposable
{
    public void Dispose()
    {
        PlayerPartyTradeContext.End();
    }

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
        PlayerPartyTradeContext.Begin("session-1");

        Assert.True(PlayerPartyTradeContext.CanAccept());

        PlayerPartyTradeContext.UpdateAcceptance(localAccepted: true, remoteAccepted: false);

        Assert.False(PlayerPartyTradeContext.CanAccept());
        Assert.False(PlayerPartyTradeContext.CanCancel());
    }

    [Fact]
    public void CanReset_BlocksResetWhileActive()
    {
        Assert.True(PlayerPartyTradeContext.CanReset());

        PlayerPartyTradeContext.Begin("session-1");

        Assert.False(PlayerPartyTradeContext.CanReset());
    }

    [Fact]
    public void CanOffer_BlocksUnknownBarterableWhenActive()
    {
        Assert.True(PlayerPartyTradeContext.CanOffer(null));

        PlayerPartyTradeContext.Begin("session-1");

        Assert.False(PlayerPartyTradeContext.CanOffer(null));
    }
}
