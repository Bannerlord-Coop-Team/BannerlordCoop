using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageTypes.Messages
{
    internal class VillageTypeCreated : IEvent
    {
        public VillageType VillageType { get; }

        public VillageTypeCreated(VillageType villageType)
        {
            VillageType = villageType;
        }
    }
}
