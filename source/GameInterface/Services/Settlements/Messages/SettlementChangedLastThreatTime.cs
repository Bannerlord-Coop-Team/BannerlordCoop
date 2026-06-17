using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the last threat time has been changed or has elapsed for long enough that it is null.
/// </summary>
public readonly struct SettlementChangedLastThreatTime : IEvent
{
    public readonly Settlement Settlement;
    public readonly long? LastThreatTimeTicks;

    public SettlementChangedLastThreatTime(Settlement settlement, long? lastThreatTimeTicks)
    {
        Settlement = settlement;
        LastThreatTimeTicks = lastThreatTimeTicks;
    }
}