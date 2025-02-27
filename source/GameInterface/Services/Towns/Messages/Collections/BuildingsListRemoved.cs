using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsListRemoved : GenericListEvent<Town, Building>
    {
        public BuildingsListRemoved(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
