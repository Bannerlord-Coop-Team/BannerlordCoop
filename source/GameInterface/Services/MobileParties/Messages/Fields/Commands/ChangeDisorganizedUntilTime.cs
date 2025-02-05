using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _disorganizedUntilTime
/// </summary>
public record ChangeDisorganizedUntilTime(long DisorganizedUntilTime, string MobilePartyId) : ICommand
{
    public long DisorganizedUntilTime { get; } = DisorganizedUntilTime;

    public string MobilePartyId { get; } = MobilePartyId;
}