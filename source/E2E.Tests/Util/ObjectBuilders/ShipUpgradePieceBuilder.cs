using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class ShipUpgradePieceBuilder : IObjectBuilder
{
    public object Build()
    {
        return new ShipUpgradePiece();
    }
}
