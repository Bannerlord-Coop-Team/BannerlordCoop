using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

public enum MangonelLoadPhase
{
    Request,
    Animate,
    Consumed,
    Cancel,
    Ready,
    OwnerConsumed,
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkMangonelLoad : IEvent
{
    [ProtoMember(1)] public string BattleId { get; }
    [ProtoMember(2)] public Guid RequestId { get; }
    [ProtoMember(3)] public Guid GrantId { get; }
    [ProtoMember(4)] public Guid AgentId { get; }
    [ProtoMember(5)] public string AgentOwner { get; }
    [ProtoMember(6)] public long AgentRevision { get; }
    [ProtoMember(7)] public int MachineId { get; }
    [ProtoMember(8)] public int PointId { get; }
    [ProtoMember(9)] public string Simulator { get; }
    [ProtoMember(10)] public int HostEpoch { get; }
    [ProtoMember(11)] public int MachineRevision { get; }
    [ProtoMember(12)] public MangonelLoadPhase Phase { get; }
    [ProtoMember(13)] public string SenderControllerId { get; }

    public NetworkMangonelLoad(string battleId, Guid requestId, Guid grantId, Guid agentId,
        string agentOwner, long agentRevision, int machineId, int pointId,
        string simulator, int hostEpoch, int machineRevision, MangonelLoadPhase phase, string senderControllerId)
    {
        BattleId = battleId;
        RequestId = requestId;
        GrantId = grantId;
        AgentId = agentId;
        AgentOwner = agentOwner;
        AgentRevision = agentRevision;
        MachineId = machineId;
        PointId = pointId;
        Simulator = simulator;
        HostEpoch = hostEpoch;
        MachineRevision = machineRevision;
        Phase = phase;
        SenderControllerId = senderControllerId;
    }

    internal NetworkMangonelLoad WithPhase(MangonelLoadPhase phase, string sender) => new(
        BattleId, RequestId, GrantId, AgentId, AgentOwner, AgentRevision,
        MachineId, PointId, Simulator, HostEpoch, MachineRevision, phase, sender);

    internal bool Matches(NetworkMangonelLoad other) => other != null && RequestId == other.RequestId &&
        BattleId == other.BattleId && GrantId == other.GrantId && AgentId == other.AgentId &&
        AgentOwner == other.AgentOwner && AgentRevision == other.AgentRevision && MachineId == other.MachineId &&
        PointId == other.PointId && Simulator == other.Simulator && HostEpoch == other.HostEpoch &&
        MachineRevision == other.MachineRevision;
}
