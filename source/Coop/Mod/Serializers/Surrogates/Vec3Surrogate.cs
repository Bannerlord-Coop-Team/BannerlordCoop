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
    class Vec3Surrogate
    {
        [ProtoMember(1)]
        public float x;

        [ProtoMember(2)]
        public float y;

        [ProtoMember(3)]
        public float z;

        [ProtoMember(4)]
        public float w;

        public static implicit operator Vec3Surrogate(Vec3 value)
        {
            return new Vec3Surrogate
            {
                x = value.x,
                y = value.y,
                z = value.z,
                w = value.w
            };
        }

        public static implicit operator Vec3(Vec3Surrogate value)
        {
            return new Vec3(value.x, value.y, value.z, value.w);
        }
    }
}
