using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages
{
    internal class TownCreated : IEvent
    {
        public Town Town { get; }

        public TownCreated(Town town)
        {
            Town = town;
        }
    }
}
