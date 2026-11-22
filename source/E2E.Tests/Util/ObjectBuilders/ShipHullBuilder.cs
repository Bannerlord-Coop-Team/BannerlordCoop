using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class ShipHullBuilder : IObjectBuilder
{
    public object Build()
    {
        return new ShipHull();
    }
}
