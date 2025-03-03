using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressAdded : GenericEvent<Town, Building>
    {
        public BuildingsInProgressAdded(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
