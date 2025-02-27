using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Messages.Collections
{
    internal record WorkshopsChanged : GenericArrayEvent<Town, Workshop>
    {
        public WorkshopsChanged(Town instance, Workshop value, int index) : base(instance, value, index)
        {
        }
    }
}
