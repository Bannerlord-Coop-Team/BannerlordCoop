using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify gameinterface to change miltiia
/// </summary>
public record ChangeSettlementMilitia : ICommand
{
    public uint SettlementId { get; }
    public float Militia { get; }

    public ChangeSettlementMilitia(uint settlementId, float militia)
    {
        SettlementId = settlementId;
        Militia = militia;
    }
}
