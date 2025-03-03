using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsAdded : GenericEvent<Town, Building>
    {
        public BuildingsAdded(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
