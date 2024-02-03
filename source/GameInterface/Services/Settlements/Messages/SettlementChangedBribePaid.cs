using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the server changes BribePaid
/// </summary>
public record SettlementChangedBribePaid : ICommand
{

    public string SettlementId { get; }
    public int BribePaid { get; }

    public SettlementChangedBribePaid(string settlementId, int bribePaid)
    {
        SettlementId = settlementId;
        BribePaid = bribePaid;
    }
}
