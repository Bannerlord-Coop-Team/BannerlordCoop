using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheListAdded : GenericListEvent<Town, Village>
    {
        public TradeBoundVillagesCacheListAdded(Town instance, Village value) : base(instance, value)
        {
        }
    }
}
