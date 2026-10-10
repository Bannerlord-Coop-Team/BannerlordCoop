using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;

internal class ShipBuilder : IObjectBuilder
{
    public object Build()
    {
        ShipHull shipHull = GameObjectCreator.CreateInitializedObject<ShipHull>();
        ShipSlot shipSlot1 = GameObjectCreator.CreateInitializedObject<ShipSlot>();
        ShipSlot shipSlot2 = GameObjectCreator.CreateInitializedObject<ShipSlot>();

        Dictionary<string, ShipSlot> dictionary = new()
        {
            { "slot1", shipSlot1 },
            { "slot2", shipSlot2 }
        };

        shipHull.AvailableSlots = dictionary.GetReadOnlyDictionary<string, ShipSlot>();

        var ship = new Ship(shipHull)
        {
            _name = new TextObject("Test Ship"),
            CustomSailPatternId = null,
        };
        ship.RandomValue = 0;

        return ship;
    }
}
