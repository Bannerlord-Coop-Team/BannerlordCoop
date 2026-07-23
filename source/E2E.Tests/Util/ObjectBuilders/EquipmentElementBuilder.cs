
using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;
internal class EquipmentElementBuilder : IObjectBuilder
{
    public object Build()
    {
        var itemObject = GameObjectCreator.CreateInitializedObject<ItemObject>();

        return new EquipmentElement(itemObject);
    }
}
