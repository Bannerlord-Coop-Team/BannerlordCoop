using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SettlementComponentBuilder : IObjectBuilder
    {
        public object Build()
        {
            var settlement = GameObjectCreator.CreateInitializedObject<Village>();



            return settlement as SettlementComponent;
        }
    }
}
