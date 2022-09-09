using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic.Serializers
{
    internal class ItemModifierGroupDynamicSerializer : IDynamicSerializer
    {
        public ItemModifierGroupDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.CreateDynamicSerializer<ItemModifierGroup>();
        }
    }
}
