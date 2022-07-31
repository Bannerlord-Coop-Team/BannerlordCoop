using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers.Surrogates
{
    [ProtoContract]
    class Vec2Surrogate
    {
        [ProtoMember(1)]
        public float x;

        [ProtoMember(2)]
        public float y;

        public static implicit operator Vec2Surrogate(Vec2 value)
        {
            return new Vec2Surrogate
            {
                x = value.x,
                y = value.y
            };
        }

        public static implicit operator Vec2(Vec2Surrogate value) => new Vec2(value.x, value.y);
    }
}
