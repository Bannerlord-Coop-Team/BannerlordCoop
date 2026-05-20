using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventSides.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkAddMapEventParty : ICommand
    {
        [ProtoMember(1)]
        public string SideId { get; }
        [ProtoMember(2)]
        public string PartyId { get; }

        public NetworkAddMapEventParty(string sideId, string partyId)
        {
            SideId = sideId;
            PartyId = partyId;
        }
    }
}