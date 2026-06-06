using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PlayerCaptivityService.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerSurrendered : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerParty;
    [ProtoMember(2)]
    public readonly string MapEventId;

    public NetworkPlayerSurrendered(string mobilePartyId, string mapEventId)
    {
        PlayerParty = mobilePartyId;
        MapEventId = mapEventId;
    }
}
