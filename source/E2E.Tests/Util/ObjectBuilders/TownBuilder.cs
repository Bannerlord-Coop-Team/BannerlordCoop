using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class TownBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Town();
    }
}
