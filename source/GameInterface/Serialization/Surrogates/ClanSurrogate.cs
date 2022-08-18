using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class ClanSurrogate
    {
        public static implicit operator ClanSurrogate(Clan obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator Clan(ClanSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}