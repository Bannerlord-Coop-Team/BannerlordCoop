using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;

namespace GameInterface.Serialization
{
    internal class DynamicModelManager
    {
        public DynamicModelManager()
        {
            DynamicModelGenerator.CreateDynamicSerializer<ItemObject>();
            DynamicModelGenerator.CreateDynamicSerializer<ItemComponent>();
        }
    }
}
