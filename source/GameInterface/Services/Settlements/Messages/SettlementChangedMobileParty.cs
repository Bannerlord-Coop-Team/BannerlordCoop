using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Notify Server to send message about mobileparty cache change
/// </summary>
[BatchLogMessage]
public record SettlementChangedMobileParty : ICommand
{
    public string SettlementId { get; }
    public string MobilePartyId { get; }
    public int NumberOfLordParties { get; }
    public bool AddMobileParty { get; }

    public SettlementChangedMobileParty(string settlementId, string mobilePartyId, int numberOfLordParties, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        NumberOfLordParties = numberOfLordParties;
        AddMobileParty = addMobileParty;
    }
}
