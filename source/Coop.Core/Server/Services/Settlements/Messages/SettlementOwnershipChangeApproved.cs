using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Settlements.Messages
{
    /// <summary>
    /// Event for settlement ownership change approved sent from server to client
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class SettlementOwnershipChangeApproved : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string OwnerId { get; }
        [ProtoMember(3)]
        public string CapturerId { get; }
        [ProtoMember(4)]
        public int Detail { get; }

        public SettlementOwnershipChangeApproved(string settlementId, string ownerId, string capturerId, int detail)
        {
            SettlementId = settlementId;
            OwnerId = ownerId;
            CapturerId = capturerId;
            Detail = detail;
        }
    }
}