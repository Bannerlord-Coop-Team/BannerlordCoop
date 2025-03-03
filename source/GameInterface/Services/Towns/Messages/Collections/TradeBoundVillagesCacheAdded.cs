using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheAdded : GenericEvent<Town, Village>
    {
        public TradeBoundVillagesCacheAdded(Town instance, Village value) : base(instance, value)
        {
        }
    }
}
