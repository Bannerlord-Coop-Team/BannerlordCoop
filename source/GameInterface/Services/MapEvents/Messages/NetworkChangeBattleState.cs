using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract]
public readonly struct NetworkChangeBattleState : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleState BattleState;
    /// <summary>
    /// BR-102: the sender's host epoch for this battle — a victory report is a host-authority message, so
    /// the server refuses one stamped by a stale hosting generation. 0 = unstamped (no host assignment is
    /// known for the map event, e.g. a battle without a coop mission).
    /// </summary>
    [ProtoMember(3)]
    public readonly int HostEpoch;

    public NetworkChangeBattleState(string mapEventId, BattleState battleState, int hostEpoch = 0)
    {
        MapEventId = mapEventId;
        BattleState = battleState;
        HostEpoch = hostEpoch;
    }
}

/// <summary>[Local] Reports whether a received battle-state change reached the authoritative map event.</summary>
public readonly struct BattleStateChangeProcessed : IEvent
{
    public readonly string MapEventId;
    public readonly BattleState BattleState;
    public readonly bool Applied;

    public BattleStateChangeProcessed(string mapEventId, BattleState battleState, bool applied)
    {
        MapEventId = mapEventId;
        BattleState = battleState;
        Applied = applied;
    }
}
