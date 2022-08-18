using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterObjectSurrogate
    {
        public static implicit operator CharacterObjectSurrogate(CharacterObject obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator CharacterObject(CharacterObjectSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}
