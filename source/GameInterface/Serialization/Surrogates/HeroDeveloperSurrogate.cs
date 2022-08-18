using ProtoBuf;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class HeroDeveloperSurrogate
    {
        public static implicit operator HeroDeveloperSurrogate(HeroDeveloper obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator HeroDeveloper(HeroDeveloperSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}
