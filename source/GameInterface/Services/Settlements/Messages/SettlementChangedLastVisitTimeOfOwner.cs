using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify server to send Settlement.LastVistTimeOfOwner change.
/// </summary>
[BatchLogMessage]
public record SettlementChangedLastVisitTimeOfOwner : IEvent
{
    public string SettlementID { get; }
    public float CurrentTime { get; }

    public SettlementChangedLastVisitTimeOfOwner(string settlementID, float currentTime)
    {
        SettlementID = settlementID;
        CurrentTime = currentTime;
    }
}
