using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client -&gt; Server] Asks the server to add the given party to an existing battle on the given side. The server
/// performs the authoritative add (<c>PartyBase.MapEventSide</c> setter -&gt; <c>MapEventSide.AddPartyInternal</c>),
/// which replicates the new battle party to all clients through the existing map-event sync.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestJoinBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly BattleSideEnum Side;

    public NetworkRequestJoinBattle(string mapEventId, string partyId, BattleSideEnum side)
    {
        MapEventId = mapEventId;
        PartyId = partyId;
        Side = side;
    }
}
