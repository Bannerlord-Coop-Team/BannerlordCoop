using TaleWorlds.CampaignSystem.Siege;

namespace E2E.Tests.Util.ObjectBuilders;

internal class BesiegerCampBuilder : IObjectBuilder
{
    public object Build()
    {
        var siegeEvent = GameObjectCreator.CreateInitializedObject<SiegeEvent>();
        return new BesiegerCamp(siegeEvent);
    }
}