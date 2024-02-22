using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Notify GameInterface to change value.
/// </summary>
[BatchLogMessage]
public record ChangeMobileParty : ICommand
{
    public string SettlementId { get; }
    public string MobilePartyId { get; }
    public int NumberOfLordParties { get; }
    public bool AddMobileParty { get; }

    public ChangeMobileParty(string settlementId, string mobilePartyId, int numberOfLordParties, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        NumberOfLordParties = numberOfLordParties;
        AddMobileParty = addMobileParty;
    }
}
