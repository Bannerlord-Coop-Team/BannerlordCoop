using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.UI.Notifications.Messages;

public readonly struct NotifyTroopGivenToSettlement : IEvent
{
    public readonly Hero GiverHero;
    public readonly Settlement Settlement;
    public readonly TroopRoster Troops;

    public NotifyTroopGivenToSettlement(Hero giverHero, Settlement settlement, TroopRoster troops)
    {
        GiverHero = giverHero;
        Settlement = settlement;
        Troops = troops;
    }
}
