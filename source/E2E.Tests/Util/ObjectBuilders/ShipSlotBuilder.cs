using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class ShipSlotBuilder : IObjectBuilder
{
    public object Build()
    {
        return new ShipSlot();
    }
}
