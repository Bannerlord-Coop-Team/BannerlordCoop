using Common.Messaging;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages
{
    internal record TroopRosterDataUpdated : GenericArrayEvent<TroopRoster, TroopRosterElement>
    {
        public TroopRosterDataUpdated(TroopRoster instance, TroopRosterElement value, int index) : base(instance, value, index)
        {
            Instance = instance;
            Value = value;
            Index = index;
        }

        public TroopRoster Instance { get; }
        public TroopRosterElement Value { get; }
        public int Index { get; }
    }
}