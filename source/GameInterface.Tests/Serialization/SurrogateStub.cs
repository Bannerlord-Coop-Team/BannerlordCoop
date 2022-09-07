using GameInterface.Serialization.Surrogates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization
{
    internal class SurrogateStub<T>
    {
        public static implicit operator SurrogateStub<T>(T obj)
        {
            return null;
        }

        public static implicit operator T(SurrogateStub<T> surrogate)
        {
            return default;
        }
    }
}
