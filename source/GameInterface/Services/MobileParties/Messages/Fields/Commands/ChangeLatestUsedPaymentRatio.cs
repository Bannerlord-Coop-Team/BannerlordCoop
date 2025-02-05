using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Fields.Commands;

/// <summary>
/// Client publish for _latestUsedPaymentRatio
/// </summary>
public record ChangeLatestUsedPaymentRatio(int LatestUsedPaymentRatio, string MobilePartyId) : ICommand
{
    public int LatestUsedPaymentRatio { get; } = LatestUsedPaymentRatio;
    public string MobilePartyId { get; } = MobilePartyId;
}