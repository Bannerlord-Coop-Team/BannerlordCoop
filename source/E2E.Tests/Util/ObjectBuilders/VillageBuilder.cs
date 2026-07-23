using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class VillageBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Village();
    }
}
