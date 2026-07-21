using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

public enum JoinSyncSignal
{
    ReplayComplete,
    ReplayApplied,
    BaselineRequested,
    BaselineApplied,
    FinalBaselineApplied,
    WorldReady,
    CatchUpApplied,
}

/// <summary>Coordinates the ordered replay and baseline barriers for a joining client.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinSync : IMessage
{
    [ProtoMember(1)]
    public readonly JoinSyncSignal Signal;

    public NetworkJoinSync(JoinSyncSignal signal) => Signal = signal;
}
