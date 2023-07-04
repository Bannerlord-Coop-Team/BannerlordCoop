using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.MapEvents.Messages
{
    /// <summary>
    /// Request to leave a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementLeaveRequest : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementLeaveRequest(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}