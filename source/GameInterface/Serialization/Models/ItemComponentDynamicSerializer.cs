using GameInterface.Serialization.DynamicModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Models
{
    internal class ItemComponentDynamicSerializer : IDynamicSerializer
    {
        public ItemComponentDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<ItemComponent>();
        }
    }
}
