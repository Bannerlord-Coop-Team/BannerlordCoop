using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Allow entry to a settlement.
/// </summary>
[DontLogMessage]
public record PartyEnterSettlement : ICommand
{
    public string SettlementId { get; }
    public string PartyId { get; }

    public PartyEnterSettlement(string settlementId, string partyId)
    {
        SettlementId = settlementId;
        PartyId = partyId;
    }
}