using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.FlattenedTroopRosters.Messages
{
    internal class FlattenedTroopRosterCreated : IEvent
    {
        public FlattenedTroopRoster FlattenedTroopRoster { get; }
        public int Count { get; }

        public FlattenedTroopRosterCreated(FlattenedTroopRoster flattenedTroopRoster, int count)
        {
            FlattenedTroopRoster = flattenedTroopRoster;
            Count = count;
        }
    }
}
