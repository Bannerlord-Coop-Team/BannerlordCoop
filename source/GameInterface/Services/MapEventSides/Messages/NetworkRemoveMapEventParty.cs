using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventSides.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkRemoveMapEventParty : ICommand
    {
        [ProtoMember(1)]
        public string SideId { get; }
        [ProtoMember(2)]
        public string PartyId { get; }

        public NetworkRemoveMapEventParty(string sideId, string partyId)
        {
            SideId = sideId;
            PartyId = partyId;
        }
    }
}