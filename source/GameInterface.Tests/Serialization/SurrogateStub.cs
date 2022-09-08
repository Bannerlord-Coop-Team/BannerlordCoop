using GameInterface.Serialization.Surrogates;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization
{
    [ProtoContract(SkipConstructor = true)]
    internal class SurrogateStub<T>
    {

        public static implicit operator SurrogateStub<T>(T obj)
        {
            return new SurrogateStub<T>();
        }

        public static implicit operator T(SurrogateStub<T> surrogate)
        {
            return default;
        }
    }
}
