using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventSides.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkChangeMapEventSideIFaction : ICommand
    {
        [ProtoMember(1)]
        public string SideId { get; }
        [ProtoMember(2)]
        public string FactionId { get; }
        [ProtoMember(3)]
        public bool IsKingdom { get; }

        public NetworkChangeMapEventSideIFaction(string sideId, string factionId, bool isKingdom)
        {
            SideId = sideId;
            FactionId = factionId;
            IsKingdom = isKingdom;
        }
    }
}
