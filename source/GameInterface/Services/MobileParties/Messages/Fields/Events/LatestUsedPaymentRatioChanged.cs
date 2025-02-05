using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

public record LatestUsedPaymentRatioChanged(int LatestUsedPaymentRatio, string MobilePartyId) : IEvent
{
    public int LatestUsedPaymentRatio { get; } = LatestUsedPaymentRatio;
    public string MobilePartyId { get; } = MobilePartyId;
}