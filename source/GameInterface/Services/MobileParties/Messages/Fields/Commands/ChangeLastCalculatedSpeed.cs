using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _lastCalculatedSpeed
/// </summary>
public record ChangeLastCalculatedSpeed(float LastCalculatedSpeed, string MobilePartyId) : ICommand
{
    public float LastCalculatedSpeed { get; } = LastCalculatedSpeed;
    public string MobilePartyId { get; } = MobilePartyId;
}