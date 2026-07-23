using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// When the NPC pays a bribe.
/// </summary>
public readonly struct SettlementChangedBribePaid : IEvent
{
    public readonly Settlement Settlement;
    public readonly int BribePaid;

    public SettlementChangedBribePaid(Settlement settlement, int bribePaid)
    {
        Settlement = settlement;
        BribePaid = bribePaid;
    }
}