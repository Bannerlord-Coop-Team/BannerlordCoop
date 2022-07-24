using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers
{
    internal class SerializerConfig
    {
        public static readonly HashSet<Type> MarkAsNonSerializable = new HashSet<Type>
        {
            typeof(TextObject),
        };
    }
}
