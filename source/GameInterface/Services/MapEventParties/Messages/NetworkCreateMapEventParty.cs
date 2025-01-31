using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreateMapEventParty : ICommand
    {
        [ProtoMember(1)]
        public string MapEventPartyId { get; }

        [ProtoMember(2)]
        public string PartyBaseId { get; }

        public NetworkCreateMapEventParty(string mapEventPartyId, string partyBaseId)
        {
            MapEventPartyId = mapEventPartyId;
            PartyBaseId = partyBaseId;
        }
    }
}