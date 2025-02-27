using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsListAdded : GenericListEvent<Town, Building>
    {
        public BuildingsListAdded(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
