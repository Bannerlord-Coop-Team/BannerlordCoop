using ProtoBuf;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class CharacterPerksSurrogate
    {
        public static implicit operator CharacterPerksSurrogate(CharacterPerks obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator CharacterPerks(CharacterPerksSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}