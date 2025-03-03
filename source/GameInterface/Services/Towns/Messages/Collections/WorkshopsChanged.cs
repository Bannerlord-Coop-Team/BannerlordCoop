using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record WorkshopsChanged : GenericArrayChangedEvent<Town, Workshop>
    {
        public WorkshopsChanged(Town instance, Workshop value, int index) : base(instance, value, index)
        {
        }
    }
}
