using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public readonly struct MatrixFrameSurrogate
    {
        [ProtoMember(1)]
        public Mat3 Rotation { get; }
        [ProtoMember(2)]
        public Vec3 Origin { get; }

        private MatrixFrameSurrogate(MatrixFrame obj)
        {
            Rotation = obj.rotation;
            Origin = obj.origin;
        }

        private MatrixFrame Deserialize()
        {
            return new MatrixFrame(Rotation, Origin);
        }

        public static implicit operator MatrixFrameSurrogate(MatrixFrame obj)
        {
            return new MatrixFrameSurrogate(obj);
        }

        public static implicit operator MatrixFrame(MatrixFrameSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
