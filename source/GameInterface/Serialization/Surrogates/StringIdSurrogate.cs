using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class StringIdSurrogate<T> where T : MBObjectBase
    {
        [ProtoMember(1)]
        private readonly string StringId;

        protected StringIdSurrogate(T obj)
        {
            StringId = obj.StringId;
        }

        protected T Deserialize()
        {
            return MBObjectManager.Instance?.GetObject<T>(StringId);
        }

        public static implicit operator StringIdSurrogate<T>(T obj)
        {
            if (obj == null) return null;

            return new StringIdSurrogate<T>(obj);
        }

        public static implicit operator T(StringIdSurrogate<T> surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
