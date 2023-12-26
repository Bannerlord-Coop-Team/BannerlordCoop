using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Settlements.Messages
{
    /// <summary>
    /// Event for settlement ownership change request sent from client to server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class SettlementOwnershipChangeRequest : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string OwnerId { get; }
        [ProtoMember(3)]
        public string CapturerId { get; }
        [ProtoMember(4)]
        public int Detail { get; }

        public SettlementOwnershipChangeRequest(string settlementId, string ownerId, string capturerId, int detail)
        {
            SettlementId = settlementId;
            OwnerId = ownerId;
            CapturerId = capturerId;
            Detail = detail;
        }
    }
}