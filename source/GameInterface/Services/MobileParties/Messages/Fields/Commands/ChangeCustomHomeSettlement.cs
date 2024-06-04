using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _customHomeSettlement
/// </summary>
public record ChangeCustomHomeSettlement(string CustomHomeSettlementId, string MobilePartyId) : ICommand
{
    public string CustomHomeSettlementId { get; } = CustomHomeSettlementId;
    public string MobilePartyId { get; } = MobilePartyId;
}