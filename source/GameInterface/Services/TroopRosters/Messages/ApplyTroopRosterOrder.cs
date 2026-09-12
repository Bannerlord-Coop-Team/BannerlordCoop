using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

public readonly struct ApplyTroopRosterOrder : IEvent
{
    public readonly TroopRoster TroopRoster;
    public readonly TroopRosterOrderData OrderData;

    public ApplyTroopRosterOrder(TroopRoster troopRoster, TroopRosterOrderData orderData)
    {
        TroopRoster = troopRoster;
        OrderData = orderData;
    }
}
