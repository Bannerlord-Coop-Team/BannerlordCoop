using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Messages
{
    internal class VillageCreated : IEvent
    {
        public Village Village { get; }

        public VillageCreated(Village village)
        {
            Village = village;
        }
    }
}
