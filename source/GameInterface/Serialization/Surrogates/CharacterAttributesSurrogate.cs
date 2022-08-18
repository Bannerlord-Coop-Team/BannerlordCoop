using ProtoBuf;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterAttributesSurrogate
    {
        public static implicit operator CharacterAttributesSurrogate(CharacterAttributes obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator CharacterAttributes(CharacterAttributesSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}