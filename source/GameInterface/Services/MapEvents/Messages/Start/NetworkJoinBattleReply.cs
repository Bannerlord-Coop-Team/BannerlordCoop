using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>[Server -&gt; Client] Completes one pending authoritative battle-join request.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkJoinBattleReply : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly string MapEventId;
    [ProtoMember(3)]
    public readonly string PartyId;
    [ProtoMember(4)]
    public readonly bool Accepted;

    public NetworkJoinBattleReply(string requestId, string mapEventId, string partyId, bool accepted)
    {
        RequestId = requestId;
        MapEventId = mapEventId;
        PartyId = partyId;
        Accepted = accepted;
    }
}
