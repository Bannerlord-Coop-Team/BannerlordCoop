using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents
{
    /// <summary>
    /// Event fired when the local player leaves a settlement.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record SettlementLeft : IEvent
    {
        [ProtoMember(1)]
        public string StringId;
        [ProtoMember(2)]
        public string PartyId;

        public SettlementLeft(string stringId, string partyId)
        {
            StringId = stringId;
            PartyId = partyId;
        }
    }
}