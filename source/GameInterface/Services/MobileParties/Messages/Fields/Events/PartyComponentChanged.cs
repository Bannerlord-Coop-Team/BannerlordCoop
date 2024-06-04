using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record PartyComponentChanged(string PartyComponentId, string MobilePartyId) : IEvent
{
    public string PartyComponentId { get; } = PartyComponentId;
    public string MobilePartyId { get; } = MobilePartyId;
}