using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages
{
    /// <summary>
    /// Network settlement ownership change
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeSettlementOwnership : ICommand
    {
        [ProtoMember(1)]
        public string SettlementId { get; }
        [ProtoMember(2)]
        public string OwnerId { get; }
        [ProtoMember(3)]
        public string CapturerId { get; }
        [ProtoMember(4)]
        public int Detail { get; }

        public NetworkChangeSettlementOwnership(string settlementId, string ownerId, string capturerId, int detail)
        {
            SettlementId = settlementId;
            OwnerId = ownerId;
            CapturerId = capturerId;
            Detail = detail;
        }
    }
}
