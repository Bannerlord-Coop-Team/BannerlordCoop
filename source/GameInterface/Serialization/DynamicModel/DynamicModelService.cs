using GameInterface.Serialization.Models;
using System.Collections.Generic;

namespace GameInterface.Serialization.DynamicModel
{
    internal class DynamicModelService : IDynamicModelService
    {
        public readonly IEnumerable<IDynamicSerializer> DynamicSerializers;
        public DynamicModelService(IDynamicModelGenerator modelGenerator)
        {
            DynamicSerializers = new List<IDynamicSerializer>
            {
                new ItemDynamicSerializer(modelGenerator),
                new ItemComponentDynamicSerializer(modelGenerator),
                new HeroDynamicSerializer(modelGenerator)
            };

            modelGenerator.Compile();
        }
    }
}
