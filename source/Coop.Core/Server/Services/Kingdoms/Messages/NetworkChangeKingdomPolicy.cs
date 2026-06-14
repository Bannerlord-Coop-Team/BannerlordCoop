using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Kingdoms.Messages
{
    /// <summary>
    /// Network message replicating a kingdom policy add/remove to clients.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeKingdomPolicy : ICommand
    {
        [ProtoMember(1)]
        public string KingdomId { get; }
        [ProtoMember(2)]
        public string PolicyId { get; }
        [ProtoMember(3, IsRequired = true)]
        public bool IsAdd { get; }

        public NetworkChangeKingdomPolicy(string kingdomId, string policyId, bool isAdd)
        {
            KingdomId = kingdomId;
            PolicyId = policyId;
            IsAdd = isAdd;
        }
    }
}
