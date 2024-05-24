using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Event from GameInterface for _partyLastCheckIsPrisoner
/// </summary>
public record PartyLastCheckIsPrisonerChanged(bool PartyLastCheckIsPrisoner, string MobilePartyId) : IEvent
{
    public bool PartyLastCheckIsPrisoner { get; } = PartyLastCheckIsPrisoner;
    public string MobilePartyId { get; } = MobilePartyId;
}