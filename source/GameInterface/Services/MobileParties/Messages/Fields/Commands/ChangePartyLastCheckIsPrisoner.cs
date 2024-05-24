using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyLastCheckIsPrisoner
/// </summary>
public record ChangePartyLastCheckIsPrisoner(bool PartyLastCheckIsPrisoner, string MobilePartyId) : ICommand
{
    public bool PartyLastCheckIsPrisoner { get; } = PartyLastCheckIsPrisoner;
    public string MobilePartyId { get; } = MobilePartyId;
}