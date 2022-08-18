using ProtoBuf;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterTraitsSurrogate
    {
        public static implicit operator CharacterTraitsSurrogate(CharacterTraits obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator CharacterTraits(CharacterTraitsSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}