using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Notify server of Militia value change
/// </summary>
public record SettlementChangedMilitia : ICommand
{
    public string SettlementId { get; }
    public float Militia { get; }

    public SettlementChangedMilitia(string settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
