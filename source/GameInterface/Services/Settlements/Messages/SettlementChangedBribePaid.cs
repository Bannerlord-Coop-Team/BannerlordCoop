using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the npc pays a BribePaid
/// </summary>
public record SettlementChangedBribePaid : IEvent
{

    public string SettlementId { get; }
    public int BribePaid { get; }

    public SettlementChangedBribePaid(string settlementId, int bribePaid)
    {
        SettlementId = settlementId;
        BribePaid = bribePaid;
    }
}
