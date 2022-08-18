using ProtoBuf;
using TaleWorlds.ObjectSystem;

namespace Coop.Serialization.Models
{
    [ProtoContract(SkipConstructor = true)]
    public class MBObjectBaseSurrogate
    {
        public static implicit operator MBObjectBaseSurrogate(MBObjectBase obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator MBObjectBase(MBObjectBaseSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}