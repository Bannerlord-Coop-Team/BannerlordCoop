using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheListRemoved : GenericListEvent<Town, Village>
    {
        public TradeBoundVillagesCacheListRemoved(Town instance, Village value) : base(instance, value)
        {
        }
    }
}
