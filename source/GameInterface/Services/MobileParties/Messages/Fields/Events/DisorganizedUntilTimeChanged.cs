using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

public record DisorganizedUntilTimeChanged(long DisorganizedUntilTime, string MobilePartyId) : IEvent
{
    public long DisorganizedUntilTime { get; } = DisorganizedUntilTime;

    public string MobilePartyId { get; } = MobilePartyId;
}