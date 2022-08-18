using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class ItemObjectSurrogate
    {
        public static implicit operator ItemObjectSurrogate(ItemObject obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator ItemObject(ItemObjectSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}
