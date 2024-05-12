using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Util.ObjectBuilders;
internal class HideoutBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Hideout();
    }
}