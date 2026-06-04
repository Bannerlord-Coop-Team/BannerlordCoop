using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Leave;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerSurrendered : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly string MapEventId;

    public NetworkPlayerSurrendered(string mobilePartyId, string mapEventId)
    {
        MobilePartyId = mobilePartyId;
        MapEventId = mapEventId;
    }
}
