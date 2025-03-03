using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsInProgressRemoved : GenericEvent<Town, Building>
    {
        public BuildingsInProgressRemoved(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
