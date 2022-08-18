using System;
using ProtoBuf;
using TaleWorlds.ObjectSystem;

namespace Coop.Serialization.Models
{
    [ProtoContract]
    // ReSharper disable once UnusedType.Global
    // ReSharper disable once InconsistentNaming
    internal class MBObjectSurrogate
    {
        public static implicit operator MBObjectSurrogate(MBObjectBase mbObjectBase)
        {
            throw new NotImplementedException();
        }

        public static implicit operator MBObjectBase(MBObjectSurrogate mbObjectSurrogate)
        {
            throw new NotImplementedException();
        }
    }
}