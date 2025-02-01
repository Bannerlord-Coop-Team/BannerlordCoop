using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEventSides.Messages;

[ProtoContract(SkipConstructor = true)]
internal class NetworkMapEventSideChangeMobileParty : ICommand
{
    public NetworkMapEventSideChangeMobileParty(string mapEventSideId, string mobilePartyId)
    {
        MapEventSideId = mapEventSideId;
        MobilePartyId = mobilePartyId;
    }

    [ProtoMember(1)]
    public string MapEventSideId { get; }

    [ProtoMember(2)]
    public string MobilePartyId { get; }
}
