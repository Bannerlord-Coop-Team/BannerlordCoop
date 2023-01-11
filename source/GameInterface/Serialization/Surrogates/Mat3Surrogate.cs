﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract]
    public readonly struct Mat3Surrogate
    {
        [ProtoMember(1)]
        public Vec3 S { get; }

        [ProtoMember(2)]
        public Vec3 F { get; }

        [ProtoMember(3)]
        public Vec3 U { get; }

        private Mat3Surrogate(Mat3 obj)
        {
            S = obj.s;
            F = obj.f;
            U = obj.u;
        }

        private Mat3 Deserialize()
        {
            return new Mat3(S, F, U);
        }

        public static implicit operator Mat3Surrogate(Mat3 obj)
        {
            return new Mat3Surrogate(obj);
        }

        public static implicit operator Mat3(Mat3Surrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
