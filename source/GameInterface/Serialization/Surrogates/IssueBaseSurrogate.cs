using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Serialization.Surrogates
{
    // TODO implement correctly
    [ProtoContract]
    internal class IssueBaseSurrogate
    {
        public static implicit operator IssueBaseSurrogate(IssueBase obj)
        {
            return new IssueBaseSurrogate();
        }

        public static implicit operator IssueBase(IssueBaseSurrogate surrogate)
        {
            return null;
        }
    }
}
