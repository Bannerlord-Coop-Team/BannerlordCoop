using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters.Messages
{
    internal class ItemRosterCreated : IEvent
    {
        public ItemRoster ItemRoster { get; }

        public ItemRosterCreated(ItemRoster itemRoster)
        {
            ItemRoster = itemRoster;
        }
    }
}
