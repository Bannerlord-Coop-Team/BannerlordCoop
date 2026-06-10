using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Server-controlled notification that fast-forwarding has become (un)available
/// because players are in map events. Carries the current number of players in a
/// map event so clients can report it when a blocked fast-forward is attempted.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventLockChanged : IEvent
{
    [ProtoMember(1)]
    public readonly bool FastForwardBlocked;

    [ProtoMember(2)]
    public readonly int PlayersInMapEvent;

    public NetworkMapEventLockChanged(bool fastForwardBlocked, int playersInMapEvent)
    {
        FastForwardBlocked = fastForwardBlocked;
        PlayersInMapEvent = playersInMapEvent;
    }
}
