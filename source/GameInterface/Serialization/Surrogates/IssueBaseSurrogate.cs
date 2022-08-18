using ProtoBuf;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class IssueBaseSurrogate
    {
        public static implicit operator IssueBaseSurrogate(IssueBase obj)
        {
            if (obj == null) return null;

            // TODO implement
            return null;
        }

        public static implicit operator IssueBase(IssueBaseSurrogate surrogate)
        {
            if (surrogate == null) return null;

            // TODO implement
            return default;
        }
    }
}