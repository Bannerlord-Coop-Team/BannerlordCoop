using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;
internal class ItemObjectBuilder : IObjectBuilder
{
    private static int itemCounter;

    public object Build()
    {
        return new ItemObject($"ItemObject_Tests_{++itemCounter}");
    }
}
