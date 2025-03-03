using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record BuildingsRemoved : GenericEvent<Town, Building>
    {
        public BuildingsRemoved(Town instance, Building value) : base(instance, value)
        {
        }
    }
}
