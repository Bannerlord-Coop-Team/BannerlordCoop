using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the LastThreatTime has been changed or is elapsed for so long its null.
/// </summary>
public record SettlementChangedLastThreatTime : ICommand
{
    public string SettlementId { get; }
    public long? LastThreatTimeTicks { get; }

    public SettlementChangedLastThreatTime(string settlementId, long? lastThreatTime)
    {
        SettlementId = settlementId;
        LastThreatTimeTicks = lastThreatTime;
    }
}
