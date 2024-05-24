using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
public record IgnoredUntilTimeChanged(long IgnoredUntilTime, string MobilePartyId) : IEvent
{
    public long IgnoredUntilTime { get; } = IgnoredUntilTime;
    public string MobilePartyId { get; } = MobilePartyId;
}