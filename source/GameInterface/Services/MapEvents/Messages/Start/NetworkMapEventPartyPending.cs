using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventPartyPending : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly string PartyId;

    [ProtoMember(3)]
    public readonly bool IsCancellation;

    public NetworkMapEventPartyPending(string mapEventId, string partyId, bool isCancellation = false)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        IsCancellation = isCancellation;
    }
}
