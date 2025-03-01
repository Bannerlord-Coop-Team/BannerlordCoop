using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages
{
    internal record TroopRosterDataUpdated : GenericArrayEvent<TroopRoster, TroopRosterElement>
    {
        public TroopRosterDataUpdated(TroopRoster instance, TroopRosterElement value, int index) : base(instance, value, index)
        {
        }
    }
}