using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Notify server of the ready militia reserve after native troop conversion.
/// </summary>
public readonly struct SettlementChangedMilitia : IEvent
{
    public readonly Settlement Settlement;
    public readonly float Militia;

    public SettlementChangedMilitia(Settlement settlement, float militia)
    {
        Settlement = settlement;
        Militia = militia;
    }
}
