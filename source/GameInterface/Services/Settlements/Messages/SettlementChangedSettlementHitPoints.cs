using Common.Logging.Attributes;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the settlement hit points change.
/// </summary>
[BatchLogMessage]
public readonly struct SettlementChangedSettlementHitPoints : IEvent
{
    public readonly Settlement Settlement;
    public readonly float SettlementHitPoints;

    public SettlementChangedSettlementHitPoints(Settlement settlement, float settlementHitPoints)
    {
        Settlement = settlement;
        SettlementHitPoints = settlementHitPoints;
    }
}