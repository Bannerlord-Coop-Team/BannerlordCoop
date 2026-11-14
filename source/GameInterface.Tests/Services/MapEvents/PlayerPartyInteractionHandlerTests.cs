using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyInteractionHandlerTests
{
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
        Assert.Equal(expectedMessage, PlayerPartyInteractionHandler.GetOutcomeMessage(outcomeType));
    }
}
