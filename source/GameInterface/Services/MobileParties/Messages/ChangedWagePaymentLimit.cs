using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Notify client that a changedwagepaymentlimit
/// </summary>
public record ChangedWagePaymentLimit : ICommand
{
    public MobileParty MobileParty { get; }

    public int WageAmount { get; }

    public ChangedWagePaymentLimit(MobileParty mobileParty, int wageAmount)
    {
        MobileParty = mobileParty;
        WageAmount = wageAmount;
    }
}
