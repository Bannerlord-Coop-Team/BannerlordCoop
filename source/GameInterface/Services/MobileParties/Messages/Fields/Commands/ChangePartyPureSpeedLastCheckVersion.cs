using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _partyPureSpeedLastCheckVersion
/// </summary>
public record ChangePartyPureSpeedLastCheckVersion(int PartyPureSpeedLastCheckVersion, string MobilePartyId) : ICommand
{
    public int PartyPureSpeedLastCheckVersion { get; } = PartyPureSpeedLastCheckVersion;
    public string MobilePartyId { get; } = MobilePartyId;
}