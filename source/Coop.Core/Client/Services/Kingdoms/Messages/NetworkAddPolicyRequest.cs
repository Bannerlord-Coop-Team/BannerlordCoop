using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAddPolicyRequest : ICommand
    {
        [ProtoMember(1)]
        public string PolicyId { get; }
        [ProtoMember(2)]
        public string KingdomId { get; }

        public NetworkAddPolicyRequest(string policyId, string kingdomId)
        {
            PolicyId = policyId;
            KingdomId = kingdomId;
        }
    }
}