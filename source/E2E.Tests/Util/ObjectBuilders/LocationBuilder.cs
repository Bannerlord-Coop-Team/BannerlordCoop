using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class LocationBuilder : IObjectBuilder
{
    public object Build()
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var locationComplex = settlement.LocationComplex ?? new LocationComplex();

        return new Location(
            stringId: "test_location",
            name: new TextObject("TestLocation"),
            doorName: new TextObject("TestLocationDoor"),
            prosperityMax: 100,
            isIndoor: true,
            canBeReserved: false,
            playerCanEnter: "CanAlways",
            playerCanSee: "CanAlways",
            aiCanExit: "CanAlways",
            aiCanEnter: "CanAlways",
            // The constructor copies exactly four scene name slots, one per upgrade level.
            sceneNames: new string[4],
            locationComplex: locationComplex);
    }
}
