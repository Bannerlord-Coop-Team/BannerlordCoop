using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Server-controlled lock for local time control input.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTimeControlLockChanged : IEvent
{
    [ProtoMember(1)]
    public readonly bool IsLocked;

    [ProtoMember(2)]
    public readonly int LoadingPlayers;

    public NetworkTimeControlLockChanged(bool isLocked, int loadingPlayers = 0)
    {
        IsLocked = isLocked;
        LoadingPlayers = loadingPlayers;
    }
}
