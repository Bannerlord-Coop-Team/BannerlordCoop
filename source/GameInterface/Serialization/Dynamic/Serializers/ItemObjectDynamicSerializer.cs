using GameInterface.Serialization.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class ItemObjectDynamicSerializer : IDynamicSerializer
    {
        public ItemObjectDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<ItemObject>();
        }
    }
}
