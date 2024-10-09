using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class WorkshopBuilder : IObjectBuilder
    {
        public object Build()
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            return new Workshop(settlement, "");
        }
    }
}
