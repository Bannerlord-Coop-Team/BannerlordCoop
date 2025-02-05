using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record PartyTradeGoldChanged(int PartyTradeGold, string MobilePartyId) : IEvent
{
    public int PartyTradeGold { get; } = PartyTradeGold;
    public string MobilePartyId { get; } = MobilePartyId;
}