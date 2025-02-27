using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressQueueEnqueue : GenericQueueEvent<Town, Building>
    {
        public BuildingsInProgressQueueEnqueue(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
