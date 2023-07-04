using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Server.Services.MapEvents
{
    /// <summary>
    /// Allow leave to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementLeaveAllowed : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementLeaveAllowed(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}