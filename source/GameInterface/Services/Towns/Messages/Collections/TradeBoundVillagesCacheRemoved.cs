using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record TradeBoundVillagesCacheRemoved : GenericEvent<Town, Village>
    {
        public TradeBoundVillagesCacheRemoved(Town instance, Village value) : base(instance, value)
        {
        }
    }
}
