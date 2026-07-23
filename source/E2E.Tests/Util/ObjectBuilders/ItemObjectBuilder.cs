using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;
internal class ItemObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        return new ItemObject();//new ItemObject(Guid.NewGuid().ToString());
    }
}
