using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace E2E.Tests.Util.ObjectBuilders;
internal class BuildingBuilder : IObjectBuilder
{
    public object Build()
    {
        var town = GameObjectCreator.CreateInitializedObject<Town>();
        var buildingType = new BuildingType("testBuildingType");

        return new Building(buildingType, town);
    }
}
