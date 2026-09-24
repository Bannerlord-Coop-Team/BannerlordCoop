using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Notify GameInterface to change value.
/// </summary>
public record ChangeMobileParty : ICommand
{
    public uint SettlementId { get; }
    public uint MobilePartyId { get; }
    public bool AddMobileParty { get; }

    public ChangeMobileParty(uint settlementId, uint mobilePartyId, bool addMobileParty)
    {
        SettlementId = settlementId;
        MobilePartyId = mobilePartyId;
        AddMobileParty = addMobileParty;
    }
}
