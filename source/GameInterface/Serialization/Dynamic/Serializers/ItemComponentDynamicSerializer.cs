using GameInterface.Serialization.Dynamic;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class ItemComponentDynamicSerializer : IDynamicSerializer
    {
        public ItemComponentDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<ItemComponent>();
        }
    }
}
