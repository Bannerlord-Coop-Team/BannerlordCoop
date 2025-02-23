using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages
{
    internal record TroopRosterDataUpdated(TroopRoster Instance, TroopRosterElement Value, int Index) : IEvent
    {
        public TroopRoster Instance { get; } = Instance;
        public TroopRosterElement Value { get; } = Value;
        public int Index { get; } = Index;
    }
}