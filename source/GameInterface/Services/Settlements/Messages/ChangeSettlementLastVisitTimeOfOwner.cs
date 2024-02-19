using Common.Logging.Attributes;
using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Message for modifying client Settlement.LastVisitTimeOfOwner
/// </summary>
[BatchLogMessage]
public record ChangeSettlementLastVisitTimeOfOwner : ICommand
{
    public string SettlementID { get; }
    public float CurrentTime { get; }

    public ChangeSettlementLastVisitTimeOfOwner(string settlementID, float currentTime)
    {
        SettlementID = settlementID;
        CurrentTime = currentTime;
    }
}
