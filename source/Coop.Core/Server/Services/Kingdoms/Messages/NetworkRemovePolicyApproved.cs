using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    /// <summary>
    /// Remove policy has been approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRemovePolicyApproved : ICommand
    {
        [ProtoMember(1)]
        public string PolicyId { get; }
        [ProtoMember(2)]
        public string KingdomId { get; }

        public NetworkRemovePolicyApproved(string policyId, string kingdomId)
        {
            PolicyId = policyId;
            KingdomId = kingdomId;
        }
    }
}