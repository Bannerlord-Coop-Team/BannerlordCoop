using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record WorkshopsSet : GenericEvent<Town, Workshop[]>
    {
        public WorkshopsSet(Town instance, Workshop[] value) : base(instance, value)
        {
        }
    }
}
