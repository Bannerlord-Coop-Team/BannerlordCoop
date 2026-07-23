using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;
internal class EquipmentBuilder : IObjectBuilder
{
    public object Build()
    {
        return new Equipment();
    }
}
