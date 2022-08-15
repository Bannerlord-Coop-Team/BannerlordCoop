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
            if(mbObjectBase == null) return null;
            throw new NotImplementedException();
        }

        public static implicit operator MBObjectBase(MBObjectSurrogate mbObjectSurrogate)
        {
            if( mbObjectSurrogate == null) return null;
            throw new NotImplementedException();
        }
    }
}