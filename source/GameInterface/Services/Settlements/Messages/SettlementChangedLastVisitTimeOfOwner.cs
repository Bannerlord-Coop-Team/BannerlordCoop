using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify server to send Settlement.LastVistTimeOfOwner change.
/// </summary>
[BatchLogMessage]
public readonly struct SettlementChangedLastVisitTimeOfOwner : IEvent
{
    public readonly Settlement Settlement;
    public readonly float CurrentTime;

    public SettlementChangedLastVisitTimeOfOwner(Settlement settlement, float currentTime)
    {
        Settlement = settlement;
        CurrentTime = currentTime;
    }
}
