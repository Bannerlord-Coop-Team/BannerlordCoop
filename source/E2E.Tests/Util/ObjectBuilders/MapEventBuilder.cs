using TaleWorlds.CampaignSystem.MapEvents;

namespace E2E.Tests.Util.ObjectBuilders;
internal class MapEventBuilder : IObjectBuilder
{
    public object Build()
    {
        return new MapEvent();
    }
}