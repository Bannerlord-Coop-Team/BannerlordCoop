using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLeavePlayerBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly string MapEventId;

    public NetworkLeavePlayerBattle(string mobilePartyId, string mapEventId)
    {
        MobilePartyId = mobilePartyId;
        MapEventId = mapEventId;
    }
}