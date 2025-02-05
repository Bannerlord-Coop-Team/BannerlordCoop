using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _customHomeSettlement
/// </summary>
public record CustomHomeSettlementChanged(string CustomHomeSettlementId, string MobilePartyId) : IEvent
{
    public string CustomHomeSettlementId { get; } = CustomHomeSettlementId;
    public string MobilePartyId { get; } = MobilePartyId;
}