using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify gameinterface to change miltiia
/// </summary>
[BatchLogMessage]
public record ChangeSettlementMilitia : ICommand
{
    public string SettlementId { get; }
    public float Militia { get; }

    public ChangeSettlementMilitia(string settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
