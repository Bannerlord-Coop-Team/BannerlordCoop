using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Approved the TaskCompletition sources grabs it
/// </summary>
public class SetWagePaymentLimitApproved : ICommand
{

    public string MobilePartyId { get; }

    public int WageAmount { get; }

    public SetWagePaymentLimitApproved(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
