using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Server-controlled notification of how many players are currently in a map event.
/// Zero means fast-forward is available again; any positive count means it is blocked,
/// and clients report that count when a blocked fast-forward is attempted.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventLockChanged : IEvent
{
    [ProtoMember(1)]
    public readonly int PlayersInMapEvent;

    public NetworkMapEventLockChanged(int playersInMapEvent)
    {
        PlayersInMapEvent = playersInMapEvent;
    }
}
