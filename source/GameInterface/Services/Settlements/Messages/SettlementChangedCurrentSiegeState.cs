using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Used to let server send message when seigestate changes.
/// </summary>
/// 
[BatchLogMessage]
public record SettlementChangedCurrentSiegeState : ICommand
{
    public string SettlementId { get; }
    public short CurrentSiegeState { get; }

    public SettlementChangedCurrentSiegeState(string settlementId, short currentSiegeState)
    {
        SettlementId = settlementId;
        CurrentSiegeState = currentSiegeState;
    }
}
