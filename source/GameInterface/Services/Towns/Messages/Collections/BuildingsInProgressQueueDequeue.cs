using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressQueueDequeue : GenericQueueEvent<Town, Building>
    {
        public BuildingsInProgressQueueDequeue(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
