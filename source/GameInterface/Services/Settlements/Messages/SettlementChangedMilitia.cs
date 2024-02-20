using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Notify server of Militia value change
/// </summary>
[BatchLogMessage]
public record SettlementChangedMilitia : IEvent
{
    public string SettlementId { get; }
    public float Militia { get; }

    public SettlementChangedMilitia(string settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
