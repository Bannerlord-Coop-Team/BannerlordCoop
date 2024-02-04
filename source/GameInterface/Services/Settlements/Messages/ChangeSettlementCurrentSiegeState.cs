using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;
/// <summary>
/// Have gameinterface change current siege state
/// </summary>
public record ChangeSettlementCurrentSiegeState : ICommand
{
    public string SettlementId { get; }
    public short CurrentSiegeState { get; }

    public ChangeSettlementCurrentSiegeState(string settlementId, short currentSiegeState)
    {
        SettlementId = settlementId;
        CurrentSiegeState = currentSiegeState;
    }
}
