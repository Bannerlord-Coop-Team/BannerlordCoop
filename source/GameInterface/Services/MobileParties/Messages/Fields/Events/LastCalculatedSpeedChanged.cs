using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Event from GameInterface for _lastCalculatedSpeed
/// </summary>
public record LastCalculatedSpeedChanged(float LastCalculatedSpeed, string MobilePartyId) : IEvent
{
    public float LastCalculatedSpeed { get; } = LastCalculatedSpeed;
    public string MobilePartyId { get; } = MobilePartyId;
}