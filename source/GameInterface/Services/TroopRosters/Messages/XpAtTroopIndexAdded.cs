using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

internal readonly struct XpAtTroopIndexAdded : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly int Index;
    public readonly int XpAmount;

    public XpAtTroopIndexAdded(TroopRoster troopRoster, int index, int xpAmount)
    {
        TroopRoster = troopRoster;
        Index = index;
        XpAmount = xpAmount;
    }
}