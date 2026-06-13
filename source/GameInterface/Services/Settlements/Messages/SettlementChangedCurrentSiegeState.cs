using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Used to let server send message when siege state changes.
/// </summary>
public readonly struct SettlementChangedCurrentSiegeState : IEvent
{
    public readonly Settlement Settlement;
    public readonly short CurrentSiegeState;

    public SettlementChangedCurrentSiegeState(Settlement settlement, short currentSiegeState)
    {
        Settlement = settlement;
        CurrentSiegeState = currentSiegeState;
    }
}