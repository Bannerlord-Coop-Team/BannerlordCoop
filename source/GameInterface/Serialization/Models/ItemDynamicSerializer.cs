using GameInterface.Serialization.DynamicModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Models
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
