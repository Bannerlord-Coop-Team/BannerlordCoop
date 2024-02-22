using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Update all other clients but sender
/// </summary>
public record WagePaymentApprovedOthers : ICommand
{
    public string MobilePartyId { get; }

    public int WageAmount { get; }

    public WagePaymentApprovedOthers(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
