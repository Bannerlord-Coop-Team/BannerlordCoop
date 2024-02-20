using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Notify client that a changedwagepaymentlimit
/// </summary>
public record ChangedWagePaymentLimit : ICommand
{
    public string MobilePartyId { get; }

    public int WageAmount { get; }

    public ChangedWagePaymentLimit(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
