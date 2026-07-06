using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -&gt; Clients] The server (via <c>ServerBattleModeArbiter</c>) claimed a map event for a battle-resolution
/// mode (0 = live mission, 1 = auto-resolve simulation, 2 = unclaimed; see <c>BattleStartMode</c>). Every client whose
/// own party is in that event records it (<c>BattleModeRegistry</c>) so the encounter menu greys out the wrong-mode
/// options. The server sends unclaimed when the last player leaves a live mission and the map event can be resolved
/// another way, while map-event finalization also clears the client state locally.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleModeSet : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly int Mode;

    public NetworkBattleModeSet(string mapEventId, int mode)
    {
        MapEventId = mapEventId;
        Mode = mode;
    }
}
