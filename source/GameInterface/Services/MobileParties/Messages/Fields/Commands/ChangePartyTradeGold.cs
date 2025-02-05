using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangePartyTradeGold(int PartyTradeGold, string MobilePartyId) : ICommand
{
    public int PartyTradeGold { get; } = PartyTradeGold;
    public string MobilePartyId { get; } = MobilePartyId;
}