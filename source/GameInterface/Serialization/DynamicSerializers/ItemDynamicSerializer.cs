using GameInterface.Serialization.DynamicModel;
using TaleWorlds.Core;

namespace GameInterface.Serialization.DynamicSerializers
{
    internal class ItemDynamicSerializer : IDynamicSerializer
    {
        public ItemDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<ItemObject>();
            modelGenerator.CreateDynamicSerializer<ItemComponent>();

            modelGenerator.Compile();
        }
    }
}
