using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Notify GameInterface to change value.
/// </summary>
public record ChangeMobileParty : ICommand
{
    public string SettlementId { get; }
    public string MobilePartyId { get; }
    public bool AddMobileParty { get; }

    public ChangeMobileParty(string settlementId, string mobilePartyId, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        AddMobileParty = addMobileParty;
    }
}
