using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Surrogates
{
    public static class SurrogateCollector
    {
        public static void CollectSurrogates()
        {
            RuntimeTypeModel.Default.Add(typeof(Vec3), false).SetSurrogate(typeof(Vec3Surrogate));
            RuntimeTypeModel.Default.Add(typeof(Vec2), false).SetSurrogate(typeof(Vec2Surrogate));
        }
    }
}
