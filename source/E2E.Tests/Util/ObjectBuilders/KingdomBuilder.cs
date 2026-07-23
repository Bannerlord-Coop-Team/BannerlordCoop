using TaleWorlds.CampaignSystem;

namespace E2E.Tests.Util.ObjectBuilders;
internal class KingdomBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Kingdom();
    }
}
