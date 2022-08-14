using GameInterface.Serialization.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

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
            };

            modelGenerator.Compile();
        }
    }
}
