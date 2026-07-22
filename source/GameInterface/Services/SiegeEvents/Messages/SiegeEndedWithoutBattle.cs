using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Messages;

/// <summary>
/// A siege dissolved on the server without an assault battle (the besieger left or was defeated on
/// the map); players parked on the preparation menus inside need the vanilla end menu.
/// </summary>
public readonly struct SiegeEndedWithoutBattle : IEvent
{
    public readonly Settlement Settlement;
    public readonly bool BesiegerDefeated;

    public SiegeEndedWithoutBattle(Settlement settlement, bool besiegerDefeated)
    {
        Settlement = settlement;
        BesiegerDefeated = besiegerDefeated;
    }
}
