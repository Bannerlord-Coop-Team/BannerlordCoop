using Common.Messaging;
using GameInterface.Services.MapEvents;
using ProtoBuf;

namespace Coop.Core.Server.Services.MapEvents.Messages
{
    /// <summary>
    /// Allow leave to a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record PartyLeftSettlement : ICommand
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public PartyLeftSettlement(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}