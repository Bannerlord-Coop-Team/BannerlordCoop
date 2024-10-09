using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class WorkshopTypeBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new WorkshopType();
        }
    }
}
