using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record ChangePartyComponent(string PartyComponentId, string MobilePartyId) : ICommand
{
    public string PartyComponentId { get; } = PartyComponentId;
    public string MobilePartyId { get; } = MobilePartyId;
}