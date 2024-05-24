using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Event from GameInterface for _partyPureSpeedLastCheckVersion
/// </summary>
public record PartyPureSpeedLastCheckVersionChanged(int PartyPureSpeedLastCheckVersion, string MobilePartyId) : IEvent
{
    public int PartyPureSpeedLastCheckVersion { get; } = PartyPureSpeedLastCheckVersion;
    public string MobilePartyId { get; } = MobilePartyId;
}