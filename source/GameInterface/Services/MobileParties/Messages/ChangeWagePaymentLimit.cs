using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Only used for the server to modify its own gameinterface
/// </summary>
public record ChangeWagePaymentLimit : ICommand
{

    public string MobilePartyId { get; }

    public int WageAmount { get; }

    public ChangeWagePaymentLimit(string mobilePartyId, int wageAmount)
    {
        MobilePartyId = mobilePartyId;
        WageAmount = wageAmount;
    }
}
