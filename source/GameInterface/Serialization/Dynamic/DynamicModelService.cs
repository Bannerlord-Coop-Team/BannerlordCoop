using GameInterface.Serialization.Dynamic.Serializers;
using System.Collections.Generic;

namespace GameInterface.Serialization.Dynamic
{
    internal class DynamicModelService : IDynamicModelService
    {
        public readonly IEnumerable<IDynamicSerializer> DynamicSerializers;
        public DynamicModelService(IDynamicModelGenerator modelGenerator)
        {
            DynamicSerializers = new IDynamicSerializer[]
            {
                new ItemObjectDynamicSerializer(modelGenerator),
                new ItemComponentDynamicSerializer(modelGenerator),
                new HeroDynamicSerializer(modelGenerator)
            };

            modelGenerator.Compile();
        }
    }
}
