using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notifies client game interface that a change needs to happen.
/// </summary>
public record ChangeSettlementBribePaid : ICommand
{
    public string SettlementId { get; }
    public int BribePaid { get; }

    public ChangeSettlementBribePaid(string settlementId, int bribePaid)
    {
        SettlementId = settlementId;
        BribePaid = bribePaid;
    }
}
