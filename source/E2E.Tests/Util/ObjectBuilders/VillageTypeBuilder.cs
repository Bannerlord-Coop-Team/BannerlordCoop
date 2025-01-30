using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class VillageTypeBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new VillageType("testVillageType");
        }
    }
}